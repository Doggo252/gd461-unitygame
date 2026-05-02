# Game Design Document: Tank Royale

## 1. Game Overview

**Project Name:** Tank Royale

**Genre:** Real-Time Strategy (RTS) / Card Battler

**Platforms:** PC (Desktop) and Mobile, local multiplayer support

**Target Audience:** Casual and mid-core players who enjoy fast-paced PvP strategy with stylized combined-arms gameplay.

**Core Concept:** Players assemble an 8-card deck from a single shared card pool of tanks, infantry, aircraft, and support cards. During a fast-paced 3-minute match, players spend a regenerating resource ("Command Points") to deploy units onto a dual-front battlefield. Units act autonomously, advancing toward the enemy's Forward Operating Bases (FOBs) and ultimately their Command HQ. The game ends with the destruction of an enemy HQ or by holding more FOBs when the timer runs out.

---

## 2. Core Gameplay Mechanics

### The Battlefield

- **Orientation:** Landscape. **Player 1 (blue)** deploys from the **left (−X) side**; **Player 2 / AI (red)** deploys from the **right (+X) side**. The two lanes run along the **Z axis** (north and south).
- **Layout:** A symmetrical top-down war map split into two halves (Friendly Half / Enemy Half) separated by a contested no-man's-land at X = 0.
- **Terrain:** Roads (faster movement), open ground (standard), rubble/craters (reduced speed). Terrain is fixed per map.
- **River:** A river channel runs down the **centre line (X = 0)** from map edge to map edge. It is **impassable except at the two bridges** (one per lane at Z = ±12). The bridges are the only crossing points and create natural chokepoints.
- **Bridges:** Two wooden bridges crossing the river, each 5 units wide — wide enough for one tank at a time. Tactically significant: a single heavy tank can hold a bridge against multiple light tanks.
- **Fronts:** Two "fronts" (north lane Z ≈ +12, south lane Z ≈ −12) cross no-man's-land via the bridges, creating two distinct avenues of attack.
- **Structures:** Each player commands 3 structures:
  - **2 Forward Operating Bases (FOBs):** One per front. They have auto-firing defensive weapons (crew-served guns, AA batteries). These are military positions, not fantasy towers.
  - **1 Command HQ:** Placed behind the FOBs at ±25 X. It is hardened and fully activates its perimeter defenses only after a FOB falls or takes direct fire.
- **Win Conditions:**
  - Destroying the enemy Command HQ: immediate victory.
  - Timer expiry: the player who controls (has destroyed) more enemy FOBs wins.
  - Tiebreaker: total enemy unit casualties.

### Resource System (Command Points)

- **Generation:** Command Points (CP) regenerate passively at a rate of 1 CP per 2.8 seconds.
- **Surge Phase:** In the final 60 seconds, CP generation doubles, forcing decisive action.
- **Capacity:** CP bar caps at 10. When full, generation pauses — spend CP to keep the pressure on.
- **Usage:** Deploying a card costs its listed CP value (ranging from 2 to 9).

### AI and Pathfinding

- Once deployed, units act autonomously — the player has no direct control.
- **Pathfinding:** All ground units use Unity NavMesh for obstacle-aware navigation. Rocks, trees, and compound walls are baked as impassable; tanks are dynamic obstacles handled by NavMesh local avoidance (RVO).
- **Targeting Priority:** Tanks engage enemy tanks first. Only when all enemy tanks are eliminated do they advance on enemy FOBs (nearest first) and then the Command HQ.
- **Anti-Air Awareness:** Aircraft can be targeted and shot down by units or FOBs with the **AA Capable** keyword.

#### Four-State AI Loop (implemented)

Each tank runs a four-state state machine every Update frame:

| State | Behaviour |
|---|---|
| **Search** | Polls every 0.5 s for the nearest visible enemy tank within detection cone + line-of-sight. If no tanks visible, searches for nearest enemy FOB/HQ (objectives always globally known — no LOS required). Transitions → Priority. |
| **Priority** | Re-evaluates globally. Rule 1: any visible enemy tank → target it, roll approach style, go to Pathfind. Rule 2: no tanks anywhere → target nearest enemy objective, go to Pathfind. |
| **Pathfind** | Two-phase navigation for unit targets. **Phase 1 (Staging):** navigates to a staging position anchored to the *target's own facing direction* — WideFlank stages at ~131° off the target's forward (rear quarter); ShallowFlank stages at 90° (side); Direct skips staging. **Phase 2:** once at the staging position, closes straight in to attack range. For objectives: navigates to the closest NavMesh-reachable point near the structure. Transitions → Attack when within weapon RNG. |
| **Attack** | Stops, faces target, fires every `1/SPD` seconds. For unit targets: validates detection cone each tick (drop lock if target exits cone AND beyond `RNG × 1.5`). If target backs outside `RNG × 1.2`, returns to Pathfind. If target dies or is destroyed, immediately returns to Search (scan timer reset to 0 for instant response). |

