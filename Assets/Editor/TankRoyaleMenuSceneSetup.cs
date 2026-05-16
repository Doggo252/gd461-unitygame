#if UNITY_EDITOR
// ============================================================================
// TankRoyaleMenuSceneSetup.cs
// One-shot Editor utility — Menu: TankRoyale ▸ 2 — Polish MenuScene
//
// Performs Steps 9-10 of the UX overhaul plan:
//   Step 9  Resize cards (240×360), bigger title/counter fonts, W/L display
//   Step 10 (DeckBuilderUI already edited to use SceneTransitionService)
//
// USAGE: open MenuScene in Editor, then run TankRoyale ▸ 2 — Polish MenuScene
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SceneMgr = UnityEngine.SceneManagement.SceneManager;

public static class TankRoyaleMenuSceneSetup
{
    const string FontStencil = "Assets/Asset Packs/Tanks/Art/Fonts/Big_Shoulders_Stencil/BigShouldersStencil.ttf";
    const string FontBody    = "Assets/Asset Packs/Tanks/Art/Fonts/Exo_2/Exo2.ttf";
    const string CardEntry   = "Assets/Prefabs/UI/CardEntry.prefab";

    [MenuItem("TankRoyale/2 — Polish MenuScene")]
    public static void Run()
    {
        if (!EnsureMenuScene()) return;
        Log("=== MenuScene Polish Begin ===");

        var fStencil = A<Font>(FontStencil);
        var fBody    = A<Font>(FontBody);

        Step9_FontsAndLayout(fStencil, fBody);
        Step9_StatsDisplay(fStencil, fBody);
        Step9_ResizeCardPrefab();

        EditorSceneManager.SaveScene(SceneMgr.GetActiveScene());
        Log("=== MenuScene polish complete — scene saved ===");
        EditorUtility.DisplayDialog("TankRoyale", "MenuScene polish complete!\nScene saved.", "OK");
    }

    // =========================================================================
    // Step 9a — Fonts, title size, counter size, grid cell size
    // =========================================================================
    static void Step9_FontsAndLayout(Font fStencil, Font fBody)
    {
        // ── Title text ("TANK ROYALE") ────────────────────────────────────────
        var titleGO = GameObject.Find("TitleText");
        if (titleGO != null)
        {
            var t = titleGO.GetComponent<Text>();
            if (t != null)
            {
                t.font      = fStencil ?? t.font;
                t.fontSize  = 120;
                t.fontStyle = FontStyle.Bold;
                Log("  TitleText → 120pt stencil bold.");
            }
        }
        else Log("  WARN: TitleText not found.");

        // ── Selection counter ─────────────────────────────────────────────────
        var selGO = GameObject.Find("SelectionCountText");
        if (selGO != null)
        {
            var t = selGO.GetComponent<Text>();
            if (t != null)
            {
                t.font     = fStencil ?? t.font;
                t.fontSize = 36;
                Log("  SelectionCountText → 36pt stencil.");
            }
        }
        else Log("  WARN: SelectionCountText not found.");

        // ── Start button text (if it has a Text child) ────────────────────────
        var btnGO = GameObject.Find("StartButton");
        if (btnGO != null)
        {
            var t = btnGO.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.font      = fStencil ?? t.font;
                t.fontSize  = 32;
                t.fontStyle = FontStyle.Bold;
                Log("  StartButton label → 32pt stencil bold.");
            }
        }

