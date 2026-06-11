# Agent Rules — Tank Royale

These rules apply to all code written for this project. Follow them unconditionally.

---

## 1. Unity New Input System

- Always use **Unity's New Input System** (`UnityEngine.InputSystem`) for all player input.
- Never use the legacy `Input` class (`Input.GetKey`, `Input.GetAxis`, `Input.GetMouseButton`, etc.).
- Bind actions in an **Input Action Asset** (`.inputactions` file). Do not hard-code key bindings in C# scripts.
- Read input values via `InputAction.ReadValue<T>()` or callback delegates (`InputAction.performed`, `InputAction.canceled`, `InputAction.started`).
- For UI interaction, use the **Input System UI Input Module** component — not the legacy Standalone Input Module.

---

## 2. Event-Driven Architecture (Unity Event System)

- Prefer **events and callbacks** over direct method calls between components. Components should not reach into other components and call their methods directly.
- Use `UnityEvent` for designer-configurable hooks exposed in the Inspector.
- Use C# `event Action` / `event Action<T>` (or `event EventHandler`) for code-to-code communication that does not need Inspector exposure.
- Use a **central event bus / message broker** (a static or ScriptableObject-based event channel) for cross-system communication (e.g., a unit dying should raise `UnitDestroyedEvent` that any interested system can subscribe to — not call each subscriber directly).
- Components must **subscribe in `OnEnable` and unsubscribe in `OnDisable`** to avoid memory leaks and stale references.
- Avoid `FindObjectOfType`, `GameObject.Find`, and `SendMessage`. If a reference is needed, inject it via the Inspector, a ScriptableObject channel, or a service locator — never by searching at runtime.

---

## 3. Reusable, Modular Components

- Write every component as if it will be reused in a different game. A `HealthComponent` should know nothing about tanks, cards, or eras — it handles HP, damage, and death, full stop.
- **Single Responsibility Principle:** one component, one job. Split large MonoBehaviours into focused pieces (e.g., `MovementController`, `TargetSelector`, `AttackController` are three separate components, not one `UnitController` that does everything).
- Avoid hard-coding references to specific scene objects or other component types by name. Depend on interfaces or abstract base classes so the same component works with any conforming object.
- No spaghetti code — if a script needs to know about more than 2–3 other unrelated systems, it is doing too much. Refactor before adding more dependencies.
- Prefer **composition over inheritance**. Build complex units by combining simple components rather than deep MonoBehaviour inheritance chains.
- Any utility or helper logic used in more than one place must be extracted into a shared class or a static utility — never copy-pasted.

---

## 4. ScriptableObjects for Data / Logic Separation

- **All game data lives in ScriptableObjects**, not in MonoBehaviours. This includes unit stats (HP, ATK, ARM, PEN, SPD, MOV, RNG, cost), card definitions, era configurations, and audio/visual profiles.
- MonoBehaviours are **runtime behavior** only — they read data from ScriptableObjects and act on it. They do not own the data.
- Create a clearly named ScriptableObject type per data category: `UnitDataSO`, `CardDataSO`, `EraConfigSO`, `AudioProfileSO`, etc.
- ScriptableObject-based **event channels** (`GameEventSO`, `GameEventSO<T>`) are the preferred cross-system communication mechanism (see Rule 2).
- Never put balancing numbers (damage values, costs, durations) directly in a MonoBehaviour field. If a designer needs to tune it, it belongs in a ScriptableObject.
- ScriptableObjects must be stored under `Assets/Data/` with a clear folder structure (e.g., `Assets/Data/Units/`, `Assets/Data/Cards/`, `Assets/Data/Events/`).

---

## 5. No Procedural UI Construction

- All Canvas hierarchies and UI structure (Image, Text, Slider, LayoutGroup, RectTransform, CanvasScaler, etc.) **must be created and configured in the Unity Editor** — either directly in the scene or as Prefab assets under `Assets/Prefabs/`.
- MonoBehaviour scripts must only hold `[SerializeField]` references to **pre-existing** UI components. They update values (fill amounts, text strings, colors) but never call `new GameObject()`, `AddComponent()`, or manually parent UI nodes at runtime.
- **Exception:** transient data-driven entries (e.g., individual kill-feed rows, damage numbers, notification items) may be instantiated and destroyed at runtime because they represent live game data, not structural UI.
- If a UI element needs to be reused across multiple scene objects, create a **Prefab** for it under `Assets/Prefabs/UI/` and instantiate that prefab — do not reconstruct the hierarchy in code.

