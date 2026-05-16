#if UNITY_EDITOR
// ============================================================================
// TankRoyaleMainSceneSetup.cs
// One-shot Editor utility — Menu: TankRoyale ▸ 1 — Setup MainScene
//
// Performs Steps 3-8 of the UX overhaul plan:
//   Step 3  FrontlineService GO + wiring
//   Step 4  Replace CardDragDeploy with CardDragController
//   Step 5  Rebuild HUDCanvas (TopStrip + BottomTray + GameOverPanel)
//   Step 6  Update CameraBattlefieldFitter HUD inset fields
//   Step 7  Add WinConditionManager to MatchTimerManager GO
//   Step 8  Save scene
//
// USAGE: open MainScene in Editor, then run TankRoyale ▸ 1 — Setup MainScene
// ============================================================================

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using SceneMgr = UnityEngine.SceneManagement.SceneManager;

public static class TankRoyaleMainSceneSetup
{
    // ── Asset paths ───────────────────────────────────────────────────────────
    const string FontStencil     = "Assets/Asset Packs/Tanks/Art/Fonts/Big_Shoulders_Stencil/BigShouldersStencil.ttf";
    const string FontBody        = "Assets/Asset Packs/Tanks/Art/Fonts/Exo_2/Exo2.ttf";
    const string PathUnitReg     = "Assets/Data/Events/UnitRegistry.asset";
    const string PathMatchEnd    = "Assets/Data/Events/MatchEndEvent.asset";
    const string PathCpEvtP1     = "Assets/Data/Events/CpChangedEvent_P1.asset";
    const string PathCardActions = "Assets/Misc/CardActions.inputactions";
    const string PathGhost       = "Assets/Prefabs/GhostPreview.prefab";

    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color CHudBg    = new Color(0.06f, 0.08f, 0.12f, 0.96f);
    static readonly Color CCpRailBg = new Color(0.04f, 0.06f, 0.10f, 1.00f);
    static readonly Color CCardBg   = new Color(0.11f, 0.13f, 0.17f, 0.93f);
    static readonly Color CGlowZero = new Color(1.00f, 0.92f, 0.25f, 0.00f);
    static readonly Color CFillBlue = new Color(0.18f, 0.52f, 1.00f, 1.00f);
    static readonly Color CWhite    = Color.white;
    static readonly Color CGrey     = new Color(0.60f, 0.60f, 0.62f);
    static readonly Color CCpYellow = new Color(1.00f, 0.85f, 0.10f);

    // stash set during HUD build, consumed by WinConditionManager step
    static GameObject s_SuddenDeathBanner;

    // =========================================================================
    [MenuItem("TankRoyale/1 — Setup MainScene")]
    public static void Run()
    {
        if (!EnsureMainScene()) return;
        Log("=== MainScene Setup Begin ===");

        // load assets
        var fStencil    = A<Font>(FontStencil);
        var fBody       = A<Font>(FontBody);
        var unitReg     = A<UnitRegistrySO>  (PathUnitReg);
        var matchEnd    = A<MatchEndEventSO> (PathMatchEnd);
        var cpEvtP1     = A<CpChangedEventSO>(PathCpEvtP1);
        var ghostPrefab = A<GameObject>      (PathGhost);
        var cardActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(PathCardActions);

        // find existing scene objects
        var deckMgrGO = Need("DeckManager");
        var cpP1GO    = Need("CP_Manager_P1");
        var cpP2GO    = Need("CP_Manager_P2");
        var timerGO   = Need("MatchTimerManager");
        var camGO     = Need("Main Camera");
        var enemyAIGO = GameObject.Find("EnemyAISummoner");

        // execute steps
        var frontlineGO = Step3(unitReg);
        var dragCtrlGO  = Step4(deckMgrGO, cpP1GO, frontlineGO, camGO, ghostPrefab, cardActions);
        Step5(fStencil, fBody, deckMgrGO, cpP1GO, cpEvtP1, dragCtrlGO, timerGO, matchEnd);
        Step6(camGO);
        Step7(timerGO, unitReg, matchEnd);
        WireAI(enemyAIGO, frontlineGO, cpP2GO, deckMgrGO);

        EditorSceneManager.SaveScene(SceneMgr.GetActiveScene());
        Log("=== Setup complete — scene saved ===");
        EditorUtility.DisplayDialog("TankRoyale", "MainScene setup complete!\nScene saved.", "OK");
    }

