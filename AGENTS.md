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

## 5. Game Design Document

- The GDD is located at `Spec/GDD.md`. **Read it before writing any gameplay code.**
- All unit stats, card definitions, era configurations, damage formulas, and win conditions are defined there. Do not invent values — always source them from the GDD.
- If a gameplay decision is not covered by the GDD, ask before proceeding. Do not make assumptions about game design intent.
- The GDD is the authoritative source of truth for what the game is and how it works.