#### Line-of-Sight Detection

Unit-vs-unit detection requires a clear line of sight through cover. A ray is cast from the detecting tank to the target; if it hits any solid collider that is not a tank or objective structure, detection fails. This means:

- **Rocks and trees block detection** — a fast flanker can use cover to approach a heavy tank undetected.
- **Walls block detection** — tanks inside compounds are invisible to tanks outside until line-of-sight clears.
- **Objectives are exempt** — FOBs and HQs are always globally visible to all surviving tanks (their positions are fixed and known).

The detection cone (§2.1) is applied first; LOS is only checked for targets already within the forward arc.

### 2.1 AI Tactical Profiles

Each tank type has a **tactical personality** that governs how it approaches an enemy. Whenever a unit acquires a new target, it rolls a randomized **Approach Style** from three options weighted per tank type. The side (left vs right) is also randomized to prevent mirrored, predictable patterns.

#### Approach Styles

| Style | Description |
|---|---|
| **Direct** | Charges straight to just inside attack range. Front-arc armour exchange. |
| **Shallow Flank** | Stages at 90° off the *target's own* right or left, then closes in. Attacks from the side arc (ARM ×0.40, ATK ×1.5). |
| **Wide Flank** | Stages at ~131° off the *target's own* forward (rear quarter), routing around the target via NavMesh. Attacks from rear arc (ARM ×0.15, ATK ×2.5). The staging position is computed from the **target's facing**, not the attacker's approach vector, so it stays anchored to the target's rear regardless of relative movement. |

> **RocketArtillery** (RocketShip) always holds position at maximum range regardless of roll — flanking provides no benefit for indirect fire.

#### Detection Cone

Units with a `detectionAngle` less than 360° can only scan for and track enemies within a forward-facing cone. Enemies that move outside the cone are lost as targets (unless within close-combat range ≤ 1.5× RNG, where contact is maintained regardless).

While engaged, tanks slowly rotate their facing toward the locked target. A fast flanker can outpace a slow-rotating heavy tank and exit its detection cone, causing the heavy to lose the lock and stand idle while the flanker approaches from the side or rear.

| Tank | Detection Angle | Turn Speed | Notes |
|---|---|---|---|
| **Heavy** | 110° | 0.9 | Tight cone + very slow rotation — light flankers can out-rotate it |
| **Crawler** | 360° | 0.8 | Slowest rotation; Fortress bonus rewards stopping anyway |
| **Monster** | 360° | 1.0 | Ponderous |
| **MegaBall** | 360° | 1.2 | Heavy charger |
| **Original** | 360° | 3.0 | Baseline |
| **Alternative** | 360° | 2.8 | — |
| **Droid** | 360° | 3.0 | — |
| **Shark** | 360° | 3.5 | — |
| **Spike** | 360° | 3.2 | — |
| **RocketShip** | 360° | 2.5 | — |
| **UFO** | 360° | 4.0 | — |
| **Light** | 360° | 5.0 | Fast flanker; out-rotates heavy tracking |
| **UTV** | 360° | 5.5 | Scout; quickest rotation |

Turn Speed is a slerp factor (degrees/frame-weighted). Higher = faster rotation toward a locked target during Attack state.

#### Per-Tank Approach Weights

Weights are relative probabilities; the engine normalizes them. A weight of 0 means the style never occurs.

| Tank | Direct | Shallow Flank | Wide Flank | Personality |
|---|---|---|---|---|
| **Original** | 0.50 | 0.35 | 0.15 | Balanced all-rounder; mostly charges with occasional probing flanks |
| **Alternative** | 0.50 | 0.35 | 0.15 | Disciplined; high PEN rewards direct engagements |
| **Light** | 0.10 | 0.45 | 0.45 | Erratic flanker; almost never charges straight due to low ARM |
| **Heavy** | 0.75 | 0.20 | 0.05 | Front-line brawler; ARM and HP reward trading shots head-on |
| **Crawler** | 0.90 | 0.10 | 0.00 | Ponderous advance then stops for Fortress bonus; never wide-flanks |
| **Monster** | 0.80 | 0.15 | 0.05 | Devastating frontal assault; ATK too high to waste on side approaches |
| **Spike** | 0.15 | 0.50 | 0.35 | Armor Piercer; prefers side/rear arcs where ArmorPierceIgnore stacks even further |
| **Shark** | 0.70 | 0.25 | 0.05 | Aggressive; charges to join allies already engaging a target for the ATK bonus |
| **Droid** | 0.50 | 0.35 | 0.15 | Methodical; self-repair means it can absorb hits from any angle |
| **UTV** | 0.10 | 0.35 | 0.55 | Scout; exploits mobility with wide sweeping routes |
| **MegaBall** | 0.75 | 0.20 | 0.05 | Rollout charger; designed to breach head-on |
| **RocketShip** | — | — | — | Always hangs back at max range (RocketArtillery keyword overrides roll) |
| **UFO** | 0.20 | 0.30 | 0.50 | Hover; exploits terrain-ignoring movement with wide flanking arcs |

