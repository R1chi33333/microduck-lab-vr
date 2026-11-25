# Porting plan: Microduck Lab (web) → Unity VR (Quest Pro over Link)

The web game is about 8,500 lines of JavaScript (three.js r160 + Rapier). Nothing is converted
automatically: each module is rewritten in C# with the same behaviour and tuning numbers.
Read the matching file in `Reference/web/` before porting a system, and keep the constants.

## 1. Player = the duck (design decisions)

**Scale.** The world stays at real metric scale. The player's camera sits in the duck's head, so the
eye height is about 0.22 m. Put the XR Origin inside the duck's head socket and scale the XR Origin
transform by about 0.15. That scales the stereo baseline (IPD) too, which is what makes the sofa read
as a building. Expose the scale as a setting (0.10–0.25) for tuning.

**Head.**
- Tracking origin mode: Device. Recenter on start and on a menu button.
- HMD rotation drives the duck's neck and head joints (neck pitch, head pitch, yaw, roll) within
  the rig's joint ranges. Past the head-yaw limit, the body turns to follow, smoothly.
- HMD position: apply 15–25 % of the (already scaled) positional offset as a small head sway. Clamp
  it so the eyes never leave the head shell.
- Looking down and moving the head towards a toy is the grab gesture: the beak IK reaches for what
  the player is looking at (port `DuckBody` beak reach / CCD from `duck.js`).

**Controls (Quest Touch Pro).**

| Input | Action | Web equivalent |
|---|---|---|
| Left stick | Walk (relative to head yaw), procedural legs step | `W A S D` |
| Right stick X | Snap turn 30° (option: smooth turn) | mouse / `A D` |
| Right trigger | Beak: hold = open; release on a toy = grab; trigger again = drop | `E` grab / drop |
| Right grip | Laser pointer from the right controller (a "remote" the duck holds) | `L` |
| A | Chirp (beak flap + voice) | `Space` (walking) |
| B | Skates on / off | `K` |
| Left trigger (on skates) | Boost | `Shift` |
| Left grip (on skates, hold) | Drift | `Space` hold (skating) |
| X | Emote wheel (sit, crouch, nod, head shake, dance, look around) | `X` `C` `1`–`4` |
| Y | Pause menu: bag, football, race, mirror (customise), thoughts on/off, comfort settings, recenter | `B` `F` `R` `T` `?` |
| Left stick click | Switch body to the next duck (fade out / in) | `Tab` |

Hands are not shown (ducks have none). Controllers show as small comic "remote" props only while the
menu or laser is in use.

**Comfort (non-negotiable).**
- Tunnelling vignette while moving or turning, with strength scaling with speed and turn rate.
  The setting goes from off to strong, default medium.
- Never roll, pitch or shake the camera from code. The web version's camera shake, dutch angles,
  FOV kicks and impact frames are **not** ported. Their energy moves to world-space FX (stickers,
  speed lines around the duck, dust, sparks) and to sound.
- When the duck tips over (falls), cut to a quick fade to black, then fade back once it is up
  again. Never show the tumble from inside the head.
- Skates: keep `SKATE` tuning from `agent.js`, but cap the top speed in VR at about 0.30 m/s by
  default. Exposed as a setting. Drifting rotates the body, while the view yaw follows with smoothing
  and no roll.
- Bed bounce: limit vertical camera acceleration. The duck bounces; the camera rides a damped spring.
- Seated play is the default. Everything must work seated.

**Customising** (`customise.js`, "Build your duck"): a mirror in the living room. Stand in front of it
and the pause menu offers colourways (cream, graphite, lavender, sky) and per-part paint
(head, body, legs, feet) plus a voice preset. The player sees themselves in the mirror. Render the
mirror with a planar reflection camera at reduced resolution.

## 2. Rendering: the comic look in URP and XR

Source: `Reference/web/toon.js`, `look.js`, `world.js` (procedural textures), `house.js`.

**Palette** (from `look.js` / `toon.js`):
- ink `#1B1430`, paper `#FFF7E8`, rim `#FF7AD9`
- duck cream `#FBF4E4` / `#F3EAD6`, accent orange `#FF8A1F`, yellow `#FFC928`, lilac `#DCC9E2`,
  warm grey `#DCE0E7`, graphite `#97A0AE`, lens `#0B0C12`
- skins: `SKINS` in `look.js` (cream, graphite, lavender, sky)

**Toon shading** (custom URP lit shader in HLSL, or Shader Graph with a custom lighting node):
- Banded ramp on main-light NdotL × shadow attenuation. Thresholds: lit/mid `T1 = 0.62`,
  mid/shadow `T2 = 0.22`, deep `T3 = 0.06` (fractions of the key light).
- Shadows lean towards violet (shadow hue 0.72) instead of going grey.
- Fresnel rim band at 0.55 in rim pink, kept subtle on the duck.
- Hard shadows from one directional key light. Use the trailer's final look as the default:
  near two-tone, with halftone and hatching off.
- **Screen-space halftone and hatching shimmer in VR** (different per eye, swimming with head
  motion). If they come back, make them object-space or triplanar world-space patterns, and keep
  them off by default.

