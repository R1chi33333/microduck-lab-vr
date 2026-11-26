# Microduck Lab items

Every toy, piece of furniture, bit of household clutter and the duck itself, as separate Unity prefabs under
`Assets/MicroduckVR/Prefabs/`: drag one into a scene (or another project), share them as a `.unitypackage`, or edit
one item on its own.

## Licences

| What | Licence | Commercial use |
|---|---|---|
| `Ducks/` (Duck, Duck graphite, Duck lavender, Duck sky, Duck (live)) and their meshes and materials | [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), adapted from the Microduck robot by [Pollen Robotics](https://github.com/pollen-robotics/microduck_rl) | **No.** Non-commercial only; credit Pollen Robotics; share changes under the same licence |
| Everything else (toys, furniture, clutter, their meshes, materials, shaders, textures and the two scripts) | MIT, (c) 2026 Yutong Jin (see `LICENSE`) | Yes |

The toys that the web game took from glTF sample models (toy car, chess pieces, cookie, toast, croissant) are rebuilt
from simple shapes in code here; no third-party model or font file is in the prefabs or the package. The package
carries all of this in `Assets/MicroduckVR/Prefabs/CREDITS.txt`.

## Making and refreshing the items (Microduck > Assets)

1. Open `Assets/MicroduckVR/Scenes/House.unity` and run **Export Prefabs**. One prefab per item in
   `Prefabs/{Toys,Furniture,Clutter,Ducks}`, its meshes in `Prefabs/Meshes/<category>/<item>.asset`, materials made
   at runtime in `Prefabs/Materials/`; the house's own materials stay in `Materials/House`.
2. **Make Catalog** draws every prefab to `Logs/assets/catalog.png` and rewrites this file.
3. **Export Package** writes `Builds/MicroduckLab_Assets.unitypackage`.
4. Optional: **Link Scene To Prefabs** swaps the house's furniture and clutter for instances of the prefabs, so
   editing a prefab changes the house. **Build House** makes the house again from code and drops those links; tick
   **Relink House After Build** to link the new house to the prefabs straight after each build.

How items are picked: each named prop the house builder makes (a root with parts, such as `Bookcase`, `Teddy`,
`Mug`), furniture the builder lays out as separate pieces (`Table` = top + 4 legs, `Kitchen chair`, `Bed`, `Kitchen
units`, ...), a few single pieces (`Fridge`, `Shelf`, `Pouf`, ...) and every toy in the toy bag. Walls, floors,
ramps, the pitch, the garden ground and the scenery outside stay part of the house. Identical copies share one
prefab; an item that comes in many sizes or colours (books, blocks, flowers) keeps up to 8 versions (`Book`,
`Book 2`, ...). Pivots sit at the bottom centre, at the floor contact, and items are unrotated.

Using them elsewhere: Unity 6 with URP. The prefabs keep the game's layers by number (World 6, Duck 7, Toy 8,
Prop 10): name those layers in the other project or change them. For the ink outlines add a Full Screen Pass
Renderer Feature using `Materials/ComicOutline.mat` (Depth and Normal inputs) to the URP renderer.

The item list is written here by **Microduck > Assets > Make Catalog** (run it after Export Prefabs).