        // ── GridLayoutGroup cell size ─────────────────────────────────────────
        var contentGO = GameObject.Find("Content");
        if (contentGO != null)
        {
            var grid = contentGO.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize    = new Vector2(240f, 360f);
                grid.spacing     = new Vector2(12f, 12f);
                grid.padding     = new RectOffset(12, 12, 12, 12);
                Log("  GridLayoutGroup cellSize → 240×360, spacing 12.");
            }
            else Log("  WARN: GridLayoutGroup not found on Content.");
        }
        else Log("  WARN: Content GO not found.");
    }

    // =========================================================================
    // Step 9b — W/L/D stats display under the title
    // =========================================================================
    static void Step9_StatsDisplay(Font fStencil, Font fBody)
    {
        // Don't duplicate
        if (GameObject.Find("MenuStatsDisplay") != null)
        {
            Log("  MenuStatsDisplay already exists — skipping creation.");
            return;
        }

        var titleGO = GameObject.Find("TitleText");
        if (titleGO == null) { Log("  WARN: TitleText not found — can't place StatsDisplay."); return; }

        // Create sibling next to the title
        var statsGO = new GameObject("MenuStatsDisplay", typeof(RectTransform));
        statsGO.transform.SetParent(titleGO.transform.parent, false);

        // Place in the strip between SelectionCountText (~0.82-0.88) and the deck grid
        // so it never collides with title or counter.
        var statsRT = statsGO.GetComponent<RectTransform>();
        statsRT.anchorMin        = new Vector2(0.25f, 0.77f);
        statsRT.anchorMax        = new Vector2(0.75f, 0.81f);
        statsRT.pivot            = new Vector2(0.50f, 0.50f);
        statsRT.anchoredPosition = Vector2.zero;
        statsRT.sizeDelta        = Vector2.zero;

        // Text
        var statsText = statsGO.AddComponent<Text>();
        statsText.font          = fBody ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statsText.fontSize      = 22;
        statsText.color         = new Color(0.70f, 0.70f, 0.75f);
        statsText.alignment     = TextAnchor.UpperCenter;
        statsText.raycastTarget = false;
        statsText.text          = "WINS  0     LOSSES  0     DRAWS  0";

        // MenuStatsDisplay component
        var msd = statsGO.AddComponent<MenuStatsDisplay>();
        var so  = new SerializedObject(msd);
        var sp  = so.FindProperty("_statsText");
        if (sp != null) { sp.objectReferenceValue = statsText; so.ApplyModifiedPropertiesWithoutUndo(); }

        Log("  MenuStatsDisplay created and wired.");
    }

    // =========================================================================
    // Step 9c — Resize CardEntry prefab to 240×360
    // =========================================================================
    static void Step9_ResizeCardPrefab()
    {
        var path = CardEntry;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Log($"  WARN: CardEntry.prefab not found at {path}"); return; }

        // Edit in prefab mode so all instances update
        var root = PrefabUtility.LoadPrefabContents(path);
        bool changed = false;
        try
        {
            var rt = root.GetComponent<RectTransform>();
            if (rt != null && rt.sizeDelta != new Vector2(240f, 360f))
            {
                rt.sizeDelta = new Vector2(240f, 360f);
                changed = true;
                Log("  CardEntry.prefab root → 240×360.");
            }

            // Scale up any Text components inside the prefab
            foreach (var t in root.GetComponentsInChildren<Text>(true))
            {
                int prev = t.fontSize;
                // Apply a 1.38× scale factor (174→240 = 1.38) to all font sizes
                t.fontSize = Mathf.RoundToInt(t.fontSize * 1.38f);
                if (t.fontSize != prev) changed = true;
            }
            if (changed) Log("  CardEntry.prefab text sizes scaled ×1.38.");
        }
        finally
        {
            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        Log("  CardEntry.prefab resize done.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static T A<T>(string path) where T : UnityEngine.Object
    {
        var t = AssetDatabase.LoadAssetAtPath<T>(path);
        if (t == null) Debug.LogWarning($"[TankRoyale] Asset not found: {path}");
        return t;
    }

    static bool EnsureMenuScene()
    {
        var s = SceneMgr.GetActiveScene();
        if (s.name == "MenuScene") return true;
        EditorUtility.DisplayDialog("TankRoyale Setup",
            $"Please open MenuScene first.\nCurrent: '{s.name}'", "OK");
        return false;
    }

    static void Log(string m) => Debug.Log($"[TankRoyale] {m}");
}
#endif
