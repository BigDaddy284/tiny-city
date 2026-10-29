# Art style guide (locked)

The owner approved the Tiny City cyberpunk style and palette on 29 October 2026, replacing the cosy village look
approved on 28 October. Everything drawn for Tiny City (residents, animation, effects, menus that show art) follows
these rules and uses only the colours below. Changing a rule or a colour needs the owner's approval first.

Approved changes:
- 28 October 2026: the back walls of rooms meet in a clean corner.
- 29 October 2026: the game became Tiny City. The neon palette is taken from the owner's reference picture: navy
  and purple streets with magenta, violet, blue and teal blocks lit by cyan, pink, yellow and mint neon. Homes and
  shops became flat-roofed blocks. Nothing sticks out from a wall: no awnings, signs or blades, so doors and lines
  stay clean.

The lock is enforced by `Assets/Tests/EditMode/ArtStyleTests.cs`. It fingerprints the palette, the style rules and
the drawn ground, buildings, rooms, trees and furniture. Any change to them makes a test fail. Once the owner
approves a change, update this guide and the fingerprint in the same commit.

![The locked palette](Images/palette.png)

## Look and feel

- **Small neon city.** Dark navy and purple surfaces with saturated neon accents. Buildings are bold blocks in
  magenta, violet, blue and teal; neon trim, signs and lit windows make the evening and night glow. Each building
  takes its own colours, so no two neighbours match.
- **Pixel art, drawn by the game's code** (`Assets/Scripts/Art`). No smoothing: textures use point filtering, and
  every pixel is a solid colour except shadows and glass, which are see-through.
- **Hand-made, not perfect.** Edges between paving, streets and water wander slightly. Plates and wall panels vary
  a little in shade, chosen by `Noise` from their position, so the same city always looks the same.

## Projection and size

- **Isometric 2:1.** One tile is a 64 × 32 pixel diamond (`IsoPainter.HalfTileWidth` 32, `HalfTileHeight` 16),
  shown at 64 pixels per world unit. A map's x runs down to the right and its y runs down to the left.
- **Heights are in screen pixels.** Homes are 58 pixels tall (two floors) and shops 66, both with flat roofs and a
  4-pixel parapet. Room back walls are 64 pixels tall, front walls 12 and inner walls 40. Each map sits on a
  10-pixel block (wooden boards for rooms), like a diorama.
- **Things stand on their tile.** A picture's anchor is the centre of the tile it stands on. Furniture and trees
  fit within a 72 × 104 canvas. Buildings fill their footprint, inset slightly, and nothing hangs off their walls.

## Light and shade

- **Light comes from the upper left.** Box faces are lit at 1.0 for the top, 0.84 for the left face (facing +y) and
  0.68 for the right face (facing +x) (`IsoPainter.TopLight`, `LeftLight`, `RightLight`).
- **Shadows fall down and to the right.** They are soft and see-through: `Shadow` ellipses under trees and outdoor
  objects, and a `ShadowTint` wash on the ground beside buildings.