---

## 3. Control Schemes

The game supports two control schemes for PC and Mobile parity.

### Touchscreen Controls (Mobile/Tablet)

- **Drag-and-Drop:** Press and hold a card in the hand, drag it over the friendly deployment zone, and release to deploy.
- **Tap-to-Deploy:** Tap a card to select it, then tap a valid tile to deploy.
- **Deselect:** Tap elsewhere or tap another card to cancel.

### Keyboard and Mouse Controls (PC)

- **Drag-and-Drop:** Left-click-hold a card, drag to the battlefield, release to deploy.
- **Hotkeys:** Keys `1`, `2`, `3`, `4` select the corresponding card in hand.
  - Selected card attaches a ghost preview to the cursor.
  - Left-click deploys; Right-click cancels.

---

## 4. Card Types and Deployment

Players bring a deck of 8 cards into battle. The hand holds 4 cards at a time. When a card is played, the next card from the deck rotation enters the hand.

### Card Types

1. **Infantry:** Squads of foot soldiers. Cheap, numerous, effective against other infantry and light vehicles. Vulnerable to tanks and artillery. Deployed on friendly ground.
2. **Armor (Tanks):** The backbone of the deck. Durable, high-damage, effective against FOBs and other vehicles. Core card type of the game. Deployed on friendly ground.
3. **Aviation:** Aircraft and helicopter gunships. Very expensive, extremely powerful, fly over terrain and engage ground targets across the entire width of the map. Cannot be deployed on the ground — they enter from the map edge and execute a single attack run before exiting. Can be shot down by AA-capable units mid-run.
4. **Support:** Limited-use deployable assets — artillery strikes, smoke screens, supply drops, defensive emplacements. Deployed on the friendly half (emplacements) or called anywhere (strikes).

### Deployment Rules

- Infantry and Armor deploy on the player's half only.
- Destroying an enemy FOB expands the valid deployment zone into the enemy's corresponding front.
- Aviation cards launch from the player's edge and fly the full map length; they cannot be dropped at a specific point.
- Support Strikes can be called on any map coordinate; Support Emplacements deploy on the friendly half only.

---

## 5. Damage Calculation System

All combat values use the following formulae. The core addition over a generic system is the **Armor / Penetration** mechanic, which creates rock-paper-scissors dynamics between unit classes.

### Core Stats

| Stat  | Description                                 |
| ----- | ------------------------------------------- |
| `HP`  | Hit Points                                  |
| `ATK` | Base damage per shot/attack                 |
| `ARM` | Armor rating — resistance to kinetic damage |
| `PEN` | Penetration — ability to defeat armor       |
| `SPD` | Attack speed (shots per second)             |
| `MOV` | Movement speed (tiles per second)           |
| `RNG` | Attack range (tiles)                        |

---

### 5.0 Directional Armor (Arc System)

All tank damage is modified by which arc the attacker fires from, relative to the **defender's** facing direction.

| Arc | Angle from Defender's Forward | ARM Multiplier | ATK Multiplier |
|---|---|---|---|
| **Front** | < 45° | ×1.00 (full armor) | ×1.0 |
| **Sides** | 45°–135° | ×0.40 (60% stripped) | ×1.5 |
| **Rear** | > 135° | ×0.15 (85% stripped) | ×2.5 |

Both multipliers apply simultaneously. A rear hit against a Heavy tank (ARM 90):

```
Effective_ARM = 90 × 0.15 = 13.5
PEN_Ratio     = Light_PEN(80) / 13.5 = 5.9 → clamped to 1.0
Base_Damage   = Light_ATK(200) × 1.0 = 200
Final_Damage  = 200 × 2.5 (rear ATK multiplier) = 500
```

vs a frontal hit:

```
Effective_ARM = 90 × 1.0 = 90
PEN_Ratio     = 80 / 90 = 0.89
Base_Damage   = 200 × 0.89 = 178
Final_Damage  = 178 × 1.0 = 178
```

