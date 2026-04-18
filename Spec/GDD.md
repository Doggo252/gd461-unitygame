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

- **Layout:** A symmetrical top-down war map split into two halves (Friendly Half / Enemy Half) separated by a contested no-man's-land.
- **Terrain:** Roads (faster movement), open ground (standard), rubble/craters (reduced speed). Terrain is fixed per map.
- **Fronts:** Two "fronts" (left and right) cross no-man's-land, creating two distinct avenues of attack — equivalent to lanes.
- **Structures:** Each player commands 3 structures:
  - **2 Forward Operating Bases (FOBs):** One per front. They have auto-firing defensive weapons (crew-served guns, AA batteries). These are military positions, not fantasy towers.
  - **1 Command HQ:** Placed behind the FOBs. It is hardened and fully activates its perimeter defenses only after a FOB falls or takes direct fire.
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
- **Pathfinding:** Units calculate the shortest path to the nearest enemy objective (FOB, then HQ) along valid terrain, preferring roads.
- **Targeting Priority:** Tanks and infantry engage enemy units within their aggro radius. Aircraft fly a fixed attack-run path across the map and cannot be redirected after launch.
- **Anti-Air Awareness:** Aircraft can be targeted and shot down by units or FOBs with the **AA Capable** keyword.

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

## 8. User Interface (UI)

- **Top of Screen:** Enemy FOB health bars, enemy HQ health bar, match timer, enemy era/nation banner.
- **Middle:** The battlefield — isometric top-down war map with terrain features.
- **Bottom of Screen:**
  - **CP Bar:** Horizontal fill bar showing current Command Points (0–10).
  - **Current Hand:** 4 cards showing their CP cost, unit type icon, and era/nation flag.
  - **Next Card:** Small preview showing the upcoming card in deck rotation.
- **Kill Feed:** A scrolling side panel showing recent unit destructions (e.g., "Overlord destroyed Enforcer").

---

## 9. Art & Audio Direction

- **Visual Style:** Stylized top-down 3D. Tank models come from the Unity Tanks asset pack (13 distinct meshes). Infantry uses Kenney asset packs. Aircraft uses the Generic Aircraft Models Free pack. Units are team-colored (red vs blue) to ensure readability.
- **Camera:** Fixed isometric/top-down perspective.
- **Audio:**
  - Distinct audio per unit type: tank engine growl on deployment, aircraft engine roar on attack run, infantry boot crunch and shouting.
  - Warning sirens when a FOB is below 25% HP.
  - CP-full audio cue (radio burst: "Command Post at capacity").
