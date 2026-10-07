# COIKA — Game Design Document

> Version 1.0 · Unity 6000.6.3f1 · URP 2D · Pixel art · Mobile portrait · Premium (no IAP / no ads)
>
> This document is written to be implementable without further design input. Every number marked **[TUNE]** is a starting value expected to be adjusted during playtesting; everything else is a firm decision. Items I assumed on your behalf are collected in §20 — change them freely.

---

## 1. Vision

**One-liner:** Drop cosmic bodies into a jar, merge identical pairs into bigger ones, and reach the Black Hole before the jar overflows.

**Pillars**
1. **Readable physics toy** — the player can predict what will happen. No hidden randomness in physics.
2. **One-thumb, 30-second sessions** — drag, release, done. Never more than one input type.
3. **Juicy pixel feedback** — every merge feels good (squash, particles, sound, screen shake scaling with tier).
4. **Fair tension** — losses should feel like the player's mistake, never the game's.

**Reference:** *Suika Game* (Aladdin X, 2021). We replicate its core loop and differentiate through theme, pixel-art presentation, and a small set of optional modes (§12).

**Target audience:** casual players, 10+. Sessions 2–10 min.

**Platform:** Android + iOS, portrait only. (Editor modules already installed.)

---

## 2. Core Loop

```
 ┌─► Next piece appears at top ──► Player slides horizontally ──► Release
 │                                                                  │
 │                                                          Piece falls, collides
 │                                                                  │
 │                                              Same tier touching? ─► MERGE → score, spawn next tier
 │                                                                  │
 └──────────── Overflow check (piece above line too long?) ◄────────┘
                       │ yes → GAME OVER → score screen → retry
```

Session goal: maximize score; stretch goal: create the top-tier body.

---

## 3. Rules

### 3.1 The Jar
- A static open-top container. Interior **10 × 12.5 world units** (160 × 200 px at PPU 16).
- Two side walls and a floor, built from `BoxCollider2D` / `EdgeCollider2D`. Walls are **1 unit thick** (16 px, for the collider and for the art) and extend above the **Drop Line** so pieces never escape sideways. With the walls, the jar is **12 units (192 px) wide**, exactly the width of the reference frame (§8), so both walls are fully visible.
- **Drop Line** (spawn height): 1.5 units above the jar's top rim.
- **Danger Line**: a horizontal line at the jar's top rim (y = jar floor + 12.5). Rendered as a dashed pixel line, hidden until a piece comes within 2 units of it, then pulses red.

### 3.2 Pieces (11 tiers)
Placeholder theme: cosmic bodies. Art can be re-skinned without code changes (§14).

| Tier | Name | Diameter (px) | Diameter (units) | Radius (units) | Merge score |
|---|---|---|---|---|---|
| 0 | Dust | 12 | 0.75 | 0.375 | 1 |
| 1 | Pebble | 16 | 1.00 | 0.500 | 3 |
| 2 | Asteroid | 21 | 1.31 | 0.656 | 6 |
| 3 | Moon | 27 | 1.69 | 0.844 | 10 |
| 4 | Dwarf Planet | 34 | 2.13 | 1.063 | 15 |
| 5 | Rocky Planet | 42 | 2.63 | 1.313 | 21 |
| 6 | Ocean Planet | 51 | 3.19 | 1.594 | 28 |
| 7 | Gas Giant | 61 | 3.81 | 1.906 | 36 |
| 8 | Star | 72 | 4.50 | 2.250 | 45 |
| 9 | Neutron Star | 84 | 5.25 | 2.625 | 55 |
| 10 | Black Hole | 96 | 6.00 | 3.000 | 66 |

Pixel-art rule: sprite size in pixels **equals the diameter in px** above (PPU 16), so collider radius = sprite width / 2 / 16. Sprites are drawn on a canvas whose side is the diameter, circle touching the edges.

### 3.3 Spawning
- The player never chooses a tier. Only **tiers 0–4** can spawn.
- Weighted random **[TUNE]**: Dust 30 %, Pebble 28 %, Asteroid 20 %, Moon 14 %, Dwarf Planet 8 %.
- **Anti-streak:** the same tier cannot appear more than 3 times in a row (re-roll).
- **Next preview:** always show the *next* piece (top-right of HUD). The current piece is held at the Drop Line.
- First 3 pieces of a run are forced to tiers 0, 1, 0 so new players see an early merge.
- RNG uses a seeded `System.Random` stored in the run state (enables daily mode, §12, and replays).

