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
2. **Make Catalog** draws every prefab to `Logs/assets/catalog.png` and rewrites this file; **Make Design Sheets**
   draws the duck turnaround and one labelled sheet per category to `docs/images/`.
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

The list below was written by **Microduck > Assets > Make Catalog** on 2026-11-26: 241 prefabs. The design sheets (one per category) are in `docs/images/` (**Make Design Sheets**); the all-in-one preview sheet is `Logs/assets/catalog.png` (local, not in git).

## Toys (29)

| Item | Size (cm, W × H × D) | Physics | Licence |
|---|---|---|---|
| `Toys/Blue duck.prefab` | 3 × 3.6 × 5.1 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Bottle cap.prefab` | 3.1 × 0.7 × 3.1 | Toy, rigidbody 2 g, grabbable, Box collider | MIT |
| `Toys/Bread crust.prefab` | 5.2 × 1.7 × 2.1 | Toy, rigidbody 3 g, grabbable, Box collider | MIT |
| `Toys/Button.prefab` | 2.2 × 0.5 × 2.2 | Toy, rigidbody 1 g, grabbable, Box collider | MIT |
| `Toys/Chess knight.prefab` | 3.1 × 5.5 × 3.5 | Toy, rigidbody 15 g, grabbable, Box collider | MIT |
| `Toys/Chess pawn.prefab` | 3.2 × 5 × 3.2 | Toy, rigidbody 12 g, grabbable, Box collider | MIT |
| `Toys/Cookie.prefab` | 4.6 × 1.3 × 4.6 | Toy, rigidbody 8 g, grabbable, Box collider | MIT |
| `Toys/Cracker.prefab` | 4 × 0.5 × 4 | Toy, rigidbody 3 g, grabbable, Box collider | MIT |
| `Toys/Crayon.prefab` | 8.3 × 1 × 1 | Toy, rigidbody 5 g, grabbable, Box collider | MIT |
| `Toys/Croissant.prefab` | 4.8 × 1.8 × 1.7 | Toy, rigidbody 10 g, grabbable, Box collider | MIT |
| `Toys/Foam cube.prefab` | 2.4 × 2.3 × 2.4 | Toy, rigidbody 4 g, grabbable, Box collider | MIT |
| `Toys/Grape.prefab` | 1.7 × 2.3 × 1.7 | Toy, rigidbody 4 g, grabbable, Box collider | MIT |
| `Toys/Green crayon.prefab` | 8.3 × 1 × 1 | Toy, rigidbody 5 g, grabbable, Box collider | MIT |
| `Toys/Key.prefab` | 5.2 × 0.3 × 2.3 | Toy, rigidbody 4 g, grabbable, Box collider | MIT |
| `Toys/Leaf.prefab` | 2.4 × 0.4 × 6.7 | Toy, rigidbody 0.8 g, grabbable, Box collider | MIT |
| `Toys/Mint duck.prefab` | 3 × 3.6 × 5.1 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Pebble.prefab` | 2.8 × 1.5 × 2.2 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Pencil.prefab` | 12.4 × 0.9 × 0.9 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Pink duck.prefab` | 3 × 3.6 × 5.1 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Red crayon.prefab` | 8.3 × 1 × 1 | Toy, rigidbody 5 g, grabbable, Box collider | MIT |
| `Toys/Ring.prefab` | 4.4 × 1.3 × 4.4 | Toy, rigidbody 8 g, grabbable, Box collider | MIT |
| `Toys/Rubber duck.prefab` | 3 × 3.6 × 5.1 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Sock ball.prefab` | 6 × 6.2 × 6 | Prop, rigidbody 25 g, Sphere collider | MIT |
| `Toys/Strawberry.prefab` | 2.2 × 3.2 × 2.2 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Toast.prefab` | 7.2 × 0.7 × 6.4 | Toy, rigidbody 10 g, grabbable, Box collider | MIT |
| `Toys/Toy brick.prefab` | 1.6 × 1.1 × 1.6 | Toy, rigidbody 2 g, grabbable, Box collider | MIT |
| `Toys/Toy car.prefab` | 5.1 × 3.6 × 8 | Prop, rigidbody 30 g, Box collider | MIT |
| `Toys/Toy figure.prefab` | 2.7 × 3.9 × 1.8 | Toy, rigidbody 6 g, grabbable, Box collider | MIT |
| `Toys/Wicker ball.prefab` | 6.2 × 6.2 × 6.2 | Prop, rigidbody 20 g, Sphere collider | MIT |

## Furniture (36)

| Item | Size (cm, W × H × D) | Physics | Licence |
|---|---|---|---|
| `Furniture/Armchair.prefab` | 88.2 × 82.6 × 85.7 | Default, static, Box collider, surface tag | MIT |
| `Furniture/Beanbag.prefab` | 76.6 × 48.6 × 76.6 | World, static, Box collider, surface tag | MIT |
| `Furniture/Bed.prefab` | 193.9 × 61.2 × 107.1 | World, static, Box colliders, surface tag | MIT |
| `Furniture/Bench.prefab` | 120.1 × 71.4 × 34.5 | World, static, Box collider, surface tag | MIT |
| `Furniture/Bookcase 2.prefab` | 33.1 × 91.5 × 91.7 | World, static, Box collider | MIT |
| `Furniture/Bookcase.prefab` | 38.3 × 160.3 × 112.2 | World, static, Box collider | MIT |
| `Furniture/Coffee table.prefab` | 101.2 × 40.9 × 50.7 | Default, static, Box colliders, surface tag | MIT |
| `Furniture/Console table.prefab` | 90.3 × 72.1 × 30.4 | World, static, Box collider, surface tag | MIT |
| `Furniture/Desk chair.prefab` | 38.6 × 88.1 × 38.5 | World, static, Box collider, surface tag | MIT |
| `Furniture/Desk.prefab` | 55.3 × 64.2 × 100.2 | World, static, Box collider, surface tag | MIT |
| `Furniture/Floor lamp.prefab` | 41 × 153.7 × 41 | World, static, Box collider | MIT |
| `Furniture/Floor plant.prefab` | 46 × 83.6 × 44.8 | World, static, Mesh collider, surface tag | MIT |
| `Furniture/Fridge.prefab` | 75.9 × 188.8 × 71.6 | World, static, Box collider | MIT |
| `Furniture/Hose reel.prefab` | 18.1 × 32 × 31.7 | World, static, Box collider | MIT |
| `Furniture/Kitchen chair 2.prefab` | 40.6 × 88.9 × 40.4 | World, static, Box+Mesh colliders, surface tag | MIT |
| `Furniture/Kitchen chair 3.prefab` | 40.4 × 89 × 40.5 | World, static, Box+Mesh colliders, surface tag | MIT |
| `Furniture/Kitchen chair 4.prefab` | 40.6 × 89.1 × 40.6 | World, static, Box+Mesh colliders, surface tag | MIT |
| `Furniture/Kitchen chair.prefab` | 40.7 × 89.2 × 40.2 | World, static, Box+Mesh colliders, surface tag | MIT |
| `Furniture/Kitchen table.prefab` | 125.9 × 76.9 × 81.2 | World, static, Box+Mesh colliders, surface tag | MIT |
| `Furniture/Kitchen units.prefab` | 266 × 212.7 × 67.9 | World, static, Box colliders, surface tag | MIT |
| `Furniture/Laundry basket.prefab` | 45 × 38 × 43 | World, static, Box collider | MIT |
| `Furniture/Pet bed.prefab` | 69 × 14 × 69 | World, static, Box collider, surface tag | MIT |
| `Furniture/Pouf.prefab` | 60 × 20 × 58.5 | World, static, Mesh collider, surface tag | MIT |
| `Furniture/Raised bed 2.prefab` | 100.4 × 41 × 30.5 | World, static, Box collider | MIT |
| `Furniture/Raised bed.prefab` | 100.4 × 41.2 × 30.5 | World, static, Box collider | MIT |
| `Furniture/Shelf.prefab` | 130.7 × 48.3 × 36.3 | World, static, Box collider, surface tag | MIT |
| `Furniture/Shoe rack.prefab` | 80.8 × 32.2 × 30.3 | World, static, Box collider, surface tag | MIT |
| `Furniture/Side table.prefab` | 44 × 52.5 × 44 | World, static, Mesh collider, surface tag | MIT |
| `Furniture/Sofa.prefab` | 213.1 × 80.8 × 94.7 | Default, static, Box colliders, surface tag | MIT |
| `Furniture/Step stool.prefab` | 38.6 × 25.4 × 31.8 | World, static, Box collider, surface tag | MIT |
| `Furniture/TV console.prefab` | 130.5 × 42.2 × 38.3 | World, static, Box collider, surface tag | MIT |
| `Furniture/Table.prefab` | 161.1 × 75.8 × 100.9 | World, static, Box colliders, surface tag | MIT |
| `Furniture/Toy box.prefab` | 67.6 × 52.9 × 68.3 | World, static, Box collider | MIT |
| `Furniture/Trash bin.prefab` | 36 × 48.8 × 37.3 | World, static, Box collider | MIT |
| `Furniture/Umbrella stand.prefab` | 25 × 83 × 25 | World, static, Box collider | MIT |
| `Furniture/Wardrobe.prefab` | 94.4 × 184.1 × 59.5 | World, static, Box collider | MIT |

## Clutter (172)

| Item | Size (cm, W × H × D) | Physics | Licence |
|---|---|---|---|
| `Clutter/Apple.prefab` | 7.2 × 7.9 × 7.2 | Prop, rigidbody 128.3 g, Sphere collider | MIT |
| `Clutter/Backpack.prefab` | 32 × 43 × 22.4 | World, static, Box collider | MIT |
| `Clutter/Ball 2.prefab` | 8 × 8 × 8 | Prop, rigidbody 21.2 g, Sphere collider | MIT |
| `Clutter/Ball 3.prefab` | 6.8 × 6.8 × 6.8 | Prop, rigidbody 15 g, grabbable, Sphere collider | MIT |
| `Clutter/Ball 4.prefab` | 5.8 × 5.8 × 5.8 | Prop, rigidbody 15 g, grabbable, Sphere collider | MIT |
| `Clutter/Ball 5.prefab` | 7 × 7 × 7 | Prop, rigidbody 15 g, grabbable, Sphere collider | MIT |
| `Clutter/Ball 6.prefab` | 7.4 × 7.3 × 7.4 | Prop, rigidbody 16.7 g, Sphere collider | MIT |
| `Clutter/Ball 7.prefab` | 18.2 × 18 × 18.2 | Prop, rigidbody 248 g, Sphere collider | MIT |
| `Clutter/Ball.prefab` | 6.4 × 6.4 × 6.4 | Prop, rigidbody 15 g, grabbable, Sphere collider | MIT |
| `Clutter/Book 2.prefab` | 5.1 × 17.1 × 13.3 | World, static | MIT |
| `Clutter/Book 3.prefab` | 4.1 × 17.7 × 13.8 | World, static | MIT |
| `Clutter/Book 4.prefab` | 4.5 × 25.6 × 15 | World, static | MIT |
| `Clutter/Book 5.prefab` | 4.8 × 23.7 × 15.7 | World, static | MIT |
| `Clutter/Book 6.prefab` | 4.4 × 27.7 × 16.7 | World, static | MIT |
| `Clutter/Book 7.prefab` | 3.4 × 21.3 × 13.8 | World, static | MIT |
| `Clutter/Book 8.prefab` | 3.1 × 24.7 × 13.3 | World, static | MIT |
| `Clutter/Book block 2.prefab` | 20.7 × 20.5 × 10.8 | World, static | MIT |
| `Clutter/Book block 3.prefab` | 20.7 × 20.5 × 11.1 | World, static | MIT |
| `Clutter/Book block 4.prefab` | 20.5 × 20.6 × 10.8 | World, static | MIT |
| `Clutter/Book block 5.prefab` | 20.7 × 20.5 × 10.7 | World, static | MIT |
| `Clutter/Book block 6.prefab` | 20.6 × 20.6 × 10.7 | World, static | MIT |
| `Clutter/Book block.prefab` | 20.6 × 20.5 × 10.8 | World, static | MIT |
| `Clutter/Book.prefab` | 3.8 × 23.6 × 13.7 | World, static | MIT |
| `Clutter/Bucket.prefab` | 24 × 31 × 24 | World, static, Box collider | MIT |
| `Clutter/Calendar.prefab` | 32.2 × 45.1 × 1.4 | World, static | MIT |
| `Clutter/Cardboard box 2.prefab` | 31 × 24.2 × 29.6 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 3.prefab` | 35.6 × 27.7 × 33.8 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 4.prefab` | 30.5 × 24.6 × 25.2 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 5.prefab` | 26.6 × 20.1 × 26.4 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 6.prefab` | 33.9 × 27.6 × 29.8 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 7.prefab` | 32.9 × 22.6 × 27.3 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cardboard box 8.prefab` | 40.5 × 25.5 × 40.4 | World, static | MIT |
| `Clutter/Cardboard box.prefab` | 32.9 × 27.7 × 32.2 | World, static, Box collider, surface tag | MIT |
| `Clutter/Cereal box 2.prefab` | 19.2 × 26 × 7.6 | World, static | MIT |
| `Clutter/Cereal box.prefab` | 19.2 × 30 × 7.6 | World, static | MIT |
| `Clutter/Chew bone.prefab` | 14.2 × 2.6 × 4.8 | Prop, rigidbody 37.2 g, Box collider | MIT |
| `Clutter/Clock.prefab` | 32 × 32 × 4 | World, static | MIT |
| `Clutter/Crayon 2.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon 3.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon 4.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon 5.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon 6.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon 7.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Crayon.prefab` | 1.4 × 1.3 × 8.5 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Desk lamp.prefab` | 14 × 34.1 × 26.9 | World, static | MIT |
| `Clutter/Dish rack.prefab` | 30.1 × 19 × 20.1 | World, static | MIT |
| `Clutter/Doormat.prefab` | 90.3 × 1.4 × 45.4 | World, static | MIT |
| `Clutter/Drawing 2.prefab` | 21.2 × 0.4 × 29.1 | World, static | MIT |
| `Clutter/Drawing 3.prefab` | 21.2 × 0.4 × 29.1 | World, static | MIT |
| `Clutter/Drawing 4.prefab` | 21.2 × 0.4 × 29.1 | World, static | MIT |
| `Clutter/Drawing 5.prefab` | 21.2 × 0.4 × 29.1 | World, static | MIT |
| `Clutter/Drawing.prefab` | 21.2 × 0.4 × 29.1 | World, static | MIT |
| `Clutter/Fallen leaf 2.prefab` | 8 × 0.6 × 4.5 | World, static | MIT |
| `Clutter/Fallen leaf 3.prefab` | 8 × 0.6 × 4.5 | World, static | MIT |
| `Clutter/Fallen leaf.prefab` | 8 × 0.6 × 4.5 | World, static | MIT |
| `Clutter/Floor cushion 2.prefab` | 45.5 × 17.8 × 45.5 | World, static, Box collider, surface tag | MIT |
| `Clutter/Floor cushion.prefab` | 45.5 × 17.8 × 45.5 | World, static, Box collider, surface tag | MIT |
| `Clutter/Floor mat.prefab` | 90 × 0.6 × 50 | World, static | MIT |
| `Clutter/Flower 2.prefab` | 12.8 × 28.1 × 11.5 | World, static | MIT |
| `Clutter/Flower 3.prefab` | 12.6 × 27.6 × 11.3 | World, static | MIT |
| `Clutter/Flower 4.prefab` | 14.5 × 31.8 × 13 | World, static | MIT |
| `Clutter/Flower 5.prefab` | 13.4 × 29.4 × 12 | World, static | MIT |
| `Clutter/Flower 6.prefab` | 13 × 28.5 × 11.7 | World, static | MIT |
| `Clutter/Flower 7.prefab` | 12.5 × 27.5 × 11.2 | World, static | MIT |
| `Clutter/Flower 8.prefab` | 12.7 × 27.9 × 11.4 | World, static | MIT |
| `Clutter/Flower.prefab` | 14.7 × 32.3 × 13.2 | World, static | MIT |
| `Clutter/Football 2.prefab` | 5.8 × 5.6 × 5.5 | Prop, rigidbody 40 g, grabbable, Sphere collider | MIT |
| `Clutter/Football 3.prefab` | 10 × 9.5 × 9.5 | Prop, rigidbody 40 g, Sphere collider | MIT |
| `Clutter/Football goal 2.prefab` | 35.5 × 16.7 × 13.2 | World, static, Box colliders | MIT |
| `Clutter/Football goal.prefab` | 35.5 × 16.8 × 13.2 | World, static, Box colliders | MIT |
| `Clutter/Football.prefab` | 7.3 × 6.9 × 6.8 | Prop, rigidbody 40 g, grabbable, Sphere collider | MIT |
| `Clutter/Fruit bowl.prefab` | 30.5 × 6.6 × 30.5 | World, static | MIT |
| `Clutter/Game console.prefab` | 22.1 × 5.3 × 16.2 | World, static | MIT |
| `Clutter/Game pad.prefab` | 14.2 × 3 × 8 | Prop, rigidbody 71.1 g, Box collider | MIT |
| `Clutter/Garden gnome.prefab` | 14 × 32.1 × 13.8 | World, static, Box collider | MIT |
| `Clutter/Jar 2.prefab` | 7.6 × 12.5 × 7.6 | World, static | MIT |
| `Clutter/Jar 3.prefab` | 7.6 × 15.5 × 7.6 | World, static | MIT |
| `Clutter/Jar 4.prefab` | 7.6 × 12.9 × 7.6 | World, static | MIT |
| `Clutter/Jar.prefab` | 7.6 × 11.9 × 7.6 | World, static | MIT |
| `Clutter/Kettle.prefab` | 19 × 21 × 26.4 | World, static | MIT |
| `Clutter/Key bowl.prefab` | 20 × 5.6 × 19.7 | World, static | MIT |
| `Clutter/Laptop.prefab` | 34.3 × 24.3 × 30.7 | Prop, rigidbody 4000 g, Box collider | MIT |
| `Clutter/Laundry pile.prefab` | 56.4 × 26.5 × 47.7 | World, static, Box collider, surface tag | MIT |
| `Clutter/Lego brick 2.prefab` | 3.2 × 2.2 × 6.4 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Lego brick 3.prefab` | 3.2 × 2.2 × 6.4 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Lego brick 4.prefab` | 3.2 × 2.2 × 6.4 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Lego brick.prefab` | 3.2 × 2.2 × 6.4 | Prop, rigidbody 15 g, grabbable, Box collider | MIT |
| `Clutter/Magazine 2.prefab` | 21.2 × 1.1 × 28.1 | Prop, rigidbody 268.4 g, Box collider | MIT |
| `Clutter/Magazine 3.prefab` | 21.2 × 1.1 × 28.1 | World, static | MIT |
| `Clutter/Magazine 4.prefab` | 21.2 × 1.1 × 28.1 | World, static | MIT |
| `Clutter/Magazine.prefab` | 21.2 × 1.1 × 28.1 | Prop, rigidbody 268.4 g, Box collider | MIT |
| `Clutter/Magnet 2.prefab` | 7.7 × 6.1 × 1.2 | World, static | MIT |
| `Clutter/Magnet 3.prefab` | 6 × 6 × 1.5 | World, static | MIT |
| `Clutter/Magnet 4.prefab` | 8 × 6.5 × 1.2 | World, static | MIT |
| `Clutter/Magnet 5.prefab` | 7.4 × 5.5 × 1.2 | World, static | MIT |
| `Clutter/Magnet.prefab` | 6 × 6 × 1.5 | World, static | MIT |
| `Clutter/Mop.prefab` | 17.7 × 109.9 × 7.1 | World, static | MIT |
| `Clutter/Mug 2.prefab` | 11.6 × 9.2 × 8.4 | Prop, rigidbody 188.3 g, Box collider | MIT |
| `Clutter/Mug 3.prefab` | 11.6 × 9.2 × 8.4 | World, static | MIT |
| `Clutter/Mug 4.prefab` | 11.6 × 9.2 × 8.4 | World, static | MIT |
| `Clutter/Mug 5.prefab` | 11.6 × 9.2 × 8.4 | World, static | MIT |
| `Clutter/Mug.prefab` | 11.6 × 9.2 × 8.4 | Prop, rigidbody 188.3 g, Box collider | MIT |
| `Clutter/Notebook.prefab` | 21.1 × 1.7 × 15.1 | World, static, Box collider, surface tag | MIT |
| `Clutter/Orange.prefab` | 7.2 × 8.1 × 7.2 | Prop, rigidbody 131.5 g, Sphere collider | MIT |
| `Clutter/Pencil pot.prefab` | 8 × 19 × 8 | World, static | MIT |
| `Clutter/Pet bowl 2.prefab` | 17.2 × 5.2 × 17.2 | Prop, rigidbody 430.7 g, Box collider | MIT |
| `Clutter/Pet bowl.prefab` | 17.2 × 5.2 × 17.2 | Prop, rigidbody 430.7 g, Box collider | MIT |
| `Clutter/Picture 2.prefab` | 50 × 38.2 × 3.8 | World, static | MIT |
| `Clutter/Picture 3.prefab` | 50.1 × 38.3 × 3.6 | World, static | MIT |
| `Clutter/Picture 4.prefab` | 50.3 × 38.3 × 3.5 | World, static | MIT |
| `Clutter/Picture 5.prefab` | 50.1 × 38.3 × 3.7 | World, static | MIT |
| `Clutter/Picture 6.prefab` | 50.5 × 38.2 × 3.6 | World, static | MIT |
| `Clutter/Picture.prefab` | 50.2 × 38.2 × 3.5 | World, static | MIT |
| `Clutter/Plant 2.prefab` | 20.8 × 29.9 × 17 | World, static | MIT |
| `Clutter/Plant 3.prefab` | 43.8 × 57.5 × 33.7 | World, static, Box collider, surface tag | MIT |
| `Clutter/Plant.prefab` | 13.3 × 23.9 × 15.4 | World, static | MIT |
| `Clutter/Plate 2.prefab` | 20 × 1.8 × 20 | World, static | MIT |
| `Clutter/Plate 3.prefab` | 20 × 1.8 × 20 | World, static | MIT |
| `Clutter/Plate 4.prefab` | 20 × 1.1 × 20 | World, static | MIT |
| `Clutter/Plate.prefab` | 20 × 1.8 × 20 | World, static | MIT |
| `Clutter/Plush bunny 2.prefab` | 14 × 38.3 × 19 | Prop, rigidbody 427.6 g, Box collider | MIT |
| `Clutter/Plush bunny.prefab` | 12.6 × 34.4 × 17.1 | Prop, rigidbody 311.7 g, Box collider | MIT |
| `Clutter/Poster 2.prefab` | 49.2 × 64.1 × 2.8 | World, static | MIT |
| `Clutter/Poster 3.prefab` | 54.2 × 44.1 × 2.8 | World, static | MIT |
| `Clutter/Poster.prefab` | 74.2 × 54.1 × 2.7 | World, static | MIT |
| `Clutter/Potted bush 2.prefab` | 48 × 65.4 × 47.8 | World, static, Mesh collider, surface tag | MIT |
| `Clutter/Potted bush 3.prefab` | 36 × 48.5 × 35.1 | World, static, Mesh collider, surface tag | MIT |
| `Clutter/Potted bush 4.prefab` | 34 × 45.7 × 33.1 | World, static, Mesh collider, surface tag | MIT |
| `Clutter/Potted bush.prefab` | 40 × 54.1 × 39 | World, static, Mesh collider, surface tag | MIT |
| `Clutter/Remote.prefab` | 5.1 × 2.3 × 18 | Prop, rigidbody 58.8 g, Box collider | MIT |
| `Clutter/Scooter.prefab` | 40.2 × 73.1 × 52.8 | World, static, Box collider | MIT |
| `Clutter/Shoe 2.prefab` | 10.5 × 7.5 × 26.4 | World, static, Box collider | MIT |
| `Clutter/Shoe 3.prefab` | 10.4 × 7.6 × 26.4 | World, static, Box collider | MIT |
| `Clutter/Shoe 4.prefab` | 8.1 × 7.5 × 21.6 | Prop, rigidbody 137.6 g, Box collider | MIT |
| `Clutter/Shoe 5.prefab` | 8.1 × 7.5 × 21.6 | Prop, rigidbody 137.6 g, Box collider | MIT |
| `Clutter/Shoe 6.prefab` | 8.1 × 7.5 × 21.6 | Prop, rigidbody 137.6 g, Box collider | MIT |
| `Clutter/Shoe 7.prefab` | 8.1 × 7.5 × 21.6 | Prop, rigidbody 137.6 g, Box collider | MIT |
| `Clutter/Shoe 8.prefab` | 10.2 × 9.4 × 27 | Prop, rigidbody 270.6 g, Box collider | MIT |
| `Clutter/Shoe.prefab` | 10.2 × 9.4 × 27 | Prop, rigidbody 270.6 g, Box collider | MIT |
| `Clutter/Shopping bag 2.prefab` | 30.3 × 50.9 × 16.4 | World, static, Box collider | MIT |
| `Clutter/Shopping bag.prefab` | 30.3 × 50.9 × 16.4 | World, static, Box collider | MIT |
| `Clutter/Skateboard.prefab` | 20.3 × 5 × 72.1 | Prop, rigidbody 617.7 g, Box collider | MIT |
| `Clutter/Sock 2.prefab` | 6.3 × 2.7 × 21.5 | Prop, rigidbody 15.4 g, Box collider | MIT |
| `Clutter/Sock.prefab` | 6.3 × 2.7 × 21.5 | Prop, rigidbody 15.4 g, Box collider | MIT |
| `Clutter/Spade.prefab` | 7 × 1.4 × 29.8 | Prop, rigidbody 61.3 g, Box collider | MIT |
| `Clutter/Spoon.prefab` | 3.5 × 1.2 × 15 | Prop, rigidbody 35.3 g, Box collider | MIT |
| `Clutter/TV.prefab` | 96.2 × 64.1 × 16.1 | World, static | MIT |
| `Clutter/Teddy 2.prefab` | 35.6 × 42.5 × 25.6 | World, static | MIT |
| `Clutter/Teddy 3.prefab` | 25.9 × 30.9 × 18.6 | Prop, rigidbody 625.6 g, Box collider | MIT |
| `Clutter/Teddy.prefab` | 29.1 × 34.7 × 21 | Prop, rigidbody 890.8 g, Box collider | MIT |
| `Clutter/Tennis ball.prefab` | 6.6 × 6.6 × 6.8 | Prop, rigidbody 18.5 g, grabbable, Sphere collider | MIT |
| `Clutter/Toaster.prefab` | 26.7 × 21.3 × 16.6 | World, static | MIT |
| `Clutter/Toy block 2.prefab` | 5.3 × 5 × 5.3 | Prop, rigidbody 24.9 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 3.prefab` | 5.9 × 5.6 × 5.9 | Prop, rigidbody 34.4 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 4.prefab` | 5.4 × 5.1 × 5.4 | Prop, rigidbody 26.3 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 5.prefab` | 4.7 × 4.4 × 4.7 | Prop, rigidbody 17.1 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 6.prefab` | 5.3 × 5 × 5.3 | Prop, rigidbody 25.1 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 7.prefab` | 6.6 × 6.3 × 6.6 | Prop, rigidbody 48 g, grabbable, Box collider | MIT |
| `Clutter/Toy block 8.prefab` | 6.6 × 6.3 × 6.6 | Prop, rigidbody 48 g, grabbable, Box collider | MIT |
| `Clutter/Toy block.prefab` | 4.7 × 4.4 × 4.7 | Prop, rigidbody 17.2 g, grabbable, Box collider | MIT |
| `Clutter/Toy car 2.prefab` | 8.3 × 5.9 × 13.2 | Prop, rigidbody 90.2 g, grabbable, Box collider | MIT |
| `Clutter/Toy car 3.prefab` | 8.3 × 5.9 × 13.2 | Prop, rigidbody 90.2 g, grabbable, Box collider | MIT |
| `Clutter/Toy car 4.prefab` | 8.3 × 5.9 × 13.2 | Prop, rigidbody 90.2 g, grabbable, Box collider | MIT |
| `Clutter/Toy car 5.prefab` | 8.3 × 5.9 × 13.2 | Prop, rigidbody 90.2 g, grabbable, Box collider | MIT |
| `Clutter/Toy car.prefab` | 8.3 × 5.9 × 13.2 | Prop, rigidbody 90.2 g, grabbable, Box collider | MIT |
| `Clutter/Train coach.prefab` | 12.4 × 13.4 × 18 | Prop, kinematic body, Box collider | MIT |
| `Clutter/Train engine.prefab` | 12.4 × 17.5 × 26.1 | Prop, kinematic body, Box collider | MIT |
| `Clutter/Train track.prefab` | 125 × 1.4 × 124.6 | World, static | MIT |
| `Clutter/Train wagon.prefab` | 12.4 × 12.5 × 18.8 | Prop, kinematic body, Box collider | MIT |
| `Clutter/Vase 2.prefab` | 10.5 × 26.5 × 10 | World, static | MIT |
| `Clutter/Vase.prefab` | 10.5 × 26.5 × 10 | World, static | MIT |
| `Clutter/Watering can.prefab` | 20 × 30 × 35.9 | World, static, Box collider | MIT |

