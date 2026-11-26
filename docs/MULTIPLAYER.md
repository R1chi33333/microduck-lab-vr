# Playing together (LAN co-op)

Two players, each on their own Windows PC with a Quest over Link, share the same house: walk around
together, bite and carry toys, quack, dance, wear skates, put toys out from the toy bag and play with the
same flock of AI ducks. Phase 1 is for hanging out; football and the race stay single-player for now.

- One PC **hosts** (it runs the house: the flock's brains and the toy physics, and it plays too).
- The other PC **joins**. There is no server, no cloud and no account; nothing leaves the local network.
- Both PCs must run the **same build** of the game (the version is checked when joining).

## Quick start

1. Put both PCs on the same network: the lab router, or one phone's hotspot (see below).
2. On PC 1: open the toy bag (**Y**), pick **HOST**. The status line at the bottom of the bag shows
   `HOSTING  <this PC's address>`.
3. On PC 2: open the toy bag, pick **JOIN**. It shows `SEARCHING...`, finds the host by itself and then
   `CONNECTED: 2 PLAYERS`. The other player's duck appears with a "HELLO!".
4. To stop: **LEAVE** (joining PC) or **STOP HOST** (hosting PC). The other duck says "BYE!" and each PC
   carries on as a normal single-player game.

Keyboard shortcuts (editor and desktop): **F5** host / stop hosting, **F6** join / leave, **F7** leave.
The same commands are in the editor menu **Microduck > Co-op** while in Play mode.

The first time each PC hosts or joins, **Windows Firewall** asks whether the game (or the Unity Editor)
may use the network. Tick **Private networks** and allow it, on **both** PCs (the joining PC listens for the
host's announcement, so it needs the permission too). If you clicked it away by mistake: Windows Security >
Firewall & network protection > Allow an app through firewall > tick *Private* for MicroduckLabVR (or
Unity 6000.6.7f1).

## Networks

**Same network, same subnet.** Both PCs must get addresses from the same router or hotspot
(for example 192.168.43.17 and 192.168.43.52). A PC on Ethernet and another on the guest Wi-Fi of the same
router are often *not* on the same subnet.

**Phone hotspot.**
- Connect both PCs to the phone's hotspot; the headsets stay on their Link cables.
- Windows marks a new Wi-Fi network as *Public*. Set it to **Private** (Settings > Network & internet >
  Wi-Fi > the hotspot's name > Network profile type: Private), otherwise the firewall rules for private
  networks do not apply.
- Some phones keep hotspot clients apart from each other ("client isolation"): the PCs can then reach the
  internet but not each other. If JOIN never finds the host and the manual address (below) does not work
  either, use a small router instead.
- Mobile data is not used: everything stays between the two PCs.

**Lab router.** Usually just works. Some managed networks (university Wi-Fi) block broadcasts or traffic
between devices; then use the manual address, or a small router of your own.

## Manual host address (when the search finds nothing)

The search uses UDP broadcasts, which some networks block. You can tell the joining PC where the host is:

1. On the hosting PC, read its address on the toy bag's status line (`HOSTING 192.168.x.y`), or run
   `ipconfig` and take the IPv4 address of the Wi-Fi / Ethernet adapter.
2. On the joining PC, create a file **`microduck_net.json`** next to `MicroduckLabVR.exe` (when playing in
   the editor: in the project folder, the one with `Assets/`; the menu **Microduck > Co-op > Create Settings
   File** writes one there). It may also go in the game's persistent data folder
   (`%USERPROFILE%\AppData\LocalLow\Yutong Jin\Microduck Lab VR\`).

```json
{
    "hostIp": "192.168.43.17",
    "port": 7777,
    "discoveryPort": 47777,
    "playerName": ""
}
```

3. Pick **JOIN**: with `hostIp` set it connects straight to that address and does not search.
   Leave `hostIp` empty (`""`) to search again. The file is read each time you press HOST or JOIN.

If you change `port` or `discoveryPort`, change them on both PCs.

## Ports

| Port | Protocol | Used for |
|---|---|---|
| 7777 | UDP | The game connection (Netcode for GameObjects over Unity Transport). The host listens. |
| 47777 | UDP | The host's announcement, once a second, broadcast on every network it is on. The joining PC listens. |

## Testing on one PC

- **Editor + built game:** make a Windows build (**Microduck > Build Windows**), start it, then press Play in
  the editor. Host on one, join on the other (F5 / F6). Only one of them can drive the headset; the other
  plays with the keyboard (WASD, Q/E, Space, C, K, Tab, L, X, V). The search finds a host on the same PC.
- **Multiplayer Play Mode:** install *Multiplayer Play Mode* from the Package Manager (Window > Package
  Management > Package Manager > Unity Registry; it is a core package of this editor version, so the editor
  picks the matching version), open **Window > Multiplayer > Multiplayer Play Mode**, enable Player 2 and
  press Play.
  Host in the main editor (F5), join in the Player 2 window (F6).

## What is shared in phase 1

| Thing | Shared | Notes |
|---|---|---|
| Your duck | yes | Position, heading, where you look, beak, hops on the bed, skates (with lean), emotes, quacks (in your voice, from your duck), falls, reaching for toys. About 30 updates a second, smoothed on the other side. |
| Your look | yes | Colours, parts and voice; a change at the mirror shows on the other PC within half a second. |
| Laser dot | yes | Both players see both dots; the ducks chase either one. |
| AI flock | yes | The host's brains decide; the joining PC shows the same ducks doing the same things (walking, emotes, sitting, fetching toys, chasing dots, thought bubbles). Ducks follow either player. ADD DUCK / REMOVE DUCK work from both PCs. |
| Toys and loose props | yes | The host simulates them. Either player can bite, carry and drop things; walking into things pushes them on both sides. Toys put out from the bag, and toys put in the bin, show on both PCs. |
| Sound words, splashes | yes | QUACK!, CHOMP!, ZOOM!, SPLASH!, POW! and the puddle splash show on both PCs for either player's duck. |
| Tutorial, help, credits | no | Your own. Joining someone hides a running tutorial (it starts again next time you play alone, unless you finished it). |
| Football, race, swap duck | no | Off while hosting or joined ("NOT IN CO-OP YET"); planned for phase 2. |
| Garage, view (first / third person), comfort settings | no | Your own. |
| Toy train, grass, lights | no | Each PC runs its own. |

When the joining PC connects, its own toys from the bag and any extra ducks it added go away: the house is
the host's. When the session ends, both PCs keep whatever is in the house at that moment and carry on alone.

## Known limits (phase 1)

- The other player's duck has no collision body: you can walk through each other, and toys pass through
  their duck on the host. The flock keeps its distance from both.
- On the joining PC, toys answer a push a moment later (they are moved by the host, about a tenth of a second
  behind).
- Props are matched by their place in the scene, so both PCs need the same build of the house. Something put
  in the bin on one PC before joining stays gone there.
- Two players only. A third PC is turned away.
- The other player's skate rolling sounds are not played (their footsteps, quacks and voice are).

## For phase 2 (football, race)

`NetSession` (Assets/MicroduckVR/Scripts/Net) owns the connection and a small message protocol (`NetMsg`).
A game would add a few message kinds (start / stop, score, race checkpoints), run its rules on the host like
the flock (`NetFlock`) and treat both players through `PlayerTargets` (which already lists every player's
duck for the flock).