**Ink outlines** (URP `ScriptableRendererFeature`, full-screen pass after opaques):
- Edge detection on depth and normals (enable the DepthNormals prepass). The line colour is ink.
- Width in pixels is scaled by render resolution. Distance fade keeps far lines from turning into
  noise.
- Port the **planar fix**: test the Laplacian of 1/z rather than raw depth. Without it, floors seen at
  grazing angles produce black horizontal bands. This is exactly the bug a player reported on Android
  Firefox for the web version.
- Must be XR-correct: use `Blitter` with `RTHandle`s and `TEXTURE2D_X`. Verify both eyes in the
  Frame Debugger with Single Pass Instanced.
- Optional extra thickness on the ducks: an inverted-hull outline pass (`Cull Front`, normal
  extrusion in view space).

**Comic FX** (`createComicFX`, `createSkateFX` in `toon.js`): world-space billboarded stickers
(ZOOM, SNAP!, BONK!, GOAL!, WHEE!, SPLASH!, DRIFT!) in the Bangers font, with pop-in scale animation.
Also skate smoke puffs, sparks, skid marks (decal or trail mesh) and speed lines as a world-space
cone around the player's duck. Get Bangers as TTF from https://github.com/googlefonts/bangers (OFL)
and use TextMeshPro.

**Quality settings:** MSAA 4×, no TAA (it smears in VR), shadow map 4096, render scale 1.0–1.2
on the PC, static batching for rooms (the web version already merges per room and material).

## 3. Module map

