# Installing the game on another PC

[中文说明](INSTALL.zh-CN.md)

You do not need Unity to play: the game is a ready-made Windows build. Install it on every PC that will
play (two PCs for co-op, see [MULTIPLAYER.md](MULTIPLAYER.md)).

## What you need

- **A Windows 10 or 11 PC that can run PC VR** (a recent NVIDIA or AMD graphics card; the same kind of PC
  that runs Quest Link).
- **A Meta Quest headset** (made for the Quest Pro; Quest 2 and Quest 3 should also work over Link) and either
  a **USB-C Link cable** or **Air Link** on a 5 GHz Wi-Fi network.
- **The Meta Horizon Link app** (the PC app that used to be called the Oculus app), from
  <https://www.meta.com/help/quest/articles/headsets-and-accessories/oculus-rift-s/install-app-for-link/>.

## 1. Set up Link (once per PC)

1. Install and open the **Meta Horizon Link** app on the PC and sign in. Pair the headset in **Devices**.
2. In the app: **Settings > General > OpenXR Runtime > Set Meta Horizon Link as active**. The game talks to
   the headset through OpenXR, so this step matters: without it the game opens on the monitor only.
3. Also in **Settings > General**, turn on **Unknown Sources**. The game is not from the Meta store.
4. Put on the headset, connect the cable (or open Air Link) and enter **Link** from the headset's quick
   settings. You should now see the Link home in the headset.

## 2. Get the game

**Option A: download a release (easiest).**

1. Open the repository's **Releases** page:
   <https://github.com/R1chi33333/microduck-lab-vr/releases>.
2. Download **`MicroduckLabVR-Windows.zip`** from the newest release.
3. Right-click the zip > **Extract All...** to a folder of your own, for example `C:\Games\MicroduckLabVR`.
   Run the game from the extracted folder, not from inside the zip.

**Option B: build it yourself** (only if you want the newest code).

1. Install **Unity Hub** and **Unity 6000.6.7f1** (with *Windows Build Support (Mono)*, which is included).
2. `git clone https://github.com/R1chi33333/microduck-lab-vr.git` and open the folder in Unity Hub
   (**Add > Add project from disk**). The first opening imports everything and takes a while.
3. In Unity: menu **Microduck > Build Windows**. The game is written to `Builds/Windows/`.
   Copy that whole folder to the other PC.

## 3. Play

1. Make sure the headset is in **Link** (step 1.4).
2. Double-click **`MicroduckLabVR.exe`** in the game folder.
   - Windows may show **"Windows protected your PC"** because the game is not signed: click
     **More info > Run anyway**.
   - The first time you use co-op, Windows Firewall asks about network access: tick **Private networks** and
     allow it.
3. The game starts in the headset. The first time, you make your duck at the mirror, then a short tutorial
   shows every control. The toy bag (**Y**) has **HELP** with all the controls, and **TUTORIAL** to run it
   again.

Controls are listed in the [README](../README.md#playing).

## Updating

Download the new release and extract it over the old folder (or into a new one). Your duck's look and the
tutorial progress are kept: they are saved in Windows, not in the game folder. **For co-op, both PCs must
run the same version.**

## Troubleshooting

| Problem | Fix |
|---|---|
| The game opens on the monitor but not in the headset | Set Meta Horizon Link as the active OpenXR runtime (step 1.2), make sure the headset is in Link, then start the game again. |
| "VCRUNTIME140.dll was not found" or a similar missing-DLL error | Install the latest Microsoft Visual C++ Redistributable (x64) from Microsoft, then try again. |
| Choppy picture in the headset | Use the Link cable instead of Air Link; in the Link app's device settings, lower the refresh rate to 72 Hz or the render resolution one step. |
| Co-op: JOIN keeps searching | See [MULTIPLAYER.md](MULTIPLAYER.md#networks): same network, the network set to Private, firewall allowed on both PCs, or the manual host address. |
| You want to make your duck again | Toy bag (**Y**) > **MY LOOK**, or walk up to the mirror and press **X**. |
