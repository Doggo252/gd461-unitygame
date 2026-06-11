// Tank Royale — flowing river water shader (URP-compatible).
//
// Flow direction: -Z world axis (top to bottom in the orthographic top-down
// camera view per Spec/GDD.md §2 — the river runs along the centre line X=0
// with bridges at Z=±12).
//
// Look: stylised, top-down RTS water. Flow reads through scrolling stripes
// (anchored in world space) broken up by FBM noise, with foam riding the crests.
//
// Design note on parameters: the visual tuning (stripe density, contrast, foam,
// etc.) is intentionally baked in as constants rather than exposed as scalar
// material properties. In this project's URP/SRP-Batcher setup, scalar floats in
// UnityPerMaterial were being read incorrectly by the GPU (the river rendered
// flat and static), while float4 properties bound correctly. So everything the
// shader needs at runtime travels through float4s: colours, _FlowDirection, and
// _FlowTimeVec (time). Flow time comes from _FlowTimeVec.x, written every frame
// by ShaderTimeDriver using UNSCALED time, so the water keeps flowing even when
// Time.timeScale == 0 (pre-battle hold, pause menu, game-over).
Shader "Tank Royale/RiverFlow"
{
    Properties
    {
        [Header(Colour)]
        _ShallowColor    ("Shallow Colour",  Color) = (0.32, 0.64, 0.84, 1)
        _DeepColor       ("Deep Colour",     Color) = (0.05, 0.20, 0.42, 1)
        _FoamColor       ("Foam Colour",     Color) = (1.00, 1.00, 1.00, 1)

        [Header(Flow)]
        // Flow axis in world space; the shader uses the X and Z components.
        // (0,0,-1,0) = flow along -Z = top-to-bottom in the top-down view.
        _FlowDirection   ("Flow Direction (uses X,Z)", Vector) = (0, 0, -1, 0)

        // Driven each frame by ShaderTimeDriver with speed-scaled unscaled time
        // (.x). A Vector (not a Float) because runtime SetVector reliably
        // refreshes an SRP-batched material whereas SetFloat does not.
        [HideInInspector] _FlowTimeVec ("Flow Time (driven)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "Queue"           = "Geometry+5"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
            };

            // Only float4s live here — scalar floats in UnityPerMaterial were
            // mis-bound on this setup, so all tunables are constants (below).
            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FlowDirection;
                float4 _FlowTimeVec;
            CBUFFER_END

            // ── Baked tuning constants ──────────────────────────────────────────
            static const float STRIPE_DENSITY   = 0.5;
            static const float STRIPE_CONTRAST  = 0.65;
            static const float STRIPE_JITTER    = 0.5;
            static const float NOISE_SCALE      = 1.2;
            static const float NOISE_STRENGTH   = 0.35;
            static const float FOAM_THRESHOLD   = 0.70;
            static const float FOAM_SOFTNESS    = 0.10;
            static const float FOAM_INTENSITY   = 0.95;
            static const float SPECULAR_STRENGTH= 1.2;
            static const float AMBIENT_FLOOR    = 0.65;

            // ── Cheap value-noise ───────────────────────────────────────────────
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise2D(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 uv)
            {
                float v = 0.0, amp = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    v   += amp * Noise2D(uv);
                    uv  *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Flow axis in world XZ. Guard against a zero/degenerate vector:
                // normalize((0,0)) is NaN, which would poison the whole surface
                // and render the river flat. Default to -Z (top-to-bottom).
                float2 fd      = _FlowDirection.xz;
                float2 flowDir = (dot(fd, fd) > 1e-5) ? normalize(fd) : float2(0.0, -1.0);
                // Time comes from the reliably-bound vector (already speed-scaled
                // by the driver); fall back to _Time.y for material preview.
                float  t = _FlowTimeVec.x > 0.0 ? _FlowTimeVec.x : _Time.y * 1.4;

                float2 worldXZ   = IN.positionWS.xz;
                float  alongFlow = dot(worldXZ, flowDir);

                float jitter1 = Fbm(worldXZ * NOISE_SCALE         + flowDir * t * 0.8) * STRIPE_JITTER;
                float jitter2 = Fbm(worldXZ * (NOISE_SCALE * 2.3) + flowDir * t * 1.6) * STRIPE_JITTER;

                float stripe1 = sin((alongFlow + t * 1.0) * STRIPE_DENSITY * 6.2831 + jitter1 * 4.0);
                float stripe2 = sin((alongFlow + t * 1.8) * STRIPE_DENSITY * 4.5    + jitter2 * 4.0 + 1.7);
                float stripeMix = (stripe1 * 0.6 + stripe2 * 0.4) * 0.5 + 0.5;

                float noiseField = Fbm(worldXZ * (NOISE_SCALE * 0.7) + flowDir * t * 0.6);

                float surface = lerp(noiseField, stripeMix, STRIPE_CONTRAST);
                surface = saturate((surface - 0.5) * 1.6 + 0.5);

                // Procedural normal from the surface field's gradient (specular).
                const float eps = 0.08;
                float sx0 = Fbm(worldXZ * NOISE_SCALE + flowDir * t * 0.8 - float2(eps,0));
                float sx1 = Fbm(worldXZ * NOISE_SCALE + flowDir * t * 0.8 + float2(eps,0));
                float sz0 = Fbm(worldXZ * NOISE_SCALE + flowDir * t * 0.8 - float2(0,eps));
                float sz1 = Fbm(worldXZ * NOISE_SCALE + flowDir * t * 0.8 + float2(0,eps));
                float3 perturb  = float3(sx0 - sx1, 1.0, sz0 - sz1);
                float3 normalWS = normalize(lerp(IN.normalWS, normalize(perturb), NOISE_STRENGTH));

                half3 baseColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, surface);

                float foamMask = smoothstep(FOAM_THRESHOLD - FOAM_SOFTNESS,
                                            FOAM_THRESHOLD + FOAM_SOFTNESS,
                                            surface);
                half3 color = lerp(baseColor, _FoamColor.rgb, foamMask * FOAM_INTENSITY);

                Light  mainLight = GetMainLight();
                float3 viewDir   = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float3 halfDir   = normalize(mainLight.direction + viewDir);
                float  ndotl     = saturate(dot(normalWS, mainLight.direction));
                float  spec      = pow(saturate(dot(normalWS, halfDir)), 64.0);

                half3 lightCol = max(mainLight.color.rgb, half3(0.55, 0.6, 0.7));
                half3 lit      = color * (AMBIENT_FLOOR + (1.0 - AMBIENT_FLOOR) * ndotl) * lightCol;
                lit           += spec * SPECULAR_STRENGTH * lightCol;

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