    // =========================================================================
    // Step 3 — FrontlineService
    // =========================================================================
    static GameObject Step3(UnitRegistrySO unitReg)
    {
        var go = GameObject.Find("FrontlineService") ?? new GameObject("FrontlineService");
        var fs = go.GetComponent<FrontlineService>() ?? go.AddComponent<FrontlineService>();
        Prop(fs, "_registry", unitReg);
        Log("Step 3: FrontlineService ready.");
        return go;
    }

    // =========================================================================
    // Step 4 — CardDragController (replaces CardDragDeploy)
    // =========================================================================
    static GameObject Step4(
        GameObject deckMgrGO, GameObject cpP1GO, GameObject frontlineGO,
        GameObject camGO, GameObject ghostPrefab,
        UnityEngine.InputSystem.InputActionAsset cardActions)
    {
        var go  = GameObject.Find("CardDragDeploy") ?? new GameObject("CardDragDeploy");

        // remove old script if present
        var old = go.GetComponent<CardDragDeploy>();
        if (old != null) { UnityEngine.Object.DestroyImmediate(old); Log("  Removed CardDragDeploy."); }

        var cdc = go.GetComponent<CardDragController>() ?? go.AddComponent<CardDragController>();
        Prop(cdc, "_cardActionsAsset", cardActions);
        Prop(cdc, "_deckManager",      deckMgrGO?.GetComponent<DeckManager>());
        Prop(cdc, "_cpManager",        cpP1GO?.GetComponent<CommandPointsManager>());
        Prop(cdc, "_frontlineService", frontlineGO?.GetComponent<FrontlineService>());
        Prop(cdc, "_cam",              camGO?.GetComponent<Camera>());
        Prop(cdc, "_ghostPrefab",      ghostPrefab);
        Int (cdc, "_playerTeam",       0);
        Log("Step 4: CardDragController wired.");
        return go;
    }