This makes flanking and the tactical approach styles (GDD §2.1) mechanically meaningful.

---

### 5.1 Armor Penetration — Core Damage Formula

When a unit fires, its shell's penetration is compared to the target's armor. Full damage is dealt only on a clean penetration.

```
Pen_Ratio = PEN_attacker / ARM_target

If Pen_Ratio >= 1.0:
    Effective_Damage = ATK                         -- Full penetration
Else:
    Effective_Damage = ATK × Pen_Ratio             -- Partial / ricochet
```

> Minimum Effective_Damage is always 10% of ATK regardless of Pen_Ratio — even a glancing blow does superficial damage.

```
Effective_Damage = max(ATK × 0.1, ATK × clamp(Pen_Ratio, 0.1, 1.0))
```

> Example: Warden (ATK 350, PEN 80) vs Enforcer (ARM 65).
> Pen_Ratio = 80/65 = 1.23 → Full penetration → Effective_Damage = 350.

> Example: Scout (ATK 120, PEN 30) vs Warden (ARM 80).
> Pen_Ratio = 30/80 = 0.375 → Partial → Effective_Damage = 120 × 0.375 = 45.

---

### 5.2 Damage Per Second (DPS)

```
DPS = Effective_Damage × SPD
```

---

### 5.3 Time to Kill (TTK)

```
TTK = HP_target / DPS
```

Used internally by the AI threat-prioritization system. The unit with the lowest TTK against the firing unit is the preferred target.

---

### 5.4 Artillery / AOE Strike Damage

Artillery and strike cards deal HE (High Explosive) damage, which uses a reduced ARM modifier because HE damages through overpressure, not penetration.

```
HE_ARM_Factor = ARM_target × 0.35          -- HE only partially defeated by armor
HE_Pen_Ratio  = PEN_strike / HE_ARM_Factor
Effective_HE  = max(ATK × 0.3, ATK × clamp(HE_Pen_Ratio, 0.3, 1.0))
```

For **Falloff** strikes (blast radius), damage scales from full at the epicenter to 40% at the edge:

```
Falloff_Damage = Effective_HE × max(0.4, 1 − (Distance / Radius) × 0.6)
```

---

### 5.5 Aircraft Attack Run

Aircraft fire on targets along their flight path. They carry HE bombs and/or autocannon bursts.

- **Bombs:** Use §5.4 HE formula with a large Falloff radius.
- **Autocannon (e.g., Heavy Gunship autocannon):** Uses §5.1 with a high dedicated PEN value that simulates armor-piercing rounds.

```
-- Autocannon (depleted uranium / HEAT rounds)
Autocannon_Damage = ATK × clamp(PEN_gun / ARM_target, 0.3, 1.0)
```

Aircraft can be **shot down** mid-run. If an AA-capable unit or FOB deals enough damage before the aircraft completes its run, it is destroyed and its remaining attacks are cancelled.

```
Aircraft_HP_Remaining -= AA_Damage_per_tick × Time_in_Range
If Aircraft_HP_Remaining <= 0: Attack run cancelled
```

---

### 5.6 FOB Targeting & Defense Fire

FOBs fire at the nearest enemy unit in range. FOBs have their own ARM rating (hardened position) but their defensive weapons deal HE-type damage (§5.4).

```
FOB_DPS = FOB_ATK × FOB_SPD × clamp(FOB_PEN / Target_ARM × 0.35, 0.3, 1.0)
```

---

### 5.7 Status Effects

| Effect          | ATK Modifier | MOV Modifier | Duration    | Source                         |
| --------------- | ------------ | ------------ | ----------- | ------------------------------ |
| **Suppressed**  | `× 0`        | `× 0.3`      | 4 s         | Smoke Screen, nearby explosion |
| **Burning**     | +Burn DoT    | `× 0.5`      | 6 s         | Napalm Strike, Flamethrower    |
| **Bogged Down** | —            | `× 0.4`      | Until clear | Minefield, Mud Terrain         |

```
Buffed_Effective_Damage = Effective_Damage × ATK_Modifier
```

Suppression does not deal damage but prevents the target from returning fire, making it ideal for advancing infantry or covering a tank push.

---

### 5.8 Burning / Napalm Damage-over-Time

Burn damage is applied in ticks every 0.5 s with linear decay:

```
Burn_Tick(n) = BurnBase × (1 − (n / TotalTicks) × 0.5)
```

Default: `TotalTicks = 12` (6 seconds), `BurnBase = 40`.
Total burn damage ≈ `BurnBase × TotalTicks × 0.75` = `360` per application.
Burn ignores ARM entirely (thermal damage).