---

## 6. Unity MCP Skill Required

- Any task that touches the Unity Editor — scene changes, prefab edits, GameObject wiring, asset creation, play mode, inspector values — **requires the `/unity-assistant-mcp` skill to be active**.
- Before starting any Unity Editor work, check whether the `mcp__unity-mcp__Unity_RunCommand` tool (and related `Unity.*` tools) are available in your tool list.
- If those tools are **not available**, stop immediately and tell the user:
  > "The Unity MCP skill is not active. Please type `/unity-assistant-mcp` to activate it before I can make Unity Editor changes."
- Do not attempt to work around the missing skill by editing files directly or guessing scene state — the MCP connection to the live Editor is required for all scene/prefab/asset work.

---

## 7. Verify Changes After Completion

After finishing any Unity Editor change, **use the MCP to verify the result before reporting it as done**. Do not rely solely on the fact that a RunCommand succeeded — confirm the actual state matches what the user asked for.

Verification checklist (use whichever apply):
- **Script changes**: call `Unity.ReadConsole { "Types": "Error" }` to confirm zero compile errors, then call `Unity.ValidateScript` on the changed file.
- **Prefab/Inspector wiring**: run a `Unity.RunCommand` that reads the serialized fields back and logs them — confirm the expected values appear in the output.
- **Scene changes**: call `Unity.ManageScene { "Action": "GetHierarchy" }` or a targeted `Unity.ManageGameObject` query to confirm the object/component state is correct.
- **UI / visual changes** — **mandatory** for anything the player sees on screen (prefab edits, fonts, button skins, colors, layout tweaks, selection state, overlays, outlines, etc.): enter play mode, drive the UI into the affected state (open the relevant panel, hover/click the affected element, select a card, etc.), capture a screenshot with `Unity.Camera_Capture` or `Unity.SceneView_Capture2DScene`, and **look at it before declaring success**. A passing compile is not visual proof, and reading serialized fields is not visual proof.
- **Behaviour changes**: if the change affects runtime behaviour, enter play mode (`Unity.ManageEditor { "Action": "Play", "WaitForCompletion": true }`), inspect the result via console logs or a screenshot, then stop play mode.

If the verification reveals the result does **not** match the user's request, fix it before summarising the work as complete. Never declare a task done without confirming through the MCP that it actually worked. "I edited the prefab and it compiled" is **not** verification of a UI change — only a screenshot from play mode is.

---

## 8. Game Design Document

- The GDD is located at `Spec/GDD.md`. **Read it before writing any gameplay code.**
- All unit stats, card definitions, era configurations, damage formulas, and win conditions are defined there. Do not invent values — always source them from the GDD.
- If a gameplay decision is not covered by the GDD, ask before proceeding. Do not make assumptions about game design intent.
- The GDD is the authoritative source of truth for what the game is and how it works.
- **Keep §10 (Implementation State) honest.** It goes stale fast — when you finish a feature, move it to "Implemented" and update the affected design section. Don't trust §10 blindly; verify against the actual code/assets before claiming something is or isn't done.

---

## 9. URP Shader Rules (learned the hard way)

Stylized shaders (e.g. `RiverFlow`) run under URP + the SRP Batcher. These bit us repeatedly — follow them:

- **Guard `normalize()` against zero-length vectors.** `normalize(float2(0,0))` is **NaN**, and one NaN poisons the entire fragment → the surface renders flat and ignores every other parameter. Default to a sane fallback when the input length is ~0. (The river rendered flat for an entire session because of exactly this.)
- **Watch swizzles.** A "direction" vector's meaningful component must sit in the channel the shader actually reads (`_FlowDirection.xz`). A value in the wrong channel reads as 0. A default of `(0,-1,0)` is wrong if the shader uses `.xz`; it must be `(0,0,-1)`.
- **`_Time` is *scaled* game time** — it freezes when `Time.timeScale == 0` (the pre-battle hold, pause, game-over). Anything that must keep animating while the game is paused must be driven from `Time.unscaledTime`, written every frame by a small component (pattern: `ShaderTimeDriver`).
- **Per-frame shader params go through a per-material `float4`.** With the SRP Batcher, loose globals (`Shader.SetGlobalFloat`) and runtime **scalar** `Material.SetFloat` proved unreliable; a **Vector** property set with `Material.SetVector` (or a Color via `SetColor`) binds reliably. Use `.x` of a vector for a single driven float. Group `float4`s at the top of the `UnityPerMaterial` CBUFFER, scalars after.
- **Don't "verify" animation with a synchronous `cam.Render()` inside a RunCommand.** There is no frame-boundary flush, and a whole-frame pixel **sum** is phase-invariant (a scrolling pattern sums to ~the same value). Verify with real play-mode frames, **per-pixel** diffs, or by saving a PNG and looking at it.