## Ducks (4)

**Non-commercial only:** every duck is adapted from Pollen Robotics' Microduck (CC BY-NC-SA 4.0).

| Item | Size (cm, W × H × D) | Physics | Licence |
|---|---|---|---|
| `Ducks/Duck graphite.prefab` | 18.2 × 27.6 × 14.5 | Duck, static | CC BY-NC-SA 4.0, **non-commercial** |
| `Ducks/Duck lavender.prefab` | 18.2 × 27.6 × 14.5 | Duck, static | CC BY-NC-SA 4.0, **non-commercial** |
| `Ducks/Duck sky.prefab` | 18.2 × 27.6 × 14.5 | Duck, static | CC BY-NC-SA 4.0, **non-commercial** |
| `Ducks/Duck.prefab` | 18.2 × 27.6 × 14.5 | Duck, static | CC BY-NC-SA 4.0, **non-commercial** |

## Ducks/Live (in this project only, not in the package)

| Item | What it is | Licence |
|---|---|---|
| `Ducks/Live/Duck (live).prefab` | The game's runtime duck (DuckBody, DuckSetup, WanderDuck). It builds its model from `Models/microduck_rig.glb` when Play starts, so it shows nothing in edit mode. Needs a floor collider, ideally on the World layer (6); it wanders inside x, z in [-1, 1]. | CC BY-NC-SA 4.0, **non-commercial** |