---

### 5.9 Strafing Ramp (Heavy Gunship Autocannon)

When an aircraft with the **Strafing** keyword locks onto a column of targets, each additional target hit in the same run receives a stacking damage bonus:

```
Strafe_Damage(n) = Base_ATK × min(2.0, 1.0 + n × 0.25)
```

Where `n` = number of targets already hit in the current run. Bonus resets at the start of each deployment.

---

### 5.10 Infantry AT Weapons (Anti-Tank Keyword)

Infantry squads with the **AT** keyword carry dedicated anti-tank weapons (bazookas, RPGs, ATGMs). Their AT shot uses a separate high-PEN profile:

```
AT_Effective_Damage = AT_ATK × clamp(AT_PEN / ARM_target, 0.2, 1.0)
```

Infantry AT shots fire at a lower SPD than their normal rifle fire. Only one AT shot fires per squad engagement.

---

## 6. Game Flow

### 6.1 Match Flow

```mermaid
flowchart TD
    A([Match Start]) --> B[Build Deck from Shared Card Pool]
    B --> D[Both Players Enter Deployment Phase]
    D --> E((CP Regenerates\n+1 per 2.8 s))
    E --> F{Card in Hand?}
    F -->|Yes| G{Card Type?}
    F -->|No| E
    G -->|Infantry / Armor| H[Deploy on Friendly Half]
    G -->|Aviation| I[Launch from Map Edge\nAttack Run Begins]
    G -->|Support Strike| J[Call Strike on Any Coordinate]
    G -->|Support Emplacement| K[Deploy on Friendly Half]
    H --> L[Unit Advances via Pathfinding]
    I --> M[Aircraft Flies Across Map\nStrikes Targets on Path]
    J --> N[AOE Damage Applied at Target]
    L --> O{Enemy in Aggro Range?}
    O -->|Yes| P[Combat Resolution Loop]
    O -->|No| Q{FOB in Range?}
    Q -->|Yes| R[Attack FOB]
    Q -->|No| L
    R --> S{FOB Destroyed?}
    S -->|Yes| T[Deployment Zone Expands\nHQ Defenses Activate]
    S -->|No| R
    T --> U{HQ in Range?}
    U -->|Yes| V[Attack Command HQ]
    U -->|No| L
    V --> W{HQ Destroyed?}
    W -->|Yes| X([Immediate Victory])
    W -->|No| V
    P --> Y{Unit Destroyed?}
    Y -->|Yes| Z[Remove Unit from Field]
    Y -->|No| P
    Z --> AA{Timer Expired?}
    AA -->|No| E
    AA -->|Yes| AB[Count FOBs Captured]
    AB --> AC{Player Holds More FOBs?}
    AC -->|Yes| X
    AC -->|No| AD([Defeat or Draw])
```

### 6.2 Combat Resolution

```mermaid
flowchart TD
    A([Attack Initiated]) --> B[Read Attacker: ATK, PEN, Type]
    B --> C[Read Target: ARM, HP, Status]
    C --> D{Attack Type?}
    D -->|Kinetic Shell| E[Pen_Ratio = PEN / ARM]
    D -->|HE / Artillery| F[HE_ARM_Factor = ARM × 0.35\nPen_Ratio = PEN / HE_ARM_Factor]
    D -->|AT Weapon| G[Use AT_PEN profile\nPen_Ratio = AT_PEN / ARM]
    E --> H[Effective_Damage = ATK × clamp Pen_Ratio 0.1 to 1.0]
    F --> H
    G --> H
    H --> I{Status Effect on Target?}
    I -->|Suppressed| J[ATK_Modifier = 0\nTarget cannot fire back]
    I -->|Burning| K[Add Burn_Tick each 0.5 s\nSee § 5.8]
    I -->|None| L[ATK_Modifier = 1.0]
    J --> M[Apply Modifier:\nFinal_Damage = Effective_Damage × ATK_Modifier]
    K --> M
    L --> M
    M --> N[HP_target -= Final_Damage]
    N --> O{HP_target <= 0?}
    O -->|Yes| P([Unit Destroyed — Remove from Field])
    O -->|No| Q([Unit Survives — Continue Combat Loop])
```

---

## 7. Card Roster

Players build an 8-card deck from the single shared card pool below. All stat values are Level 1 baseline.

### Stat Key

| Column | Meaning                              |
| ------ | ------------------------------------ |
| CP     | Command Points to deploy             |
| HP     | Hit points                           |
| ATK    | Base damage per shot (kinetic or HE) |
| ARM    | Armor rating                         |
| PEN    | Penetration value                    |
| SPD    | Shots/sec                            |
| MOV    | Tiles/sec (0 = static)               |
| RNG    | Attack range in tiles                |
| Cnt    | Units deployed per card              |

