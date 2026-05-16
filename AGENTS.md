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
- **Behaviour changes**: if the change affects runtime behaviour, enter play mode (`Unity.ManageEditor { "Action": "Play", "WaitForCompletion": true }`), inspect the result via console logs or a screenshot, then stop play mode.

If the verification reveals the result does **not** match the user's request, fix it before summarising the work as complete. Never declare a task done without confirming through the MCP that it actually worked.

---

## 8. Game Design Document

- The GDD is located at `Spec/GDD.md`. **Read it before writing any gameplay code.**
- All unit stats, card definitions, era configurations, damage formulas, and win conditions are defined there. Do not invent values — always source them from the GDD.
- If a gameplay decision is not covered by the GDD, ask before proceeding. Do not make assumptions about game design intent.
- The GDD is the authoritative source of truth for what the game is and how it works.
