# Using your own pictures

All of the game's art is drawn by its own code (`Assets/Scripts/Art`), so it needs no image files. You can still
replace any picture with a PNG of your own. This is optional, and nothing changes if you don't.

## Where the files go

In the Windows build, make a folder called `Art` inside `TheTinyTown_Data\StreamingAssets\` (next to the
`Configuration` folder) and put PNG files in it. In the Unity editor the folder is `Assets/StreamingAssets/Art/`.
Restart the game after adding or changing files. If a file can't be read, the game logs a warning and keeps its own
picture.

## File names

A file is named after the picture it replaces. You can add the season (`-spring`, `-summer`, `-autumn`, `-winter`)
to make a picture for one season only; that one is used first.

| File | Replaces |
| --- | --- |
| `tree.png`, `tree-winter.png`, … | every broadleaf tree |
| `pine.png`, `pine-winter.png`, … | every pine |
| `object-<type>.png` | every piece of furniture of that type: `object-bed`, `object-kitchen`, `object-sink`, `object-toilet`, `object-shower`, `object-armchair`, `object-gardenplot`, `object-gardentap`, `object-bench`, `object-exercisespot`, `object-workbench`, `object-shopshelf`, `object-counter`, `object-cafetable`, `object-bakerydisplay`, `object-clinicbed`, `object-easel`, `object-piano`, `object-chesstable`, `object-fishingspot`, `object-gatheringspot` |
| `building-<map id>.png` | one building in the village, by the place inside it: `building-map-store`, `building-map-cafe`, `building-map-bakery`, `building-map-clinic`, `building-map-cottage-1` … `building-map-cottage-10` |
| `ground-<map id>.png` | the whole ground of a map, such as `ground-map-village` or `ground-map-cafe` |

A replaced building has no lit windows at night, because the game can't tell where your windows are.

## Size and position

The game uses 64 pixels per tile: one tile is a 64 × 32 pixel diamond. Draw in the same isometric view as the game
(x runs down to the right, y down to the left, light from the upper left).

A picture stands on its tiles like this: it is centred left to right over what it covers, and its bottom edge sits on
the lowest corner of those tiles. A tree or a piece of furniture covers one tile, so a 64-pixel-wide picture's bottom
centre is the tile's bottom corner. Buildings cover 5 × 4 tiles (Fern Cottage 5 × 3), so a building picture is
normally 288 pixels wide (256 for Fern Cottage) plus anything that overhangs. A map's ground picture is
(width + height) × 32 pixels wide and (width + height) × 16 pixels tall; the village is 44 × 28 tiles.

Pixels stay sharp (no smoothing), so draw at exactly this scale.
