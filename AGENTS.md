# Microduck Lab VR — instructions for coding agents

Read this file and `docs/PORTING_PLAN.md` before doing anything in this repository.

## What we are building

A first-person VR port of the browser game **Microduck Lab** (source pinned in `Reference/web/`,
live at https://r1chi33333.github.io/microduck-lab-preview/). The player **is** a tiny robot duck
(about 25 cm tall) in a human-sized comic-book house, with a flock of AI ducks around. Every
gameplay feature of the web version comes across, and so does the hard American-comic look
(flat toon bands, thick ink outlines, pop-art stickers).

## Target setup (fixed)

| Item | Value |
|---|---|
| Engine | Unity **6000.6.7f1**, Universal Render Pipeline (URP) |
| OS | Windows 10/11, the game runs on the PC |
| Headset | **Meta Quest Pro** over **Quest Link** (USB-C cable) or Air Link |
| XR | OpenXR (`com.unity.xr.openxr`), PC tab. Runtime: Meta Horizon Link set as the active OpenXR runtime |
| Interaction profiles | Meta Quest Touch Pro Controller Profile, plus Oculus Touch Controller Profile as a fallback |
| Stereo | Single Pass Instanced |
| Frame rate | 90 Hz, hold 90 fps (11.1 ms) on the PC with headroom |
| Input | Input System package only (no legacy Input Manager) |

Iterate in the editor: with Link running, pressing **Play** renders straight into the headset, so no
build is needed for day-to-day testing. Make a Windows build (`Builds/`) only at milestones.

## Hard rules

1. **Commits:** author `Yutong Jin <richiejyt@gmail.com>`. No AI-tool attribution anywhere:
   no `Co-Authored-By` trailers for tools, no tool or model names in commit messages, PR text,
   code comments, docs or in-game text. Configure the repo once:
   `git config user.name "Yutong Jin"` and `git config user.email "richiejyt@gmail.com"`.
2. **No research or academic content** in the game, repo or docs (no study, thesis or PhD material).
   This is a game.
3. **Licences:** the Microduck robot model is Pollen Robotics' (CC BY-NC-SA 4.0): non-commercial
   only, keep attribution in `CREDITS.md`, the README and an in-game credits line. Keep the
   attribution for every third-party prop and font (see `Reference/web/CREDITS.md`).
4. **Never type passwords or sign in for the user** (Unity Hub, Meta, GitHub). Ask them to do it.
5. **Comfort first in VR:** never roll, shake or pitch the camera from code; no screen-space
   impact flashes; horizon always level. See "Comfort" in the porting plan.
6. Keep the web source in `Reference/web/` read-only. It is the behaviour reference; port from it.
7. Small, working commits. The project must compile at every commit. Push to `main` at the end
   of each milestone step.

## How to work in this repo

- Unity project lives at the repo root (`Assets/`, `Packages/`, `ProjectSettings/`).
  `Reference/` and `docs/` sit outside `Assets/` so Unity does not import them.
- Code goes in `Assets/MicroduckVR/Scripts/<Area>/`, one namespace per area
  (`MicroduckVR.Duck`, `MicroduckVR.Flock`, `MicroduckVR.World`, `MicroduckVR.Comic`, ...).
  Add an assembly definition per area once there are a few files.
- Shaders: HLSL in `Assets/MicroduckVR/Shaders/`, URP-compatible, XR-safe
  (`TEXTURE2D_X`, `SAMPLE_TEXTURE2D_X`, `UNITY_VERTEX_INPUT_INSTANCE_ID`,
  `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`).
- Compile check without the editor open:
  `"C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -quit -logFile Logs/batch.log`
  then read `Logs/batch.log` for `error CS`. (Batch mode cannot open the project while the
  editor has it open: then read the editor's `Editor.log` in `%LOCALAPPDATA%\Unity\Editor\`.)
- Editor scripts that build scenes or assets in code (menu items under `Microduck/...`) are
  preferred over hand-made scene edits, so the work stays reproducible and reviewable.
- Visual checks without the headset: an editor script renders a fixed desktop camera
  (`Microduck/Debug/Capture Screenshots`) to `Logs/shots/*.png`; look at them before asking
  the user to put the headset on.
- Ask the user to test in the headset at the end of each milestone, with a short list of what
  to try and what to report (comfort, scale feel, frame drops, bugs).

## Units and coordinates

- Metres, Y up, same as the web version. Real-world scale: the house is human-sized
  (living room 6 m × 5 m, walls 2.5 m), the duck is about 0.25 m tall.
- glTF is right-handed (+Z forward), Unity is left-handed. glTFast converts by mirroring X.
  The web code's joint maths (hinge axes, `zero_quat`, yaw convention
  `forward(yaw) = (sin yaw, 0, cos yaw)`) must be converted consistently: mirror X on vectors,
  and for quaternions (x, y, z, w) → (x, −y, −z, w). Write unit tests for this conversion first.
</content>
</invoke>
