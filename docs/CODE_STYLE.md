# Code style

How the C# in this project is laid out and written. It describes what the code already does; follow it for new code.
`.editorconfig` at the repo root holds the same rules for the IDE, at suggestion level only: never reformat whole
files to satisfy it.

## 1. Folders and areas

- Game code lives in `Assets/MicroduckVR/Scripts/<Area>/`. The areas are Audio, Comic, Custom, Dev, Duck, Flock,
  Games, Net, Player, Util and World.
- Editor-only code lives in `Assets/MicroduckVR/Editor/`, one subfolder per job: `Builders/` (house, scenes,
  furniture styles), `Assets/` (asset export, rig import, TextMesh Pro setup), `Film/` (the film director),
  `Checks/` (smoke tests, screenshots, audits), `Setup/` (project, comic, build and co-op menu setup) and `Tooling/`
  (the editor bridge). The Editor assembly definition sits at `Editor/` and covers every subfolder.
- Shaders are HLSL in `Assets/MicroduckVR/Shaders/`, URP-compatible and XR-safe.
- `Reference/web/` is the browser version, read-only. `docs/` and `Reference/` sit outside `Assets/`, so Unity does
  not import them.
- Put a new type in the area whose job it is: the duck's body in Duck, AI in Flock, the player's controls in Player,
  the house and toys in World, the comic look in Comic, co-op in Net. A lower area should not start using a higher
  one (for example Duck must not use Player).

## 2. Namespaces

- `MicroduckVR.<Area>`, where the area is the top folder under `Scripts/` (`MicroduckVR.Duck`, `MicroduckVR.World`, ...).
  Subfolders do not add a segment.
- Editor code uses `MicroduckVR.EditorTools`. This avoids clashing with `UnityEditor.Editor`, and tooling calls
  Editor methods by their full name, so it does not change.
- Block-scoped namespaces (`namespace X { ... }`), not file-scoped ones.

## 3. Files

- One main type per file, and the file is named after it. Small nested types are fine when only the owner uses them.
- A file that grows past about 400 lines becomes a `partial` class split by topic. Each part is named
  `Class.Part.cs` (`AssetExport.Catalog.cs`, `HouseBuilder.Rooms.cs`). The file named after the class keeps its
  fields, constants and entry points. Every part says `partial`, sits in the same folder and namespace, and keeps the
  `using`s it needs.
- A MonoBehaviour or ScriptableObject must stay in the file named after it: scenes, prefabs and assets find it by the
  GUID in its `.meta`.
- Rename or move a script with `git mv` on both the `.cs` and its `.cs.meta`. Never delete a script and recreate it.

## 4. Naming

- PascalCase for types, methods, properties, events and constants. `I` prefix for interfaces.
- Private fields are camelCase with no prefix (no `_`, no `m_`). Leave out the `private` keyword; it is the default.
- Inspector-serialized fields are camelCase (Unity's convention). Run-time-only public state uses PascalCase, as
  `DuckBody` does.
- Never rename or retype a serialized field without `[FormerlySerializedAs]`. Saved strings (PlayerPrefs keys, the
  custom duck JSON) and the string fields that scenes and prefabs store (surface type, room, skin, toy kind) keep
  their values.
- Use an enum for a closed set that only code uses, and named string constants for ids that are saved or shared with
  the web data. Avoid bare magic strings in logic.

## 5. Formatting

- 4 spaces, LF line endings, UTF-8 without BOM, a final newline.
- Allman braces (opening brace on its own line).
- `var` wherever the type is clear. A one-line `if` without braces is fine.
- Aim for lines under about 160 characters in new code. Do not rewrap old lines just for that.

## 6. Comments

- A one-line `<summary>` on public types and on public members that other areas use: what it is for and why, not a
  retelling of the code.
- Inline comments explain intent, units or a non-obvious choice. Numbers ported from the web version keep their web
  name in a comment.
- Plain English. No tool or model names anywhere in code, comments, docs or in-game text, and no research or academic
  content: this is a game.

## 7. VR comfort

- Never roll, pitch or shake the player's camera from code, and keep the horizon level.
- No screen-space impact flashes: effects live in the world.
- Units are metres, Y up; the duck is about 0.25 m tall.

## 8. Editor tools are an API

- Menu items live under `Microduck/...`. Their paths, and the names of static methods with no parameters in any
  class, are used by `Tools/editor-cmd.ps1` and `Tools/unity-batch.ps1`. Do not rename them; add a forwarding method if
  a new name is wanted.
- Prefer an Editor script with a menu item over hand edits to scenes, so the work can be repeated and reviewed.

## 9. Compiling and checking

The editor bridge (`Editor/Tooling/RemoteCommands.cs`) runs commands in the open editor and logs to `Logs/microduck_cmd.log`:

```
powershell -ExecutionPolicy Bypass -File Tools\editor-cmd.ps1 -Commands "refresh"
powershell -ExecutionPolicy Bypass -File Tools\editor-cmd.ps1 -Commands "method MicroduckVR.EditorTools.BagTest.Run"
```

- `refresh` reimports and recompiles; the log must show no `COMPILE ERROR`.
- `method Ns.Class.Method` runs a static method, `menu Microduck/...` runs a menu item, `play` / `stop` toggle Play mode.
- Smoke tests are the `Run` methods on the `*Test` classes and `UiAudit`; each logs `ok` and `[Microduck]` lines.
  `DebugCapture.PlayViews` writes screenshots to `Logs/shots/` to compare by eye.
- With the editor closed, `Tools\unity-batch.ps1` compiles in batch mode (add `-Method Ns.Class.Method` to run one)
  and prints any `error CS` lines from `Logs/batch.log`.

## 10. Commits

- Authored by the owner, `Yutong Jin <richiejyt@gmail.com>`, with no `Co-Authored-By` or other trailers.
- Small commits with a plain message saying what changed. The project compiles at every commit.