---

## Armor

Each armor card corresponds to a specific tank model from the asset pack. Stats reflect each model's visual design.

| #  | Name              | Model File                    | CP | HP   | ATK | ARM | PEN | SPD | MOV | RNG | Keywords                                                                  |
|----|-------------------|-------------------------------|----|------|-----|-----|-----|-----|-----|-----|---------------------------------------------------------------------------|
| 1  | **Original**      | Tank_Original_Model.fbx       | 4  | 1500 | 250 | 45  | 70  | 0.8 | 1.6 | 5   | —                                                                         |
| 2  | **Alternative**   | Tank_Alternative_Model.fbx    | 5  | 1900 | 310 | 65  | 90  | 0.7 | 1.5 | 6   | Balanced; high PEN for cost                                               |
| 3  | **Light**         | Tank_Light_Model.fbx          | 3  | 900  | 200 | 20  | 80  | 1.0 | 2.8 | 5   | Fast Flanker: highest MOV; high PEN vs low ARM                            |
| 4  | **Heavy**         | Tank_Heavy_Model.fbx          | 6  | 2500 | 380 | 90  | 85  | 0.6 | 1.1 | 6   | Heavy: strong ARM; slow                                                   |
| 5  | **Crawler**       | Tank_Crawler_Model.FBX        | 7  | 3000 | 350 | 110 | 80  | 0.5 | 0.8 | 6   | Fortress: ARM counts as +20 while stationary                              |
| 6  | **Monster**       | Tank_Monster_Model.FBX        | 8  | 3500 | 450 | 100 | 90  | 0.4 | 0.9 | 6   | Devastating: single massive shot; minimum 30% damage floor                |
| 7  | **Spike**         | Tank_Spike_Model.FBX          | 4  | 1100 | 290 | 25  | 125 | 0.7 | 1.3 | 7   | Armor Piercer: ignores up to 30 ARM on every shot                         |
| 8  | **Shark**         | Tank_Shark_Model.FBX          | 4  | 1300 | 240 | 35  | 70  | 0.9 | 2.2 | 5   | Aggressive: +15% ATK when attacking a unit already engaged by an ally     |
| 9  | **Droid**         | Tank_Droid_Model.FBX          | 5  | 1700 | 290 | 55  | 100 | 0.9 | 1.7 | 5   | Self-Repair: regenerates 50 HP every 5 s                                  |
| 10 | **UTV**           | Tank_UTV_Model.FBX            | 3  | 800  | 160 | 15  | 60  | 1.2 | 3.0 | 4   | Scout: reveals all enemy units within RNG 6 on deploy                     |
| 11 | **MegaBall**      | Tank_MegaBall_Model.FBX       | 6  | 2800 | 310 | 80  | 75  | 0.7 | 1.4 | 5   | Rollout: on deploy, charges 3 tiles forward dealing 150 HE to first enemy |
| 12 | **RocketShip**    | Tank_RocketShip_Model.FBX     | 5  | 1400 | 200 | 30  | 50  | 0.6 | 1.4 | 8   | Rocket Artillery: attacks use HE formula; arc-fires over terrain          |
| 13 | **UFO**           | Tank_UFO_Model.fbx            | 6  | 2000 | 340 | 50  | 110 | 0.8 | 2.0 | 6   | Hover: ignores terrain movement penalties (rubble/craters)                |

---

## Infantry

| #  | Name               | CP | HP  | ATK | ARM | PEN | SPD | MOV | RNG | Cnt | Keywords                                                |
|----|--------------------|----|-----|-----|-----|-----|-----|-----|-----|-----|---------------------------------------------------------|
| 14 | **Rifle Squad**    | 2  | 120 | 55  | 0   | 5   | 1.2 | 2.0 | 3   | 6   | AT (AT_ATK 180, AT_PEN 70)                              |
| 15 | **Assault Team**   | 2  | 110 | 65  | 0   | 5   | 1.3 | 2.2 | 3   | 6   | AT (AT_ATK 220, AT_PEN 90)                              |
| 16 | **AT Squad**       | 3  | 130 | 45  | 0   | 5   | 0.9 | 1.8 | 3   | 4   | AT (AT_ATK 340, AT_PEN 130, ATGM); specialist AT unit   |
| 17 | **Commando Unit**  | 3  | 140 | 70  | 0   | 5   | 1.2 | 2.5 | 4   | 4   | Stealth: ignored by FOB until within RNG 2              |
| 18 | **Heavy Weapons**  | 4  | 160 | 90  | 5   | 5   | 0.8 | 1.5 | 5   | 3   | AT (AT_ATK 300, AT_PEN 110); Elite: immune to Suppression |