    // =========================================================================
    // Step 5 — Rebuild HUDCanvas
    // =========================================================================
    static void Step5(
        Font fStencil, Font fBody,
        GameObject deckMgrGO, GameObject cpP1GO, CpChangedEventSO cpEvtP1,
        GameObject dragCtrlGO, GameObject timerGO, MatchEndEventSO matchEnd)
    {
        // destroy old HUDCanvas and stale CardHandUIController
        Destroy("HUDCanvas");
        Destroy("CardHandUIController");

        // ── Canvas root ───────────────────────────────────────────────────────
        var hud    = new GameObject("HUDCanvas");
        var canvas = hud.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = hud.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
        hud.AddComponent<GraphicRaycaster>();

        // ── TopStrip (top 8% = 86px) ──────────────────────────────────────────
        var topStrip = Child(hud, "TopStrip");
        Img(topStrip, CHudBg);
        TopBar(topStrip, 86f);

        var timerPanel = Child(topStrip, "TimerPanel");
        Img(timerPanel, new Color(0f, 0f, 0f, 0.55f));
        Rect(timerPanel, new Vector2(0.38f, 0f), new Vector2(0.62f, 1f),
             V(0.5f, 0.5f), Vector2.zero, new Vector2(0f, -10f));

        var timerTextGO = Child(timerPanel, "TimerText");
        Stretch(timerTextGO);
        var timerTxt = Txt(timerTextGO, fStencil, 40, CWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
        timerTxt.text = "3:00";

        var p1Icons = Child(topStrip, "P1_TowerIcons");
        Rect(p1Icons, V(0f, 0.1f), V(0.32f, 0.9f), V(0f, 0.5f), V(12f, 0f), Vector2.zero);
        Txt(p1Icons, fBody, 16, CGrey, TextAnchor.MiddleLeft).text = "P1:  L-FOB ■   R-FOB ■   HQ ■";

        var p2Icons = Child(topStrip, "P2_TowerIcons");
        Rect(p2Icons, V(0.68f, 0.1f), V(1f, 0.9f), V(1f, 0.5f), V(-12f, 0f), Vector2.zero);
        Txt(p2Icons, fBody, 16, CGrey, TextAnchor.MiddleRight).text = "■ L-FOB   ■ R-FOB   ■ HQ  :P2";

        // ── BottomTray (bottom 22% = 238px) ──────────────────────────────────
        var bot = Child(hud, "BottomTray");
        Img(bot, CHudBg);
        BotBar(bot, 238f);

        // top border line
        var border = Child(bot, "TopBorder");
        Rect(border, V(0f, 1f), V(1f, 1f), V(0.5f, 1f), Vector2.zero, V(0f, 2f));
        Img(border, new Color(0.20f, 0.45f, 0.80f, 1f));

        // ── CPRail ────────────────────────────────────────────────────────────
        var cpRail = Child(bot, "CPRail");
        Rect(cpRail, V(0f, 0f), V(0.21f, 1f), V(0f, 0.5f), V(6f, 0f), V(-8f, -10f));
        Img(cpRail, CCpRailBg);

        var cpLabelGO = Child(cpRail, "CPLabel");
        Rect(cpLabelGO, V(0f, 0.74f), V(1f, 1f), V(0.5f, 1f), Vector2.zero, V(-8f, -6f));
        Txt(cpLabelGO, fStencil, 14, CGrey, TextAnchor.UpperCenter).text = "COMMAND POINTS";

        var cpCountGO = Child(cpRail, "CPCount");
        Rect(cpCountGO, V(0f, 0.30f), V(1f, 0.76f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var cpCountTxt = Txt(cpCountGO, fStencil, 58, CWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
        cpCountTxt.text = "0";

        // Slider (used by CommandPointsManager for fractional fill)
        var sliderGO = Child(cpRail, "CPProgressBar");
        Rect(sliderGO, V(0.05f, 0.04f), V(0.95f, 0.28f), V(0.5f, 0f), Vector2.zero, Vector2.zero);
        var slider = sliderGO.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue  = 0f; slider.maxValue = 1f; slider.value = 0f;

        var slBg = Child(sliderGO, "Background");
        Stretch(slBg);
        Img(slBg, new Color(0.08f, 0.08f, 0.10f, 1f));

        var fillArea = Child(sliderGO, "Fill Area");
        Rect(fillArea, Vector2.zero, Vector2.one, V(0.5f, 0.5f), Vector2.zero, V(-10f, 0f));
        var fillGO = Child(fillArea, "Fill");
        Rect(fillGO, Vector2.zero, V(0f, 1f), V(0.5f, 0.5f), Vector2.zero, V(10f, 0f));
        Img(fillGO, CFillBlue);
        slider.fillRect = fillGO.GetComponent<RectTransform>();

        // ── CardHand ──────────────────────────────────────────────────────────
        var cardHand = Child(bot, "CardHand");
        Rect(cardHand, V(0.21f, 0f), V(1f, 1f), V(0f, 0.5f), V(4f, 0f), V(-8f, -10f));

        var slotGOs    = new GameObject[4];
        var bgImgs     = new Image[4];
        var glowImgs   = new Image[4];
        var typeTxts   = new Text[4];
        var cpCostTxts = new Text[4];
        var hpTxts     = new Text[4];
        var statTxts   = new Text[4];
        var kwTxts     = new Text[4];

        for (int i = 0; i < 4; i++)
        {
            float x0 = i * 0.25f + 0.004f;
            float x1 = x0 + 0.242f;

            var slot = Child(cardHand, $"CardSlot{i}");
            Rect(slot, V(x0, 0.04f), V(x1, 0.96f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            slotGOs[i] = slot;

            // Background (raycastTarget=true so drag fires)
            var bgGO = Child(slot, "BackgroundImage");
            Stretch(bgGO);
            bgImgs[i] = Img(bgGO, CCardBg, raycast: true);

            // Glow overlay (starts transparent)
            var glowGO = Child(slot, "GlowBorder");
            Stretch(glowGO);
            glowImgs[i] = Img(glowGO, CGlowZero, raycast: false);

            // Type (name) at top
            var typeGO = Child(slot, "TypeText");
            Rect(typeGO, V(0f, 0.80f), V(1f, 1f), V(0.5f, 1f), V(0f, -4f), V(-4f, 0f));
            typeTxts[i] = Txt(typeGO, fStencil, 16, CWhite, TextAnchor.MiddleCenter, FontStyle.Bold);

            // CP cost
            var cpCostGO = Child(slot, "CPCostText");
            Rect(cpCostGO, V(0f, 0.62f), V(1f, 0.82f), V(0.5f, 0.5f), Vector2.zero, V(-4f, 0f));
            cpCostTxts[i] = Txt(cpCostGO, fStencil, 20, CCpYellow, TextAnchor.MiddleCenter, FontStyle.Bold);

            // HP
            var hpGO = Child(slot, "HPText");
            Rect(hpGO, V(0f, 0.44f), V(1f, 0.64f), V(0.5f, 0.5f), Vector2.zero, V(-4f, 0f));
            hpTxts[i] = Txt(hpGO, fBody, 13, CWhite, TextAnchor.MiddleCenter);

            // Stats block
            var statsGO = Child(slot, "StatsText");
            Rect(statsGO, V(0f, 0.15f), V(1f, 0.46f), V(0.5f, 0.5f), Vector2.zero, V(-4f, 0f));
            statTxts[i] = Txt(statsGO, fBody, 10, CGrey, TextAnchor.UpperCenter);

            // Keywords
            var kwGO = Child(slot, "KeywordsText");
            Rect(kwGO, V(0f, 0.02f), V(1f, 0.16f), V(0.5f, 0f), V(0f, 2f), V(-4f, 0f));
            kwTxts[i] = Txt(kwGO, fBody, 10, new Color(0.55f, 0.85f, 1f), TextAnchor.UpperCenter);
        }

        // ── SuddenDeathBanner (starts disabled) ───────────────────────────────
        var sd = Child(hud, "SuddenDeathBanner");
        Rect(sd, V(0.2f, 0.43f), V(0.8f, 0.57f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Img(sd, new Color(0.78f, 0.04f, 0.04f, 0.94f));
        var sdTxtGO = Child(sd, "SuddenDeathText");
        Stretch(sdTxtGO);
        Txt(sdTxtGO, fStencil, 52, CWhite, TextAnchor.MiddleCenter, FontStyle.Bold).text = "SUDDEN DEATH";
        sd.SetActive(false);
        s_SuddenDeathBanner = sd;  // stored for Step 7

        // ── GameOverPanel_Root (starts disabled) ──────────────────────────────
        var gopRoot = Child(hud, "GameOverPanel_Root");
        Stretch(gopRoot);
        gopRoot.SetActive(false);

        // dim overlay (clickable)
        var dim    = Child(gopRoot, "DimOverlay");
        Stretch(dim);
        var dimImg = Img(dim, new Color(0f, 0f, 0f, 0.72f), raycast: true);
        var dimBtn = dim.AddComponent<Button>();
        var bc     = dimBtn.colors;
        bc.normalColor = bc.highlightedColor = bc.pressedColor = bc.selectedColor = Color.white;
        dimBtn.colors  = bc;
        dimBtn.targetGraphic = dimImg;

        // VICTORY / DEFEAT / DRAW
        var resultGO  = Child(gopRoot, "ResultText");
        Rect(resultGO, V(0.15f, 0.54f), V(0.85f, 0.73f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var resultTxt = Txt(resultGO, fStencil, 96, CWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
        resultTxt.text = "VICTORY";

        var reasonGO  = Child(gopRoot, "ReasonText");
        Rect(reasonGO, V(0.15f, 0.44f), V(0.85f, 0.55f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var reasonTxt = Txt(reasonGO, fBody, 24, CWhite, TextAnchor.MiddleCenter);
        reasonTxt.text = "by Tower Destruction";

        var statsGO2  = Child(gopRoot, "StatsText");
        Rect(statsGO2, V(0.15f, 0.35f), V(0.85f, 0.45f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var statsTxt2 = Txt(statsGO2, fBody, 22, CGrey, TextAnchor.MiddleCenter);
        statsTxt2.text = "Wins 0    Losses 0    Draws 0";

        var hintGO  = Child(gopRoot, "ContinueHint");
        Rect(hintGO, V(0.15f, 0.27f), V(0.85f, 0.36f), V(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var hintTxt = Txt(hintGO, fBody, 18, CGrey, TextAnchor.MiddleCenter);
        hintTxt.text = "Returning to menu in 4…   (click to skip)";

        // ── Wire CardHandUI onto BottomTray ───────────────────────────────────
        var dm    = deckMgrGO?.GetComponent<DeckManager>();
        var cpMgr = cpP1GO?.GetComponent<CommandPointsManager>();

        var handUI = bot.AddComponent<CardHandUI>();
        Prop(handUI, "_deckManager",    dm);
        Prop(handUI, "_cpManager",      cpMgr);
        Prop(handUI, "_cpChangedEvent", cpEvtP1);

        var soHand  = new SerializedObject(handUI);
        var slotsP  = soHand.FindProperty("_slots");
        slotsP.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var e = slotsP.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("panel"       ).objectReferenceValue = slotGOs[i];
            e.FindPropertyRelative("typeText"    ).objectReferenceValue = typeTxts[i];
            e.FindPropertyRelative("cpCostText"  ).objectReferenceValue = cpCostTxts[i];
            e.FindPropertyRelative("hpText"      ).objectReferenceValue = hpTxts[i];
            e.FindPropertyRelative("statsText"   ).objectReferenceValue = statTxts[i];
            e.FindPropertyRelative("keywordsText").objectReferenceValue = kwTxts[i];
            e.FindPropertyRelative("background"  ).objectReferenceValue = bgImgs[i];
        }
        soHand.ApplyModifiedPropertiesWithoutUndo();
        Log("  CardHandUI wired.");

        // ── Wire CardSlotDragHandler + CardSlotAffordabilityGlow per slot ──────
        var dragCtrl = dragCtrlGO?.GetComponent<CardDragController>();
        for (int i = 0; i < 4; i++)
        {
            var dh = slotGOs[i].AddComponent<CardSlotDragHandler>();
            Int (dh, "_slotIndex",  i);
            Prop(dh, "_controller", dragCtrl);

            var ag = slotGOs[i].AddComponent<CardSlotAffordabilityGlow>();
            Int (ag, "_slotIndex",      i);
            Prop(ag, "_deckManager",    dm);
            Prop(ag, "_cpManager",      cpMgr);
            Prop(ag, "_cpChangedEvent", cpEvtP1);
            Prop(ag, "_glowBorder",     glowImgs[i]);

            // dimmable: bg image + all text elements
            var soAg   = new SerializedObject(ag);
            var dimArr = soAg.FindProperty("_dimmableGraphics");
            dimArr.arraySize = 6;
            dimArr.GetArrayElementAtIndex(0).objectReferenceValue = bgImgs[i];
            dimArr.GetArrayElementAtIndex(1).objectReferenceValue = typeTxts[i];
            dimArr.GetArrayElementAtIndex(2).objectReferenceValue = cpCostTxts[i];
            dimArr.GetArrayElementAtIndex(3).objectReferenceValue = hpTxts[i];
            dimArr.GetArrayElementAtIndex(4).objectReferenceValue = statTxts[i];
            dimArr.GetArrayElementAtIndex(5).objectReferenceValue = kwTxts[i];
            soAg.ApplyModifiedPropertiesWithoutUndo();
        }
        Log("  Drag handlers + affordability glows wired.");

        // ── Re-wire CP_Manager_P1 to new UI ──────────────────────────────────
        if (cpMgr != null)
        {
            Prop(cpMgr, "_cpProgressBar", slider);
            Prop(cpMgr, "_cpCountText",   cpCountTxt);
            Log("  CP_Manager_P1 UI re-wired.");
        }

        // ── Re-wire MatchTimerManager to new TimerText ─────────────────────
        var mtm = timerGO?.GetComponent<MatchTimerManager>();
        if (mtm != null) { Prop(mtm, "_timerText", timerTxt); Log("  MatchTimerManager TimerText re-wired."); }

        // ── Wire GameOverPanel (on HUDCanvas root, always active) ─────────────
        var gop = hud.AddComponent<GameOverPanel>();
        Prop(gop, "_matchEndEvent", matchEnd);
        Prop(gop, "_root",          gopRoot);
        Prop(gop, "_resultText",    resultTxt);
        Prop(gop, "_reasonText",    reasonTxt);
        Prop(gop, "_statsText",     statsTxt2);
        Prop(gop, "_continueHint",  hintTxt);
        Prop(gop, "_clickArea",     dimBtn);
        Log("  GameOverPanel wired.");

        Log("Step 5: HUDCanvas rebuilt.");
    }

    // =========================================================================
    // Step 6 — CameraBattlefieldFitter HUD insets
    // =========================================================================
    static void Step6(GameObject camGO)
    {
        if (camGO == null) return;
        var fitter = camGO.GetComponent<CameraBattlefieldFitter>();
        if (fitter == null) return;
        Flt(fitter, "_hudInsetTop",    0.08f);
        Flt(fitter, "_hudInsetBottom", 0.22f);
        Log("Step 6: Camera HUD insets updated.");
    }

    // =========================================================================
    // Step 7 — WinConditionManager
    // =========================================================================
    static void Step7(GameObject timerGO, UnitRegistrySO unitReg, MatchEndEventSO matchEnd)
    {
        if (timerGO == null) return;
        var wcm = timerGO.GetComponent<WinConditionManager>() ?? timerGO.AddComponent<WinConditionManager>();
        Prop(wcm, "_registry",          unitReg);
        Prop(wcm, "_matchEndEvent",     matchEnd);
        Prop(wcm, "_suddenDeathBanner", s_SuddenDeathBanner);
        var mtm = timerGO.GetComponent<MatchTimerManager>();
        if (mtm != null) Prop(wcm, "_timer", mtm);
        Log("Step 7: WinConditionManager wired.");
    }

    // =========================================================================
    // Wire EnemyAISummoner
    // =========================================================================
    static void WireAI(GameObject aiGO, GameObject frontlineGO, GameObject cpP2GO, GameObject deckMgrGO)
    {
        if (aiGO == null) return;
        var ai = aiGO.GetComponent<EnemyAISummoner>();
        if (ai == null) return;
        Prop(ai, "_frontlineService", frontlineGO?.GetComponent<FrontlineService>());
        Prop(ai, "_cpManager",        cpP2GO?.GetComponent<CommandPointsManager>());
        Prop(ai, "_deckManager",      deckMgrGO?.GetComponent<DeckManager>());
        Log("EnemyAISummoner re-wired.");
    }

    // =========================================================================
    // UI helpers
    // =========================================================================

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    // full-width band pinned to top (pivotY=1) or bottom (pivotY=0)
    static void TopBar(GameObject go, float height)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, height);
    }

    static void BotBar(GameObject go, float height)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 0f);
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, height);
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.pivot            = Vector2.one * 0.5f;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;
    }

    static void Rect(GameObject go,
                     Vector2 ancMin, Vector2 ancMax,
                     Vector2 pivot, Vector2 aPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = ancMin;
        rt.anchorMax        = ancMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = aPos;
        rt.sizeDelta        = sizeDelta;
    }

    static Image Img(GameObject go, Color color, bool raycast = false)
    {
        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = raycast;
        return img;
    }

    static Text Txt(GameObject go, Font font, int size, Color color,
                    TextAnchor align  = TextAnchor.MiddleCenter,
                    FontStyle  style  = FontStyle.Normal)
    {
        var t = go.AddComponent<Text>();
        t.font          = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = size;
        t.color         = color;
        t.alignment     = align;
        t.fontStyle     = style;
        t.raycastTarget = false;
        return t;
    }

    static Vector2 V(float x, float y) => new Vector2(x, y);

    // =========================================================================
    // SerializedObject helpers
    // =========================================================================

    static void Prop(UnityEngine.Object comp, string field, UnityEngine.Object val)
    {
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var sp = so.FindProperty(field);
        if (sp == null) { Debug.LogWarning($"[TankRoyale] Property '{field}' not found on {comp.GetType().Name}"); return; }
        sp.objectReferenceValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Int(UnityEngine.Object comp, string field, int val)
    {
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var sp = so.FindProperty(field);
        if (sp == null) return;
        sp.intValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Flt(UnityEngine.Object comp, string field, float val)
    {
        if (comp == null) return;
        var so = new SerializedObject(comp);
        var sp = so.FindProperty(field);
        if (sp == null) return;
        sp.floatValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =========================================================================
    // Misc helpers
    // =========================================================================

    static T A<T>(string path) where T : UnityEngine.Object
    {
        var t = AssetDatabase.LoadAssetAtPath<T>(path);
        if (t == null) Debug.LogWarning($"[TankRoyale] Asset not found: {path}");
        return t;
    }

    static GameObject Need(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) Debug.LogWarning($"[TankRoyale] Required GO not found: '{name}'");
        return go;
    }

    static void Destroy(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) { UnityEngine.Object.DestroyImmediate(go); Log($"  Destroyed '{name}'."); }
    }

    static bool EnsureMainScene()
    {
        var s = SceneMgr.GetActiveScene();
        if (s.name == "MainScene") return true;
        EditorUtility.DisplayDialog("TankRoyale Setup",
            $"Please open MainScene first.\nCurrent: '{s.name}'", "OK");
        return false;
    }

    static void Log(string m) => Debug.Log($"[TankRoyale] {m}");
}
#endif