---

## 10. Unity MCP Working Notes (learned the hard way)

- **`result.Log(...)` wraps every argument in `[ ]`.** A GameObject named `SettingsPanel` prints as `[SettingsPanel]`. Do **not** then search for the literal string `"[SettingsPanel]"` — that match never succeeds and looks like "the object is missing / the editor is corrupted." Confirm exact names with the native `Unity_ManageGameObject { action: "find" }` tool, not by eyeballing bracketed log text.
- **Scene/prefab/asset authoring must happen in Edit mode.** Changes made during Play are discarded on Stop, and `EditorSceneManager.MarkSceneDirty/SaveScene` *throw* in Play mode. Check `EditorApplication.isPlaying` and Stop first.
- **Always confirm the active scene before editing.** Entering/exiting Play, or a match ending, can leave a *different* scene active (e.g. back on `MenuScene` when you expected `MainScene`). Query `Unity_ManageScene { Action: "GetActive" }`; switch with `OpenScene(..., Single)`.
- **Never `SaveScene` blindly.** Guard every save behind "the target object was actually found," so a transiently empty/glitched in-memory scene can't overwrite a good file on disk.
- **Right after Stop or a scene load the editor is briefly unsettled** — `FindObjectsOfType` / `GetRootGameObjects` can return empty or stale results for a command or two. Prefer the native `Unity_ManageGameObject` / `Unity_ManageScene` tools to confirm state, and retry.
- **`Image` is ambiguous** in RunCommand scripts (a namespace vs `UnityEngine.UI.Image`). Alias it: `using UIImage = UnityEngine.UI.Image;`.
- **Author UI controls via `UnityEngine.UI.DefaultControls`** (`CreateSlider`, etc.) for a correct hierarchy. `DefaultControls.Resources` has **no `sprite` field** — use `standard` / `background` / `knob`. (Editor-authoring pre-built structure is fine; runtime construction is still forbidden per §5.)
- **Screenshots:** `ScreenCapture.CaptureScreenshot` captures ScreenSpaceOverlay UI; rendering a camera into a RenderTexture does **not** (overlay canvases aren't in the camera). Save a PNG and read it to confirm UI visually.
- **Trigger zones need a Rigidbody.** `OnTriggerEnter/Exit` only fires if one of the two colliders has a Rigidbody; NavMeshAgent units have none, so trigger volumes (e.g. `TerrainModifierZone`) must carry a **kinematic** Rigidbody.
- **Measuring across frames:** set `Application.runInBackground = true` (a backgrounded/unfocused editor throttles the play loop, so frames barely advance). For static measurements, disable a spawned test unit's `TankAI` so it doesn't wander off and die between commands — and clean up test objects afterward.
- **`Renderer.material` / `sharedMaterial` edits during Play persist to the asset** in the editor. Restore them, or you'll dirty the material file.
- **Names with brackets exist too** (`[River]`, `[HUDCanvas]`), but many do **not** — never assume. Match by substring or confirm via the native find tool rather than guessing the exact string.
- **Never probe `MeshFilter.mesh` from a diagnostic command.** The getter CLONES the shared mesh and re-points the filter at the clone — the component keeps mutating the original while the renderer shows the stale copy. Probe `sharedMesh`; if code rebuilds a runtime mesh, re-assert `_mf.sharedMesh = _mesh` after rebuilds as a guard.
- **`static` fields do not persist between RunCommands** — each command compiles into a fresh assembly. Pass state through the scene/assets, or re-derive it (e.g. re-request a cached render: the callback fires synchronously).
- **Scene-wide find-by-name hits the FIRST match.** Names like `TitleText` exist on several panels; an unscoped find once renamed the wrong panel's title. Scope by parent (`panel.Find("TitleText")`) when editing.
- **Components with `Show()`/`Hide()` APIs on initially-inactive panels need lazy init** — `Awake` hasn't run when `Show()` is first called from another script.
- **Save overlay panels INACTIVE.** A panel accidentally saved active gets animation-hidden at startup (CanvasGroup alpha 0), and any later raw `SetActive(true)` then shows an invisible panel — reads as a black screen.