| Web file (lines) | Unity system | Notes |
|---|---|---|
| `microduck_rig.glb.txt` | `Assets/MicroduckVR/Models/microduck_rig.glb` | Base64 GLB. Decode it with an editor script. 95 nodes, 70 meshes, no skin: a hierarchy of rigid parts. Uses `KHR_mesh_quantization` (glTFast supports it). **Joint data is in node `extras`** (`role`, `joint`, `axis`, `range`, `zero_quat`, `stand_quat`). glTFast does not import extras, so an editor importer parses the GLB JSON into a `DuckJointMap` ScriptableObject. Pose rule: `q = zero_quat * axisAngle(axis, θ)` (convert handedness). Hide internals per `SIMPLIFY_HIDE` / `SIMPLIFY_INTERNAL` in `look.js`. |
| `duck.js` (591) | `MicroduckVR.Duck.DuckBody` | 2-link leg IK, foot planting, pigeon-toed gait (`GAIT`: maxSpeed 0.16 m/s, yaw 2.2 rad/s, swing 0.4, lift 0.016, footX 0.045, toeIn 0.11), head stabilisation and look-at, beak CCD reach, jaw, tip-over and get-up. Port the custom IK as is, rather than using Animation Rigging, to keep the feel. |
| `agent.js` (679) | `Duck.DuckAgent` + `Flock.DuckBrain` | Locomotion (walk / skates with `SKATE` tuning), grab/carry, falls, sit/crouch/emotes, laser chase, kicks, races. Brain states: wander, roam (room to room via `DOOR_ROOMS`), follow, toy/fetch, ball, laser, race, sit, idle, dance. Boids spacing. The player's duck uses the same agent with input in place of the brain. |
| `physics.js` (181) | `World.PhysicsSetup` | Rapier → PhysX. Collision layers: Static, Props, Duck, Char. Each duck has a kinematic capsule character controller, plus kinematic trunk/head/feet boxes that push props. Props are rigidbodies. Fixed timestep 1/90 s. |
| `world.js` (976), `house.js` (439), `adapter.js` (98) | `World.HouseBuilder` (editor) + `World.WorldQueries` (runtime) | Rooms: living x[−3,3], kitchen x[3.12,7], bedroom x[−7,−3.12], z[−2.5,2.5]; hallway x[−7,7] z[2.62,3.82]; garden x[−3.5,3.5] z[3.94,7.6]. Walls 0.12 m, doors in `DOORS`. Surfaces in `SURFACE_TYPES` become PhysicMaterials: tile friction 0.15 (slippery), bed bounce 0.65. Textures are procedural (canvas), so regenerate them as `Texture2D` in editor code or use flat colours plus toon bands. Build the house by an editor script into a scene, and keep `groundHeightAt`, `isOverDrop`, `randomSpot` and the room lookup as runtime queries. **Drop the wall cutaway** (`CUT`): in first person it is not needed. |
| `props.js` (486), `assets.js` (144), `assets/models/*.glb` | `World.Props` | Procedural toys (foam cube, ring, rubber duck, pencil, sock ball, key, bottle cap, crayon, bread crust, toy brick, wicker ball, toy figure, strawberry, grape, cracker) plus glTF props (toy car, chess pawn and knight, cookie, toast, croissant). The glTF props are meshopt-compressed, so add `com.unity.meshopt.decompress` or re-export them. Keep `grip.width ≤ 0.022 m` for graspables. Roller skates and laser dot are accessories. |
| `inventory.js` (127) | `UI.BagMenu` | "The bag": a world-space comic menu on the Y-menu. Point the laser at the floor or furniture to place an item, with a ghost preview (green ok / red blocked). A trash slot. Caps on ducks and toys as in `main.js`. |
| `games.js` (212) | `Games.Football`, `Games.Race` | Sock-ball football (two goals, team colours `#FF9F0A` / `#64D2FF`, score) and the checkpoint race (countdown, laps, lap timer, AI racers). Also the solo hallway drag strip. |
| `thoughts.js` (213) | `Flock.ThoughtBubbles` | World-space comic bubbles over AI ducks, with lines from `LINES` (wander, roam {room}, fetch {prop}, ball, race, laser, sit, idle, edge, fall, up, bed, water, slippery, skate...). Timing: SHOW 3.0 s, FADE 0.35 s, REPEAT 8 s, at most 4 on screen, pool of 6, range 4 m (scale for VR legibility). Always face the camera, occluded by walls, no overlap. |
| `customise.js` (99), `garage.js` (145) | `Duck.Customiser` (mirror) + `Duck.Garage` | Colourways and per-part paint, voice presets. Garage (pop-off / slide-in replacement parts) is the last milestone. |
| `voice.js` (100), `sfx.js` (231) | `Audio.DuckVoice`, `Audio.Sfx` | Procedural WebAudio. Port as synthesis in `OnAudioFilterRead`, or pre-render the same recipes into AudioClips at load. Each duck's timbre is seeded from its serial. Footsteps per surface, servo whirr, skate wheels / push / skid / clack, sit / fall / get-up. Spatialise everything (3D AudioSources). |
| `mind.js` (286) | `Duck.Sensors` (optional, last) | The simulated 8×8 ToF depth sensor and the safety policy that stops the duck at edges ("Whoa, too high!"). Keep the edge-stop behaviour from early on (it matters on the kitchen table and the bed). The sensor visualisation is optional. |
| `toon.js` (730), `look.js` (1488) | `Comic.*` shaders + renderer feature + FX | See section 2. |
| `main.js` (1262) | `Game.GameManager` and others | Bootstrap, flock spawn, modes, input, HUD. The HUD becomes world-space or diegetic (a small comic panel on the duck's "wrist", or the menu). |

## 4. Packages

`com.unity.render-pipelines.universal`, `com.unity.xr.management`, `com.unity.xr.openxr`,
`com.unity.inputsystem`, `com.unity.cloud.gltfast`, `com.unity.meshopt.decompress`,
`com.unity.textmeshpro` (or the UGUI-integrated TMP in Unity 6), `com.unity.xr.interaction.toolkit`
(optional: only if its tunnelling vignette or snap-turn helpers save time; locomotion is custom).
Later, optionally the Meta XR Core SDK and Movement SDK for Quest Pro eye and face tracking (the duck
blinks or opens its beak when you do). Add them only if they do not conflict with OpenXR over Link.

## 5. Milestones

Each milestone ends with: project compiles, screenshots in `Logs/shots/` checked, the user tests in
the headset over Link, and a push to `main`.

- **M0 Project.** Create the Unity 6000.6.7f1 URP project at the repo root. Add the packages, the
  OpenXR setup for PC and Link (Touch Pro profile), Single Pass Instanced, 90 Hz. Add the
  `.gitignore` check, a test scene with a cube, and confirm it shows in the headset.
- **M1 Be the duck (prototype).** Import the rig (with joint map and handedness tests). The XR Origin
  sits in the duck's head at scale. HMD drives the neck and head, the left stick walks with the ported
  gait and IK, plus snap turn and vignette. Build one grey-box living room at real scale, three AI ducks
  that only wander, and one graspable toy picked up with the beak. **The user decides here whether the
  scale and comfort feel right before anything else is built.**
- **M2 Comic look.** Toon shader, ink outline renderer feature (XR-correct, planar fix), palette, skins,
  hard shadows. Compare screenshots with the reference frames in `docs/look/` (from the web version).
- **M3 The house.** All rooms, furniture, surfaces (slippery tiles, bouncy bed, puddle, garden slope),
  doors, edge safety.
- **M4 The flock.** Full brain (all states), boids, room roaming, thought bubbles, voices, sfx.
- **M5 Toys and the bag.** All props, physics, grab and carry, the bag placement menu.
- **M6 Skates.** Skates, boost, drift, smoke, sparks and skid marks, with the comfort caps.
- **M7 Games.** Laser pointer, football, race, drag strip, emotes, dance, switch duck.
- **M8 Mirror and garage.** Customising in the mirror, voice presets, garage parts, then the optional
  sensors view. Credits panel. Windows build.

## 6. Performance budget (90 fps on the PC)

- Up to 8 ducks × about 60 rigid part meshes: merge each duck's static sub-parts per joint into
  one mesh per joint and material at import time (as `mergeInternals` does in `main.js`).
- Rooms are static and batched; garden grass is an instanced or flat comic texture.
- One shadow-casting directional light. The planar mirror renders only when the mirror is in view.
- Profile with the Unity Profiler (Play mode over Link) and the Meta Quest Link performance HUD.
</content>
</invoke>