---

## Aviation

| #  | Name                    | CP | HP  | ATK | PEN | RNG | Keywords                                                              |
|----|-------------------------|----|-----|-----|-----|-----|-----------------------------------------------------------------------|
| 19 | **Fighter-Bomber**      | 6  | 600 | 280 | 50  | 4   | Strafing (§5.9); Bomb x2 (HE ATK 320, Radius 2.0); AA Capable        |
| 20 | **Ground Attack**       | 5  | 520 | 250 | 45  | 4   | Strafing; Bomb x1 (HE ATK 360, Radius 1.5)                           |
| 21 | **Tank Buster**         | 6  | 400 | 320 | 110 | 4   | Autocannon vs armor; high PEN; slow, lower HP                         |
| 22 | **Attack Helicopter**   | 5  | 550 | 200 | 60  | 4   | Helicopter: hovers 4 s, attacks before exiting; Rockets + minigun    |
| 23 | **Assault Helicopter**  | 6  | 700 | 280 | 80  | 5   | Helicopter; transports 1 Infantry squad (deployed on exit); Rockets  |
| 24 | **Stealth Jet**         | 8  | 650 | 420 | 160 | 6   | Stealth: FOB AA does not fire until within RNG 3; Bomb x2            |

---

### Support Cards (Cross-Era)

Support cards are usable in any era. They do not count against the troop/armor/aviation composition but occupy one of the 8 deck slots.

#### Artillery Strikes

| #   | Name                    | CP  | ATK (HE)       | Radius | Duration | Notes                                                                                            |
| --- | ----------------------- | --- | -------------- | ------ | -------- | ------------------------------------------------------------------------------------------------ |
| 70  | **Artillery Barrage**   | 5   | 380            | 3.0    | Instant  | Falloff; 3-round salvo with 0.8 s between impacts                                                |
| 71  | **Rocket Salvo (MLRS)** | 6   | 280            | 2.0    | Instant  | 6 rockets land in a line; good vs columns of units                                               |
| 72  | **Napalm Strike**       | 4   | 120            | 2.5    | 6 s Burn | Applies Burning (§5.7, §5.8) to all units in zone                                                |
| 73  | **Smoke Screen**        | 2   | 0              | 3.0    | 8 s      | Applies Suppressed (§5.7) to all enemy units in cloud                                            |
| 74  | **Supply Drop**         | 3   | −400 HP (heal) | 2.5    | Instant  | Heals all friendly units in radius; cannot over-heal                                             |
| 75  | **EMP Pulse**           | 4   | 0              | 2.5    | 3 s      | Cold War / Modern: stuns all vehicles (ARM-equipped units) in area; MOV and SPD × 0 for duration |

#### Deployable Emplacements

| #   | Name                           | CP  | HP   | ATK | ARM | PEN | SPD | RNG | Lifetime | Notes                                                                                         |
| --- | ------------------------------ | --- | ---- | --- | --- | --- | --- | --- | -------- | --------------------------------------------------------------------------------------------- |
| 76  | **Anti-Tank Gun (AT Gun)**     | 3   | 600  | 320 | 10  | 110 | 0.7 | 6   | 30 s     | Targets Armor only; stationary; crew-served                                                   |
| 77  | **Anti-Aircraft Battery (AA)** | 4   | 700  | 260 | 10  | 80  | 1.2 | 7   | 35 s     | Targets Aviation only; essential counter to air                                               |
| 78  | **Bunker**                     | 4   | 1000 | 80  | 20  | 20  | 1.0 | 4   | 40 s     | Spawns 3 Infantry every 10 s; tanky structure                                                 |
| 79  | **Minefield**                  | 2   | —    | 200 | —   | 50  | —   | —   | 60 s     | Placed tile; deals HE damage and applies Bogged Down on contact; single-use per tile          |
| 80  | **Fuel Depot**                 | 3   | 500  | —   | 5   | —   | —   | —   | 50 s     | Friendly units within 3 tiles get MOV +0.4; explodes for 300 HE AOE (Radius 2.5) if destroyed |

---

## 7.5 Structures

Each player's three structures are implemented as `ObjectiveTarget` components with `HealthComponent`. They are damageable but have no movement or AI of their own.

