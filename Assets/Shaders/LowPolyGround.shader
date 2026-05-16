// Low-poly cartoon ground shader for Tank Royale.
// Produces a faceted terrain look using world-space cell noise entirely in the
// fragment shader — no geometry subdivision or textures required.
// Works with URP forward rendering and supports fog.
Shader "Tank Royale/LowPolyGround"
{
    Properties
    {
        [Header(Color Palette)]
        _ColorA      ("Dark Green",      Color) = (0.14, 0.36, 0.10, 1)
        _ColorB      ("Mid Green",       Color) = (0.22, 0.50, 0.15, 1)
        _ColorC      ("Light Green",     Color) = (0.31, 0.62, 0.20, 1)
        _ColorD      ("Yellow Green",    Color) = (0.38, 0.60, 0.18, 1)
        _ColorEarth  ("Earth / Dirt",    Color) = (0.46, 0.38, 0.24, 1)

        [Header(Cell Pattern)]
        _CellScale   ("Cell Size (world units)", Float) = 4.5
        _EdgeWidth   ("Edge Width",   Range(0.01, 0.30)) = 0.10
        _EdgeDark    ("Edge Darkening", Range(0, 0.7))   = 0.40
        _Jitter      ("Cell Jitter",  Range(0, 1))       = 0.65

        [Header(Lighting)]
        _SunDir      ("Sun Direction (world)",  Vector) = (0.4, 0.9, 0.3, 0)
        _Ambient     ("Ambient",       Range(0, 1)) = 0.72
        _SunStrength ("Sun Strength",  Range(0, 1)) = 0.28

        [Header(Zones)]
        _RiverWidth  ("River / Mud Width",   Float) = 3.0
        _RiverBlend  ("River Blend",         Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _ColorD;
                float4 _ColorEarth;
                float  _CellScale;
                float  _EdgeWidth;
                float  _EdgeDark;
                float  _Jitter;
                float4 _SunDir;
                float  _Ambient;
                float  _SunStrength;
                float  _RiverWidth;
                float  _RiverBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float  fogFactor   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Hash helpers ────────────────────────────────────────────────────

            float Hash11(float n)
            {
                return frac(sin(n) * 43758.5453123);
            }

            float Hash21(float2 p)
            {
                float n = dot(p, float2(127.1, 311.7));
                return frac(sin(n) * 43758.5453123);
            }

            float2 Hash22(float2 p)
            {
                float2 n = float2(dot(p, float2(127.1, 311.7)),
                                  dot(p, float2(269.5, 183.3)));
                return frac(sin(n) * 43758.5453123);
            }

            // ── Voronoi distance to nearest cell center ─────────────────────────
            // Returns (min dist to cell center, cell random value [0,1])
            float2 Voronoi(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float minDist = 8.0;
                float cellRand = 0.0;

                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = float2(x, y);
                    float2 cellId   = i + neighbor;
                    float2 jitter   = Hash22(cellId) * _Jitter;
                    float2 toCenter = neighbor + jitter - f;
                    float  dist     = dot(toCenter, toCenter);

                    if (dist < minDist)
                    {
                        minDist  = dist;
                        cellRand = Hash21(cellId);
                    }
                }

                return float2(sqrt(minDist), cellRand);
            }

            // Second-closest distance for edge detection
            float VoronoiEdge(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float d0 = 8.0, d1 = 8.0;

                for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    float2 neighbor = float2(x, y);
                    float2 cellId   = i + neighbor;
                    float2 jitter   = Hash22(cellId) * _Jitter;
                    float2 toCenter = neighbor + jitter - f;
                    float  dist     = dot(toCenter, toCenter);

                    if (dist < d0) { d1 = d0; d0 = dist; }
                    else if (dist < d1) { d1 = dist; }
                }

                return sqrt(d1) - sqrt(d0);
            }

            // ── Vertex ─────────────────────────────────────────────────────────

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            // ── Fragment ───────────────────────────────────────────────────────

            half4 frag(Varyings IN) : SV_Target
            {
                float2 xz = IN.worldPos.xz;

                // Scaled coordinates for Voronoi
                float2 scaled = xz / _CellScale;

                // Cell color
                float2 voro     = Voronoi(scaled);
                float  cellRand = voro.y;

                // Secondary noise for within-cell brightness variation
                float2 scaled2  = xz / (_CellScale * 0.35);
                float  microVar = Hash21(floor(scaled2));

                // Pick base color from palette
                half3 col;
                if      (cellRand < 0.22) col = _ColorA.rgb;
                else if (cellRand < 0.48) col = _ColorB.rgb;
                else if (cellRand < 0.72) col = _ColorC.rgb;
                else if (cellRand < 0.88) col = _ColorD.rgb;
                else                      col = _ColorEarth.rgb;

                // Within-cell micro-brightness variation (+/-6%)
                col *= lerp(0.94, 1.06, microVar);

                // Edge darkening — Voronoi "outline" gives the low-poly facet look
                float  edge      = VoronoiEdge(scaled);
                float  edgeMask  = smoothstep(0.0, _EdgeWidth, edge);
                col *= lerp(1.0 - _EdgeDark, 1.0, edgeMask);

                // River / mud zone blended in around X = 0 (the river channel)
                float riverT = 1.0 - saturate((abs(IN.worldPos.x) - _RiverWidth) / _RiverBlend);
                col = lerp(col, _ColorEarth.rgb * 0.88, riverT * 0.55);

                // Flat diffuse lighting (ground normal is always (0,1,0))
                float3 sunDir = normalize(_SunDir.xyz);
                float  NdotL  = saturate(dot(float3(0,1,0), sunDir));
                col = col * (_Ambient + NdotL * _SunStrength);

                // Fog
                half4 finalCol = half4(col, 1.0);
                finalCol.rgb   = MixFog(finalCol.rgb, IN.fogFactor);
                return finalCol;
            }
            ENDHLSL
        }

        // Shadow caster pass so the ground receives correct shadow geometry
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