### 3.4 Merging
1. Two pieces of the **same tier** that **touch** (collision contact) merge.
2. The merge spawns **one piece of tier+1** at the **midpoint** of the two, with the **average of their velocities** and zero angular velocity.
3. Score = merge score of the **new** tier (table above) × combo multiplier (§4).
4. **Determinism rule:** when A touches B, only the piece with the **lower `InstanceID`** performs the merge; the other is flagged `merged = true` and ignored. A piece can only participate in one merge per physics step.
5. **Chain reaction:** the newly spawned piece may immediately be touching another same-tier piece; it merges on the next physics step (not instantly) so the player sees the chain.
6. **Top tier:** two Black Holes that touch **both vanish** (implosion VFX), award a **Supernova bonus of 500 points** [TUNE], and free the space. This allows endless play.
7. A piece spawned by merge does **not** trigger the overflow timer for 1.0 s (grace).

### 3.5 Drop
- The held piece is kinematic, follows the pointer's X (clamped so the piece stays fully inside the jar walls).
- On release (touch up), it becomes dynamic and falls.
- **Drop cooldown:** 0.5 s [TUNE] after release before the next piece is attached and controllable (prevents spam, shows the previous piece falling). The next piece pops in during this time (spawn pop, §9).
- No dropping while the game is paused, in game over, or during the very first frame of a touch (ignore touches that begin on UI).

### 3.6 Game Over
- A piece is **overflowing** if its collider's top is above the Danger Line **and** it is **settled** (`velocity.magnitude < 0.2`) **and** it has existed for more than 1.0 s (grace, also applied to merge-spawned pieces).
- Each overflowing piece accumulates time; if **any** piece has been overflowing continuously for **2.0 s** [TUNE], the game ends.
- The danger line pulses red from 0 s to 2 s, with a countdown tick audio cue in the last second.
- If the overflowing piece leaves the zone (e.g. a merge shifts it), its timer resets to 0.
- On game over: freeze physics, play a "jar full" animation (pieces flash in order from top to bottom, 0.05 s each, awarding **no points**), then show the Game Over screen.

---

## 4. Scoring

| Event | Points |
|---|---|
| Merge producing tier N | Merge score of tier N (table §3.2) |
| Dropping a piece | Tier index of the dropped piece (0–4) |
| Supernova (2 Black Holes) | 500 |

**Combo:** merges happening within **1.0 s** of the previous merge increment a combo counter. Multiplier = `1 + 0.25 × (combo − 1)`, capped at ×3 [TUNE]. Combo resets after 1.0 s without a merge. Display the multiplier next to the score ("x1.5") with a punch animation.

Score is an `int`. Floor after multiplication.

**High score:** stored locally per mode (§13). A "NEW BEST!" banner shows on the Game Over screen if beaten.

---

## 5. Physics Specification

Use **Unity 2D Physics (Box2D)**. All pieces are `Rigidbody2D` (Dynamic) + `CircleCollider2D`.

| Setting | Value |
|---|---|
| Gravity (Physics2D.gravity) | (0, −20) [TUNE] — higher than real for a snappy feel |
| Fixed Timestep | 1/60 s (0.01667) |
| Velocity iterations / Position iterations | 8 / 3 (default) — raise to 10 / 4 if pieces tunnel or jitter |
| Rigidbody mass | `π·r²` (area-based) so large pieces push small ones |
| Linear Damping | 0.1 |
| Angular Damping | 0.3 |
| Collision detection | Continuous |
| Interpolate | Interpolate (needed for pixel-perfect smoothness) |
| Sleeping | Start Awake; allow sleep |
| Physics Material 2D | Friction 0.4, Bounciness 0.15 [TUNE] |
| Walls material | Friction 0.4, Bounciness 0 |
| Layers | `Piece`, `Wall`, `HeldPiece` (collides with nothing), `UI` |

- Layer collision matrix: `Piece` ↔ `Piece`, `Piece` ↔ `Wall` only.
- A held (not yet dropped) piece sits on the `HeldPiece` layer with a kinematic body and no collisions, then switches to `Piece` on release.
- Max pieces on screen realistically ≤ ~60; no pooling issues expected, but use a pool anyway (§15).

**Pixel-perfect note:** the Pixel Perfect Camera is already set up (`PixelSnapping`, PPU 16). Physics positions are floats, but the camera snaps visuals. Keep `Rigidbody2D.interpolation = Interpolate` and never move pieces in `FixedUpdate` via `transform`.