- **Outlines.** Trees, furniture and room walls have a one-pixel outline: the edge pixel is mixed 65% towards
  `Outline` (#140f2b), a deep navy.
- **Day and night.** Sprites are multiplied by `Daylight` by day, `GoldenHour` (a pink dusk) at dawn and dusk
  (90-minute twilight) and `Moonlight` (violet) at night. Neon (roof edges, wall plinths, door frames, shop tubes
  and signs, antenna lights) and lit windows (`WindowGlow` amber or `GlassLight` cyan) are on the glow layer: they
  fade in after dusk and are never darkened. Rooms turn to `Lamplight` instead of going dark. Sunrise and sunset
  move with the season (`Lighting.SunTimes`).

## Seasons

A year is 28 days, with four 7-day seasons, spring first. Only the outdoors changes:

- **Spring:** teal trees, cyan kerbs and cyan, pink and yellow lights in the paving.
- **Summer:** hot-pink trees and pink kerbs.
- **Autumn:** orange and gold trees and orange kerbs.
- **Winter:** pale snow on the paving, roofs and pines, bare dark trees, a frozen pond and cyan kerbs.

## Places

- **Streets and paving:** smooth tarmac streets with a neon kerb in the season's colour, and square paving plates
  everywhere else, with the odd light set into them.
- **Homes:** two-floor blocks with panel seams and a line between floors. Ground-floor windows, a long strip window
  upstairs, sliding glass doors with a neon frame, a dark plinth with a neon strip, neon corners and a neon roof
  edge in the home's door colour. On the roof: an air unit, a vent and an antenna with a red light.
- **Shops:** taller blocks with large display windows (goods behind the glass), a neon tube along the shopfront and
  a neon sign flat above the door with a picture: an apple (store), a cup (café), a loaf (bakery), a red cross
  (clinic) or a cocktail glass (the violet neon bar). Each shop has its own neon colour.
- **Apartment towers:** six floors (three times a home's height, `BuildingArt.TowerHeight`) of navy or charcoal
  panels with a line between floors and a window for each room on every floor, lit one by one at night. A glass
  door to each ground-floor flat along the front and up the right side, in cyan or violet neon, and no sign.
- **Venues:** shop-style blocks with their own neon and sign: an eight ball on olive (the lime cue club), a red-banded
  skittle on wine (amber Strike Lanes), a lime space invader on plum (the pink Pixel Arcade) and a dumbbell on slate
  (the mint Pulse Gym). Inside: olive walls and dark boards with teal pool tables and a moss snooker table; wine
  walls and pale boards with three lanes ending in pins and a ball return; navy walls, a plum checker floor and four
  cabinets with glowing screens and neon marquees; a slate gym with treadmills.
- **Outdoors:** striped market stalls laid out with goods, and a stone fountain with water in the season's colour
  (snow in winter).
- **Rooms:** doll's-house cutaways. The two back walls are tall and the same height, meeting in a clean inside
  corner, with dark wall panels, a pink neon rail at waist height, a cyan LED skirting, windows showing the lit
  city outside, and pictures. The front walls are low with a cyan top edge, and walls inside the room are half
  height. Floors are dark boards in homes and the café, and tiles in the store, bakery and clinic; the bar has dark slate tiles, a light-up dance floor and a DJ booth.

## Residents

![The ten residents in every pose, then a child, a teenager and a baby, with the mood icons](Images/residents.png)

- **Size and shape.** A resident is about 33 pixels tall from feet to the top of the head (doors are about 30), with
  a head 8 pixels wide, a body 8 wide and arms 2 wide each side. They are drawn in three-quarter view and face one
  of the four walking directions: towards the viewer down-right or down-left, or away up-left or up-right.
- **Same light as the town.** The light still comes from the upper left whichever way they face, so the right-hand
  columns are a shade darker. They have the same `Outline` and stand on a soft `Shadow` when standing.
- **Techwear with neon piping.** Everyone wears dark techwear (the `Tech…` colours, `CargoGrey`, `LabCoat`) with
  piping in one neon colour (the `Neon…` colours): the collar, a seam down the front (a stripe across the shoulders
  from behind), the cuffs, the belt or skirt hem, apron hems and the toes of their trainers. Some wear a glowing
  visor across the eyes instead of glasses, neon-dyed hair tips, or a long coat that hangs open to the knees.
  Approved by the owner on 29 October.
- **Ten looks.** Each starter resident has their own techwear and piping colours, hair colour and style, and a touch
  of their own: Mabel's cap and mint hair tips, Arthur's long navy coat and glasses, Nell's cyan visor, Rosa's curls
  with pink tips and her apron, Tom's white chef's jacket and beard, Priya's long hair with violet tips, Ivy's bob
  with pink tips and her apron, Samuel's lab coat, glasses and beard, Felix's violet visor and cyan tips, Edith's
  long plum coat, white bun and glasses. Anyone new gets a look put together from their id (piping always, a visor,
  dyed tips under 40 or a coat from 50 now and then), going grey at 65 and white at 75 (`ResidentLook`).
- **Children.** Children born in the city are the same figure with rows taken out between the neck and the ankles,
  so the head keeps its size: 9 rows shorter from 3 to 12, 4 rows shorter as a teenager, with a smaller shadow and
  no beard (`ResidentLook.Shrink`). A baby under 3 is a little bundle in their techwear colour, piped in their neon,
  held at chest height beside whoever carries them, or tucked up low beside them in bed.
- **Animation.** Four walking frames (a stride, a lifted foot, the other stride, the other foot) with a one-pixel
  bob, and two frames each for talking (mouth and a hand gesture), eating, reading, working, cheering and star
  jumps. Sitting puts the hips on the seat with the legs hanging down; sleeping shows the head on the pillow and the
  shoulders above the quilt (someone with nowhere to live lies along a park bench). People use furniture where it is used: sitting in chairs and on benches, standing at
  the front of cookers, sinks, counters and workbenches, facing them.
- **Mood icons.** A small pale bubble with a tail over the head, outlined, holding a 7 × 5 symbol: a smile
  (happy), "z" (asleep), a green face (ill), "!" (needs the toilet), bread (hungry), a drop (thirsty), heavy eyes
  (tired), wavy lines (grubby), a rain cloud (lonely), "..." (bored) and a heart (talking with their partner).
  Icons are never darkened by night.

## For later work

- Use only colours from `Palette.cs`. A new colour must be approved and added there by name.
- Use the same light direction, outline colour, shadows and point-filtered pixels.

## Palette

Every colour is named in `Assets/Scripts/Art/Palette.cs`, the only file where colours are written down. Shades of
these colours (for example a face lit at 0.84) are made by `Rgba.Shade` and `Rgba.Mix`. Seasonal colours are listed
once for each season. This table and `Images/palette.png` are made by `Tools/ArtPreview` (`dotnet run -- --palette
../../Documentation/Images/palette.png` in that folder).

| Name | Colour |
| --- | --- |
| Outline | `#140f2b` |
| Shadow | `#281e3246` |
| ShadowTint | `#1a1236` |
| Highlight | `#ffffff` |
| Earth | `#2a2048` |
| EarthDark | `#1a1433` |
| Wood | `#4a3a6e` |
| WoodDark | `#2e2350` |
| WoodLight | `#6f5a9c` |
| Stone | `#4b4775` |
| StoneDark | `#33305a` |
| Brick | `#3c2f63` |
| White | `#e6e8ff` |
| Porcelain | `#eef3f5` |
| PorcelainShade | `#dfe7ea` |
| Metal | `#7f88b8` |
| Brass | `#ffcf5a` |
| Iron | `#15122a` |
| Charcoal | `#221d3d` |
| Slate | `#2c2650` |
| SlateLight | `#463d78` |
| Ebony | `#3a2a2a` |
| Ivory | `#d8d4cc` |
| Glass | `#2a4fa8` |
| GlassLight | `#66d9ff` |
| ShowerGlass | `#aadceb` |
| DisplayGlass | `#c8e6f55a` |
| DisplayGlassTop | `#dcf0fa46` |
| WindowGlow | `#ffc15a` |
| Snow | `#e8ecff` |
| SnowShade | `#b8c2ec` |
| Sand | `#5a4a86` |
| IceCrack | `#8fb3cc` |
| Red | `#ff4f7a` |
| Blue | `#3a7bff` |
| Green | `#2fe0b0` |
| Yellow | `#ffd24a` |
| Purple | `#b45cff` |
| Cream | `#f0e6ff` |
| Tomato | `#ff5a6e` |
| Poppy | `#ff3d8b` |
| Pumpkin | `#ff9a3d` |
| Mustard | `#ffd24a` |
| Apple | `#3dffb4` |
| LeafGreen | `#19c9a0` |
| SignRed | `#ff2e6e` |
| Bread | `#ffb347` |
| Icing | `#ff9fe0` |
| Sky | `#4fd8ff` |
| Mat | `#2fb8ff` |
| MatDark | `#1f8fd0` |
| Moss | `#3a5a7a` |
| Chalkboard | `#14203a` |
| ClinicBlanket | `#5fc8ff` |
| Accents[0] | `#ff4f7a` |
| Accents[1] | `#3a7bff` |
| Accents[2] | `#2fe0b0` |
| Accents[3] | `#ffd24a` |
| Accents[4] | `#b45cff` |
| Bunting[0] | `#ff4f7a` |
| Bunting[1] | `#ffd24a` |
| Bunting[2] | `#3a7bff` |
| Bunting[3] | `#2fe0b0` |
| Goods[0] | `#ff4f7a` |
| Goods[1] | `#ffd24a` |
| Goods[2] | `#2fe0b0` |
| Goods[3] | `#f0e6ff` |
| Goods[4] | `#3a7bff` |
| Goods[5] | `#ffb347` |
| WindowGoods[0] | `#ff3d8b` |
| WindowGoods[1] | `#ffd24a` |
| WindowGoods[2] | `#3dffb4` |
| CottageWalls[0] | `#c2367f` |
| CottageWalls[1] | `#5a3fb5` |
| CottageWalls[2] | `#2f5fb8` |
| CottageWalls[3] | `#1f8f9a` |
| CottageWalls[4] | `#8a2fa0` |
| CottageWalls[5] | `#3a3f9a` |
| CottageRoofs[0] | `#1c1636` |
| CottageRoofs[1] | `#221a40` |
| CottageRoofs[2] | `#18203a` |
| CottageRoofs[3] | `#2a1838` |
| CottageRoofs[4] | `#201a3a` |
| CottageRoofs[5] | `#141a30` |
| CottageDoors[0] | `#35e0ff` |
| CottageDoors[1] | `#ff4fb0` |
| CottageDoors[2] | `#ffd24a` |
| CottageDoors[3] | `#3dffb4` |
| CottageDoors[4] | `#b45cff` |
| StoreWall | `#2b5f8f` |
| StoreRoof | `#1a2a4a` |
| StoreDoor | `#35e0ff` |
| StoreNeon | `#00e5c0` |
| CafeWall | `#8a2f7a` |
| CafeRoof | `#2a1640` |
| CafeDoor | `#ff5ab4` |
| CafeNeon | `#ff3d8b` |
| BakeryWall | `#9a5a2a` |
| BakeryRoof | `#2a1a30` |
| BakeryDoor | `#ffb347` |
| BakeryNeon | `#ff9a3d` |
| ClinicWall | `#d8e4ff` |
| ClinicRoof | `#2a3a6a` |
| ClinicDoor | `#35e0ff` |
| ShopNeon | `#7a5cff` |
| WallCap | `#120d24` |
| FrontWallOutside | `#3a2f63` |
| RoomWindowSky | `#2a1f5a` |
| RoomWindowSkyHigh | `#5a3a9a` |
| PictureFrame | `#35e0ff` |
| PictureLand | `#ff3d8b` |
| PictureSky | `#3a2a7a` |
| Doormat | `#ff3d8b` |
| DoormatDark | `#b02a6a` |
| StoreWallpaper | `#1f3a5f` |
| CafeWallpaper | `#4a1f4f` |
| BakeryWallpaper | `#4f2f2a` |
| ClinicWallpaper | `#c8d6f5` |
| ShopWallpaper | `#2f2a55` |
| CottageWallpapers[0] | `#2a2050` |
| CottageWallpapers[1] | `#3a1f4a` |
| CottageWallpapers[2] | `#1f2f4f` |
| CottageWallpapers[3] | `#2a2a5a` |
| CottageWallpapers[4] | `#3a2a3f` |
| StoreFloor[0] | `#1f2a48` |
| StoreFloor[1] | `#2a3d66` |
| ClinicFloor[0] | `#dfe8ff` |
| ClinicFloor[1] | `#b8c8f0` |
| BakeryFloor[0] | `#3a2430` |
| BakeryFloor[1] | `#2e1c26` |
| ShopFloor[0] | `#2a2648` |
| ShopFloor[1] | `#221e3d` |
| CafeFloor | `#2e2045` |
| CottageFloors[0] | `#2e2650` |
| CottageFloors[1] | `#272043` |
| CottageFloors[2] | `#363060` |
| Skin[0] | `#f6d9c2` |
| Skin[1] | `#ebc19c` |
| Skin[2] | `#d29a72` |
| Skin[3] | `#b47a52` |
| Skin[4] | `#7f5038` |
| Blush | `#e8a29a` |
| HairBlack | `#2e2626` |
| HairDarkBrown | `#4a3226` |
| HairBrown | `#6b4630` |
| HairAuburn | `#a4523a` |
| HairGinger | `#d0813e` |
| HairBlonde | `#e2c27a` |
| HairGrey | `#b9b4ad` |
| HairWhite | `#ece9e3` |
| Terracotta | `#d9734e` |
| Cornflower | `#5b7db1` |
| Fern | `#6fae5c` |
| Rose | `#c2577a` |
| Marigold | `#e0a33a` |
| Lavender | `#8e6cc0` |
| Buttercup | `#d4b44a` |
| Teal | `#3e8e8a` |
| Powder | `#7fa7d9` |
| Pebble | `#b0a8a0` |
| Denim | `#4d5f86` |
| Corduroy | `#7a5a3e` |
| Khaki | `#a8946a` |
| TechCharcoal | `#2c2e48` |
| TechNavy | `#283a70` |
| TechPlum | `#4e2860` |
| TechOlive | `#2f5048` |
| TechViolet | `#45348a` |
| TechWine | `#62223f` |
| TechPants | `#1f2136` |
| CargoGrey | `#41455c` |
| LabCoat | `#e2e8f6` |
| NeonCyan | `#35e0ff` |
| NeonPink | `#ff4fb0` |
| NeonLime | `#a6ff4d` |
| NeonAmber | `#ffb52e` |
| NeonViolet | `#b45cff` |
| NeonMint | `#3dffb4` |
| Clothes[0] | `#2c2e48` |
| Clothes[1] | `#283a70` |
| Clothes[2] | `#4e2860` |
| Clothes[3] | `#2f5048` |
| Clothes[4] | `#45348a` |
| Clothes[5] | `#62223f` |
| Bottoms[0] | `#1f2136` |
| Bottoms[1] | `#41455c` |
| Bottoms[2] | `#2c2e48` |
| Neons[0] | `#35e0ff` |
| Neons[1] | `#ff4fb0` |
| Neons[2] | `#a6ff4d` |
| Neons[3] | `#ffb52e` |
| Neons[4] | `#b45cff` |
| Neons[5] | `#3dffb4` |
| Hair[0] | `#2e2626` |
| Hair[1] | `#4a3226` |
| Hair[2] | `#6b4630` |
| Hair[3] | `#a4523a` |
| Hair[4] | `#d0813e` |
| Hair[5] | `#e2c27a` |
| Bubble | `#fffaf0` |
| Heart | `#e0566a` |
| Sleepy | `#6a86c9` |
| Queasy | `#8cbf6a` |
| Daylight | `#ffffff` |
| GoldenHour | `#ffb8d8` |
| Moonlight | `#7a6ad8` |
| Lamplight | `#e8d0ff` |
| Spring.Grass | `#2a2456` |
| Spring.GrassDark | `#221d48` |
| Spring.GrassLight | `#352e6a` |
| Spring.Leaves | `#22c7b4` |
| Spring.LeavesDark | `#138a86` |
| Spring.LeavesLight | `#6ff5dc` |
| Spring.PineLeaves | `#2a7ad0` |
| Spring.PineLeavesDark | `#1a4a96` |
| Spring.Accent | `#35e0ff` |
| Spring.Flowers[0] | `#35e0ff` |
| Spring.Flowers[1] | `#ff4fb0` |
| Spring.Flowers[2] | `#ffd24a` |
| Spring.Flowers[3] | `#b45cff` |
| Spring.Water | `#1d4fb0` |
| Spring.Path | `#4a3a86` |
| Spring.PathDark | `#33286a` |
| Summer.Grass | `#2c2254` |
| Summer.GrassDark | `#231b46` |
| Summer.GrassLight | `#3a2c6a` |
| Summer.Leaves | `#ff4fb0` |
| Summer.LeavesDark | `#b02a7e` |
| Summer.LeavesLight | `#ff9fe0` |
| Summer.PineLeaves | `#2a7ad0` |
| Summer.PineLeavesDark | `#1a4a96` |
| Summer.Accent | `#ff3d8b` |
| Summer.Flowers[0] | `#ff4fb0` |
| Summer.Flowers[1] | `#35e0ff` |
| Summer.Flowers[2] | `#ffd24a` |
| Summer.Flowers[3] | `#3dffb4` |
| Summer.Water | `#1f58c0` |
| Summer.Path | `#4a3a86` |
| Summer.PathDark | `#33286a` |
| Autumn.Grass | `#2e2450` |
| Autumn.GrassDark | `#251c42` |
| Autumn.GrassLight | `#3c2f64` |
| Autumn.Leaves | `#ff9a3d` |
| Autumn.LeavesDark | `#c25a2a` |
| Autumn.LeavesLight | `#ffd24a` |
| Autumn.PineLeaves | `#2a7ad0` |
| Autumn.PineLeavesDark | `#1a4a96` |
| Autumn.Accent | `#ff9a3d` |
| Autumn.Flowers[0] | `#ff9a3d` |
| Autumn.Flowers[1] | `#ff4f7a` |
| Autumn.Flowers[2] | `#ffd24a` |
| Autumn.Flowers[3] | `#b45cff` |
| Autumn.Water | `#22489a` |
| Autumn.Path | `#4a3a86` |
| Autumn.PathDark | `#33286a` |
| Winter.Grass | `#c9d2f0` |
| Winter.GrassDark | `#a8b2dc` |
| Winter.GrassLight | `#e8ecff` |
| Winter.Leaves | `#3a2f5a` |
| Winter.LeavesDark | `#2a2148` |
| Winter.LeavesLight | `#5a4a86` |
| Winter.PineLeaves | `#2a6a9a` |
| Winter.PineLeavesDark | `#1a4a96` |
| Winter.Accent | `#35e0ff` |
| Winter.Flowers[0] | `#7a8ad0` |
| Winter.Flowers[1] | `#5a6ab0` |
| Winter.Flowers[2] | `#35e0ff` |
| Winter.Flowers[3] | `#d0d8ff` |
| Winter.Water | `#8fb0e8` |
| Winter.Path | `#6a6aa8` |
| Winter.PathDark | `#4f4f8a` |