| Structure | Team | ARM | HP   | Notes |
|---|---|---|---|---|
| **FOB Left** | P1 / P2 | 30 | 5000 | Forward compound; walled, attacked after all enemy tanks are eliminated |
| **FOB Right** | P1 / P2 | 30 | 5000 | Mirror of FOB Left on opposite front |
| **Command HQ** | P1 / P2 | 50 | 8000 | Hardened HQ; destroying it wins the match |

Structure damage uses the kinetic formula (§5.1) with no directional arc modifier (structures don't rotate). The tank's full `PEN / ARM` ratio is applied:

```
Effective_Structural_Dmg = ATK × clamp(PEN / Structure_ARM, 0.1, 1.0)
```

---

## 8. User Interface (UI)

- **Top of Screen:** Enemy FOB health bars, enemy HQ health bar, match timer, enemy era/nation banner.
- **Middle:** The battlefield — isometric top-down war map with terrain features.
- **Bottom of Screen:**
  - **CP Bar:** Horizontal fill bar showing current Command Points (0–10).
  - **Current Hand:** 4 cards showing their CP cost, unit type icon, and era/nation flag.
  - **Next Card:** Small preview showing the upcoming card in deck rotation.
- **Kill Feed:** A scrolling side panel showing recent unit destructions (e.g., "Overlord destroyed Enforcer").

### 8.1 World-Space Structure Health Bars

Each FOB and Command HQ displays a world-space health bar directly above the structure (implemented as a `Canvas` in World Space render mode). The bar:

- Billboards toward the camera every frame so it is readable from any camera angle.
- Displays the structure name and current/maximum HP as text.
- Uses a **team-coloured border** (blue = P1, red = P2) for instant ownership identification.
- Fill colour transitions green → yellow → red as HP falls.
- Subscribes to `HealthComponent.OnHealthChanged` (event-driven per AGENTS.md §2) and updates only when damage is dealt.

---

## 9. Art & Audio Direction

- **Visual Style:** Stylized top-down 3D. Tank models come from the Unity Tanks asset pack (13 distinct meshes). Infantry uses Kenney asset packs. Aircraft uses the Generic Aircraft Models Free pack. Units are team-colored (red vs blue) to ensure readability.
- **Camera:** Orthographic, fixed top-down with a slight 80° pitch for depth cue. `orthographicSize = 19` covers the full 56-unit map width at 16:9 with margin. Far clip plane = 200. No perspective distortion — standard for this genre.
- **Audio:**
  - Distinct audio per unit type: tank engine growl on deployment, aircraft engine roar on attack run, infantry boot crunch and shouting.
  - Warning sirens when a FOB is below 25% HP.
  - CP-full audio cue (radio burst: "Command Post at capacity").

---

## 10. Implementation State (as of session 2)

### Implemented and working
- Full damage formula (§5.0–5.1): directional ARM reduction + ATK multiplier applied per shot
- Four-state AI loop (§2) with two-phase flanking (§2.1)
- Per-tank turn speed (§2.1 detection table) enforcing heavy-vs-light skill dynamics
- NavMesh pathfinding with LOS raycasts and detection cone enforcement
- World-space objective health bars (`ObjectiveHealthBar`) on all 6 structures
- Orthographic camera (§9)
- Landscape battlefield: P1 left, P2 right, river at X=0, two bridges at Z=±12
- All 13 tank prefabs in `Assets/Prefabs/` — drag into scene, set **Team** (0=blue/P1, 1=red/P2)

### Prefabs (`Assets/Prefabs/`)
| Prefab | Tank Type | Key Keyword |
|---|---|---|
| `Tank_Original.prefab` | Original | — |
| `Tank_Alternative.prefab` | Alternative | — |
| `Tank_Light.prefab` | Light | FastFlanker |
| `Tank_Heavy.prefab` | Heavy | — |
| `Tank_Crawler.prefab` | Crawler | Fortress |
| `Tank_Monster.prefab` | Monster | Devastating |
| `Tank_Spike.prefab` | Spike | ArmorPiercer |
| `Tank_Shark.prefab` | Shark | Aggressive |
| `Tank_Droid.prefab` | Droid | SelfRepair |
| `Tank_UTV.prefab` | UTV | Scout |
| `Tank_MegaBall.prefab` | MegaBall | Rollout |
| `Tank_RocketShip.prefab` | RocketShip | RocketArtillery |
| `Tank_UFO.prefab` | UFO | Hover |

### Not yet implemented
- Card system (hand, deck rotation, CP cost, deployment zones)
- CP regeneration and Surge Phase
- FOB/HQ auto-fire defensive weapons
- Infantry and Aviation card types
- Support cards (artillery strikes, emplacements)
- Win condition evaluation (timer, FOB count)
- Audio
- Match flow / game state machine