---

## 6. Controls

**Primary (touch):**
- Touch down anywhere in the play area → the held piece moves under the finger's X (smoothed with `Mathf.MoveTowards`, max 40 units/s so it never teleports).
- Drag → follow X (the piece offsets from the touch by the initial touch X to avoid occlusion; optional **Finger Offset** setting adds a Y-offset so the finger doesn't cover the piece).
- Release → drop.
- A vertical **guide line** (dotted, 1 px) falls from the held piece to the first thing it would hit (via `CircleCast`), with a faint ghost of the piece at the landing point. Toggleable in settings (default ON).

**Editor / desktop (for development):** mouse follows X, left click drops; `A/D` or arrows move, `Space` drops.

**Input stack:** Unity **Input System** (already in the project). Use `InputAction` with `Pointer` bindings for unified mouse/touch (`<Pointer>/position`, `<Pointer>/press`). Ignore presses that start over UI (`EventSystem.IsPointerOverGameObject`).

**Back button (Android):** opens Pause.

---

## 7. Game Flow & States

```
Boot → MainMenu ⇄ Settings
          │
          ▼
      Gameplay ⇄ Paused
          │
          ▼
      GameOver → (Retry → Gameplay | Menu → MainMenu)
```

`GameState` enum: `Boot, Menu, Playing, Paused, GameOver`. A single `GameManager` owns state; others subscribe to events (§15). Substate inside Playing: `Aiming` (piece held), `Dropping` (cooldown), `Resolving` (merge chain, optional — not blocking input).

**Scenes:** `Boot` (init services, load save, 0.5 s logo splash) → `Menu` → `Game`. Keep the three scenes small; additive loading is not required.

---

## 8. UI / UX

Reference portrait resolution: **192 × 320** (Pixel Perfect Camera, PPU 16): 160 px of jar interior plus two 16 px walls make the 192 px width, and the 320 px height holds the bottom strip (40 px), the floor (16 px), the interior (200 px), the gap up to the Drop Line (24 px) and the HUD margin (40 px). Set the Pixel Perfect Camera **Crop Frame to `None`** (not `Windowbox`, which adds black bars) and fill the area outside the reference frame with the background art/gradient (extra background sprite, 2× the reference frame in each direction).

Consequence of 192 not dividing evenly into common phone widths: the Pixel Perfect Camera uses an integer zoom, so a 1080×1920 screen runs at zoom 5 and a 720×1280 screen at zoom 3, showing a few extra units around the reference frame. The background covers that extra area; the HUD is a separate overlay and is not affected.

**Canvas:** `Screen Space – Overlay` (avoids upscale blur from `upscaleRT`), Canvas Scaler *Scale With Screen Size*, reference 1080 × 1920, match 0.5. UI is **uGUI + TextMeshPro** with a **pixel font** (TMP SDF set from a free pixel font, point-sampled; atlas generated with Padding 5). **No `OnGUI`.**

### 8.1 HUD (Gameplay)
| Element | Position | Notes |
|---|---|---|
| Score | Top-left | Rolling count-up animation (0.3 s) |
| Best score | Under score, small | |
| Combo multiplier | Under score, hidden at ×1 | Punch on increment |
| Next piece preview | Top-right in a framed box | Shows sprite of next tier |
| Pause button | Top-center-right | 44 px min touch target |
| **Evolution chart** | Bottom strip or side column | The 11 tiers in order as a circular chain icon strip; icon of highest tier reached this run is highlighted. Essential for new players to know merge order. |

Safe-area: respect `Screen.safeArea` (notches). Add the HUD under the safe area.

### 8.2 Main Menu
- Title "COIKA" with idle animation (pieces floating in the background).
- Buttons: **Play**, **Modes** (disabled until M3 unless you cut modes), **Settings**, **Quit** (Android only, hidden on iOS).
- Shows best score.

### 8.3 Pause
Resume · Restart (confirm) · Settings · Menu (confirm). Physics frozen via `Time.timeScale = 0` (UI uses unscaled time for animations). Menu is disabled until the Menu scene (M3) exists; Settings opens the Settings screen (§8.5) over the pause menu.

Entered by the HUD pause button, the Back button, or the app losing focus (`OnApplicationPause(true)` / `OnApplicationFocus(false)`, which also saves). Leaving Pause is only ever the player's choice: Resume, Back on the menu, or a confirmed Restart. Back closes the top panel first (a confirmation dialog counts as Cancel). Input is ignored for 0.15 s of game time after the resume, so the tap on Resume never drops a piece. Restart and Menu go through the reusable confirmation dialog, where Cancel is the default.

### 8.4 Game Over
Final score (count-up), Best score (+ "NEW BEST!"), highest tier reached (icon), pieces dropped, time played. Buttons: **Retry** (primary), **Menu**. Appears 1.2 s after the jar-full animation. Retry is accepted only after the view has finished appearing (about 0.5 s), and once.

### 8.5 Settings
Music volume, SFX volume, Haptics on/off, Guide line on/off, Reduce screen shake on/off, Finger offset on/off, Left-handed (affects the offset side only), Language (EN/ES at launch), Reset progress (confirm).

Implemented (#36): a portrait screen over the pause menu, one row per setting (sliders; toggles with an ON/OFF text so colour is never the only state). Changes apply and save at once, with no Apply button. The SFX slider plays a click on release. Language shows "English" as a disabled row until M3. Reset progress goes through the confirmation dialog and erases bests, totals and discovered tiers, keeps the settings, saves at once and refreshes the HUD best score. A reset in the middle of a run does not make that run a new best. Back button and Android Back close the screen. Controls are 140 reference px tall (about 9 mm), so the ten rows fit one panel.

---

## 9. Feedback / Juice

| Event | Visual | Audio | Haptic |
|---|---|---|---|
| Piece spawn at top | Scale 0 → 1 with overshoot (0.15 s) | soft "bloop" | — |
| Drop | Slight stretch downward | whoosh (pitch low) | light tap |
| Piece lands (impulse > threshold) | Squash-and-stretch 0.1 s, small dust puff (3 px sprites) | "thud" volume ∝ impulse, pitch ∝ 1/size | very light |
| Merge | Both pieces scale to 0 and a "flash" ring (white, 0.1 s); new piece pops 0→1.2→1.0 (0.2 s); 6–10 pixel particles in tier colour | Ascending pitch per tier: base "pop" pitched = `1 + 0.06·tier`; combo adds +1 semitone per combo step (cap 5) | medium (tier ≥ 7 heavy) |
| Merge tier ≥ 8 | + screen shake (amplitude 0.05 × (tier−7) units, 0.2 s) + brief slow-mo (timeScale 0.7 for 0.1 s) | Deeper layered sound | heavy |
| Supernova | White flash 0.15 s, big shockwave ring, shake | big boom | heavy |
| Danger line pulse | Red dashed line pulse at 4 Hz | tick tick tick | — |
| Game over | Pieces flash top → bottom | descending "womp" | long vibration 0.2 s |
| New best | Confetti of pixel stars | fanfare | — |

Tier colours (for particles/UI), in order: `#8E8E8E, #B39B7A, #7A7A9E, #C8D0DC, #D9A66B, #C4623D, #3D86C4, #D99B3D, #FFD84A, #9AF0FF, #2B1A4D`.

**One director owns this table.** `FeedbackDirector` listens to the gameplay events and plays the visual, the sound and the haptic of each row; gameplay code never calls feedback, so removing it leaves the game and the simulation result unchanged. Chained merges of one frame play one sound and one haptic (the highest tier). Nothing plays while paused or after game over, except the game-over sound, haptic and flash themselves. The Heavy merge haptic starts at tier 7 and the shake and the deep sound at tier 8, both `[TUNE]` values of `FeedbackConfig`.

**Piece animations (spawn, drop, land, merge) are purely visual.** They only scale and tint the `Sprite` child of the Piece prefab (the tint is the game-over flash); the body, collider and root scale never change, so physics, merges and score are identical with animations on or off. Durations, amplitudes and the landing impulse threshold are `[TUNE]` values of `FeedbackConfig`. The two source pieces of a merge are released at once; visual-only ghosts shrink in their place.

**Screen shake** must be disabled by the "Reduce screen shake" setting. Implement with a camera offset *that is snapped to whole pixels* (round to 1/16 unit) to keep the pixel look. The offset is applied to a parent rig of the camera, never to the camera that frames the jar. "Reduce screen shake" also removes the slow-mo and softens the Danger Line pulse to 2 Hz; the Supernova white flash stays (one 0.15 s event, flashes limited to 3 per second).

---

## 10. Art Direction

- **Style:** 16-bit-ish pixel art, limited palette (**32 colours max, one shared palette**), 1 px dark outline on pieces, cell-shaded highlight at top-left.
- **Resolution:** reference 192 × 320, PPU 16. All art authored at 1× and displayed at integer scale. **No rotation of sprites** except the physics rotation of pieces (accept sub-pixel look; if it shimmers, add a per-piece "face" that stays upright — see below).
- **Faces:** each piece has a tiny pixel face (2 eyes + mouth) that stays **upright** (child sprite with counter-rotation) for personality; face changes on events (blink idle every 2–4 s, "surprised" when hit hard, "happy" on merge).
- **Background:** deep-space gradient with parallax star layers (2 layers, very slow scroll). Palette shifts slightly with the highest tier reached (subtle tint) as a progression cue.
- **Jar:** drawn as a glass-like pixel container with a lighter inner rim; the Danger Line is part of the rim.
- **Palette:** `Assets/Art/Palette/coika.gpl` is generated from `CoikaPalette` (32 colours: the shared outline `#1B1230`, the 11 tier colours of §9, 10 shades and 10 highlights; the black hole's shade is the outline, the Moon and Neutron Star share a white highlight). Bodies and chart icons draw only these colours.
- **Files:** bodies are `piece_XX_<name>` (`Assets/Art/Sprites/Tiers`, width = round(diameter × 16) px, so no runtime resize), chart icons are `icon_XX_<name>` (`Assets/Art/Sprites/Icons`, 12 × 12 px). Both are produced by `Coika/Generate Piece Art`.
- **Import settings:** Filter Mode **Point**, Compression **None**, Mip Maps **off**, Sprite Mode Single, Pivot **Center**, Pixels Per Unit **16**. A postprocessor applies them to `piece_*` and `icon_*`, and the validator (`Coika/Validate Piece Art`, also run by an EditMode test) checks them. Piece bodies pack in the **Sprite Atlas** `Assets/Art/Atlases/Theme-Cosmic.spriteatlasv2` (Padding 4, Tight Packing off).
- **Android is the main target:** the default Android texture format (ASTC) smears pixel art. The **Android and iOS platform overrides** (importer and Sprite Atlas) are uncompressed RGBA32 with Point filtering: the import postprocessor and `SpritePlatformOverrides` set them on sprites and `PieceAtlasSetup` on the atlas, and an EditMode test fails without them. Verify them again after switching the build profile.
- **Sprite sheets per tier:** idle (1 frame), plus separate small face frames: neutral, blink, surprised, happy. Total ≈ 11 body sprites + 4 faces + particles + UI.

Placeholder policy: milestones M1 and M2 used flat-colour circles. M3 (issue #61) replaced them with the final bodies: no placeholder tier body remains in a normal run (the jar, background and effect sprites are still own placeholder art until #63).

---

## 11. Audio

- **Music:** 1 looping calm/ambient chiptune track in gameplay (≈ 90 BPM), 1 menu track. Duck −6 dB on game over.
- **SFX list:** spawn, drop, land (3 variants), merge (1 sample, pitched per tier), merge-big (tier ≥ 8), supernova, danger tick, game over, new best, UI click, UI back.
- **Engine:** Unity `AudioSource` pool (8 voices) + `AudioMixer` with groups `Master > Music, SFX`. Exposed volume params `MusicVol`, `SfxVol` (dB, mapped from the 0–1 setting with `Mathf.Log10(v)*20`).
- Avoid more than 3 simultaneous landing sounds (rate-limit: 1 per 40 ms).
- Formats: SFX WAV → Vorbis, 22.05 kHz mono, Load Type *Decompress On Load*; Music Vorbis, 44.1 kHz stereo, *Streaming*. (Optimize via `unity:optimize-audio` later.)

---

## 12. Game Modes & Progression

Premium and offline: no backend, no ads, no IAP.

| Mode | Milestone | Description |
|---|---|---|
| **Classic** | M1 | Rules above. Endless. |
| **Daily** | M3 | Same seed for everyone on a given UTC date (`seed = yyyyMMdd`). One attempt displayed as "best of today"; unlimited retries allowed. Local only. |
| **Zen** | M3 | No game over; the Danger Line is hidden. Score not saved to the leaderboard; a clear-jar button lets you reset the board. |

**Progression (lightweight, M3):**
- **Discovery log:** a collection screen showing each tier unlocked (reached at least once) with a lore line. Locked tiers are silhouettes.
- **Achievements (local, simple counters):** *First Merge*, *Reach Moon*, *Reach Gas Giant*, *Create a Star*, *Create a Black Hole*, *Supernova*, *Score 1000 / 5000 / 10000*, *5x combo*. Each stored as a bool in the save.
- **Skins (cosmetic, unlocked via achievements, M4):** swap the sprite set (e.g. Classic Cosmic, Candy, Sea Life). Implemented through the `ThemeDefinition` ScriptableObject (§14).

---

## 13. Persistence

Single JSON file at `Application.persistentDataPath/save.json`, written on: game over, settings change, app pause/quit (`OnApplicationPause(true)`). Atomic write (write to `save.tmp`, then replace).

```json
{
  "version": 1,
  "bestScore": { "classic": 0, "daily": { "date": "20261001", "score": 0 }, "zen": 0 },
  "highestTier": 0,
  "totals": { "games": 0, "merges": 0, "playSeconds": 0 },
  "discoveredTiers": [true, false, false, false, false, false, false, false, false, false, false],
  "achievements": [],
  "settings": {
    "music": 0.8, "sfx": 1.0, "haptics": true, "guideLine": true,
    "reduceShake": false, "leftHanded": false, "language": "en"
  }
}
```

- On load failure or version mismatch: back up the corrupt file as `save.bak`, create defaults. Never crash.
- **Run autosave (optional, M3):** while playing, write the board state every 5 s (tier, position, velocity per piece, score, RNG state) so the app being killed on mobile can resume. On resume, show a "Continue?" prompt.

---

## 14. Technical Architecture (Unity)

**Principles:** data in ScriptableObjects, small single-purpose MonoBehaviours, plain C# events (no heavy framework), no singletons except a `ServiceLocator`-style `Game` root and the audio manager.

### 14.1 Folder structure
```
Assets/
  Art/ (Sprites, Atlases, Fonts, VFX)
  Audio/ (Music, Sfx, Mixer)
  Data/ (TierDefinitions, Themes, GameConfig)
  Prefabs/ (Piece, Jar, Particles, UI)
  Scenes/ (Boot, Menu, Game)
  Scripts/
    Core/        GameManager, GameState, ServiceRegistry, SaveSystem
    Gameplay/    Piece, PieceFactory, MergeSystem, DropController,
                 SpawnQueue, OverflowDetector, ScoreSystem, ComboTracker,
                 PieceAnimator, PieceEffect (+ Spawn/MergePop/DropStretch/
                 LandSquash), MergeGhostPool
    Data/        TierDefinition, GameConfig, FeedbackConfig, ThemeDefinition,
                 SaveData
    UI/          HudView, GameOverView, PauseView, MenuView, SettingsView,
                 EvolutionChartView
    Audio/       AudioManager
    Fx/          ParticleSpawner, IParticleSpawner, FeedbackDirector (maps every event of §9 to particles,
                 screen effects, sound and haptics), FxKind,
                 ScreenShake (camera rig, 1/16 snap), ScreenFlash (overlay), TimeScaleOwner (slow-mo, pause),
                 ShakeCore, FlashCore
                 (own assembly Coika.Fx; Haptics is still to do;
                 squash and stretch is PieceAnimator, in Gameplay)
    Input/       PointerInputReader
  Tests/ (EditMode, PlayMode)
```
Use **assembly definitions** (`Coika.Core`, `Coika.Gameplay`, `Coika.Fx`, `Coika.UI`, `Coika.Tests`) so compile times stay low.

### 14.2 Data assets

**`TierDefinition` (ScriptableObject)** — one per tier
```
int index; string displayName; Sprite sprite; float diameterUnits;
float radius => diameterUnits/2; int mergeScore; Color tierColor;
AudioClip mergeSfx (optional); string loreLine; (localization key)
```
**`GameConfig`** — all [TUNE] values: gravity, drop cooldown, overflow time, combo window, combo cap, spawn weights, spawnable tier count, jar size, Supernova bonus, physics material references.

**`FeedbackConfig`** — the [TUNE] values of the visual piece animations (§9): spawn, drop-stretch, landing-squash and merge-pop durations and amplitudes, the landing impulse threshold and the merge ghost count. Referenced from `GameConfig` (same Core-Data group).

**`ThemeDefinition`** — an ordered list of 11 `TierDefinition` + background + jar sprites + music.

### 14.3 Key classes

| Class | Responsibility |
|---|---|
| `GameManager` | Owns `GameState`; starts/ends runs; raises `OnRunStarted/OnRunEnded/OnStateChanged` |
| `SpawnQueue` | Seeded RNG, weighted selection, anti-streak rule, exposes `Current` and `Next` tiers |
| `DropController` | Reads pointer X, moves held piece, handles drop + cooldown, draws the guide line |
| `Piece` | Holds `Tier`, `Rigidbody2D`, `merged` flag, `spawnTime`, handles `OnCollisionEnter2D` → notifies `MergeSystem` (does not merge by itself) |
| `PieceFactory` | Pool of piece prefabs; `Create(tier, pos, velocity)` and `Release(piece)` |
| `MergeSystem` | Receives contact pairs, resolves each physics step in a deterministic order (lowest InstanceID wins), spawns result, raises `OnMerged(tier, position, comboCount)` |
| `OverflowDetector` | Per-piece overflow timers, drives danger-line pulse & game over trigger |
| `ScoreSystem` + `ComboTracker` | Listens to merges/drops; exposes `Score`, `Multiplier`, events |
| `SaveSystem` | Load/save `SaveData` JSON |
| `AudioManager` | Pooled sources, mixer volumes, `PlaySfx(id, pitch, volume)` |
| `Haptics` | Wrapper: Android `Vibrator` / iOS `UIImpactFeedbackGenerator` via a small plugin or `Handheld.Vibrate()` fallback |

**Merge implementation notes**
- In `OnCollisionEnter2D` **and** `OnCollisionStay2D` (needed for chains where contact already exists), if `other.Tier == this.Tier && !merged && !other.merged`, enqueue `(this, other)` to `MergeSystem`.
- `MergeSystem.FixedUpdate` processes the queue: skip pairs where either piece is already `merged`; mark both `merged`, release both to the pool, spawn the next tier. Never destroy/instantiate inside a collision callback.
- Use `Rigidbody2D.position`, not `transform.position`, when computing the midpoint.

**Overflow detection** runs in `FixedUpdate` at 10 Hz (not every step). Check `piece.Collider.bounds.max.y > dangerLineY`.

### 14.4 Events
Plain C# `event Action<...>` on the owning class, wired in a composition-root `GameInstaller` MonoBehaviour in the Game scene. No `FindObjectOfType` at runtime.

### 14.5 Performance budget
- 60 FPS on a 2019 mid-range Android phone; 30 FPS floor.
- ≤ 60 active rigidbodies, ≤ 20 draw calls (atlas + SRP Batcher), 0 GC allocations per frame during gameplay (pool pieces, particles; avoid LINQ and string concatenation in `Update`; cache TMP strings with `SetText` and number formatting).
- Quality tier "Very Low" (the Android default, issue #39), AA off, no HDR (camera and URP asset). Post-processing: keep Bloom/Vignette on mid/high devices; on low-end (`SystemInfo.processorCount < 4` or `systemMemorySize < 3000`) a `QualityTier` service picks `Low`, which disables the Volume and halves the particle counts. The tier is read-only: it is not a Settings option (owner decision, issue #39) and shows in the development debug overlay (FPS, frame time, GC allocation count, piece count, tier), which is compiled out of release builds.

### 14.6 Build settings
- Android: IL2CPP, ARM64, Min API 26, portrait only, Target API per current Play requirement. iOS: min iOS 15, portrait only.
- Scripting backend: IL2CPP. Strip: Medium. Enable *Optimized Frame Pacing* (Android), target frame rate 60 (`Application.targetFrameRate = 60`), `QualitySettings.vSyncCount = 0`.

---

## 15. Localization

EN + ES at launch via the Unity **Localization** package (add later with `unity:localization`) or, to keep it minimal, a simple `Dictionary<string,string>` loaded from `Resources/Loc/en.json`, `es.json`. All UI strings go through keys from day 1. Numbers use invariant culture with thousands separators in display.

---

## 16. Accessibility

- Colour is never the only information: tiers differ by size and silhouette/pattern, not just colour (important with colour-vision deficiency).
- Reduce screen shake option; disabling haptics; no flashing >3 Hz (the danger pulse is 4 Hz — make it a **2 Hz soft pulse** when "Reduce shake" is on).
- Touch targets ≥ 44 px (≈ 7 % of reference width) / 9 mm physical.
- Text min size 8 px in reference space (renders ≥ 24 px on 1080p).

---

## 17. Analytics & Monetization

**None.** Premium means a one-time store purchase; the game ships with no network permissions, no SDKs, no ads. Therefore: no `INTERNET` permission in the Android manifest. Crash reporting via the store consoles only.

---

## 18. Milestones & Implementation Order

**M1 — Playable core (target: ~1 week)**
1. Layers, physics matrix, `GameConfig`, 11 `TierDefinition` assets with placeholder circles.
2. Jar (walls, floor, danger line), camera framing at 192×320.
3. `Piece` prefab + `PieceFactory` pool.
4. `SpawnQueue` + `DropController` (mouse first), drop cooldown.
5. `MergeSystem` (deterministic) + `ScoreSystem` + chain merges.
6. `OverflowDetector` + Game Over (print to console).
7. Minimal HUD (score, next piece) with TMP.
**Exit:** you can play a full run in the editor, lose, and restart.

**M2 — Mobile & juice (~1 week)**
Touch input via Input System, guide line, squash/stretch, particles, screen shake, audio pipeline + placeholder SFX, haptics, pause, settings, game over screen, save system, Android build on device.
**Exit:** 60 FPS on target device; feels good for 10 min of play.

**M3 — Content (~1–2 weeks)**
Final pixel art + faces, background parallax, evolution chart, menu, discovery log, achievements, Daily and Zen modes, localization EN/ES, run autosave.

**M4 — Polish & release**
Skins, balance pass with telemetry from playtesters (manual logs), accessibility pass, store assets (icon, screenshots 9:16, short trailer), performance and battery profiling, store submission, bug bash.

---

## 19. Acceptance Criteria (Definition of Done per feature)

| Feature | Passes when |
|---|---|
| Merge | Two same-tier pieces always produce exactly one next-tier piece; never zero, never two; verified over 1,000 randomized simulated drops in a PlayMode test |
| Determinism | Same seed + same drop X sequence → same final score (run twice in a test) |
| Overflow | A settled piece above the line for 2 s ends the game; a piece passing above it briefly while falling does **not** |
| Score | Matches the formula in §4; combo cap at ×3 verified |
| Input | Piece never leaves the jar bounds; a drop never triggers from a UI tap |
| Perf | Zero GC alloc per frame in Profiler during a 2-minute run; 60 FPS target device |
| Persistence | Killing the app mid-run and reopening keeps settings/best score (and offers continue after M3) |
| Pixel quality | No blurry sprites, no gaps/shimmer at 1080×1920, 1080×2400, 720×1280 — verified in an **Android build on a real device** (Android texture overrides applied), not only in the Game view |

**Automated tests (minimum):** EditMode tests for `SpawnQueue` (weights, anti-streak, seed), `ScoreSystem` (merge/combos), `SaveSystem` (round-trip, corrupt file). PlayMode test for merge chain + overflow.

---

## 20. Assumptions Made & Open Questions

**Assumptions (change if you disagree):**
1. **Theme = cosmic bodies**, name "Coika". The theme is entirely data-driven (`ThemeDefinition`) so it's cheap to change.
2. **Portrait**, reference **192×320** (the current project is 320×180 landscape; change the Pixel Perfect Camera `refResolutionX/Y` to 192/320 before producing art).
3. Pieces use **11 tiers**, spawn tiers 0–4, like the original.
4. Premium, fully offline, no leaderboard (could add Game Center / Play Games later without changing the design).
5. Two Black Holes make a Supernova instead of a hard end, so there's no "win" state.

**Open questions for you:**
- Do you want a fixed "win" condition (reach Black Hole → victory screen) in addition to endless play?
- Is a **fruit/animals/food** theme preferred over cosmic? (No code impact.)
- Do you want a **power-ups** system (e.g. "shake the jar", "remove one piece") in the future? Currently out of scope to keep the game pure; if added it should be earnable, not purchasable.
- Should Daily mode have a share button (OS share sheet with score text)?

---

## 21. Glossary

- **Tier**: index 0–10 of a piece's evolution stage.
- **Drop Line**: where the held piece hovers.
- **Danger Line**: top rim of the jar; overflow threshold.
- **Settled**: speed below 0.2 units/s.
- **Chain**: a merge whose result immediately merges again.
- **[TUNE]**: value expected to be balanced during playtesting.

---

## 22. Technical Constraints

Binding technical rules live in [.planning/constraints.md](.planning/constraints.md). **C-01:** scenes and all non-boot content must use Addressables to keep the initial install small. Where this GDD conflicts with that file (e.g. §7 scene flow, §14.1 folder layout, §14.5 asset loading, §17 network permission for remote content), the constraints file wins.
