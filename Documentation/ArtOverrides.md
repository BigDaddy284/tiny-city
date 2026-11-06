# Using your own pictures

All of the game's art is drawn by its own code (`Assets/Scripts/Art`), so it needs no image files. You can still
replace any picture with a PNG of your own. This is optional, and nothing changes if you don't.

## Where the files go

In the Windows build, make a folder called `Art` inside `TinyCity_Data\StreamingAssets\` (next to the
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
| `object-<type>.png` | every piece of furniture of that type: `object-bed`, `object-kitchen`, `object-gardenplot`, `object-bench`, `object-workbench`, `object-fishingspot`, `object-gatheringspot`, `object-marketstall`, `object-woodland`, `object-rockpile`, `object-buildplot`, `object-stickpile`, `object-loosestones`, `object-sleepingspot` |
| `ground-<map id>.png` | the whole ground of a map: `ground-map-flat-land` |

## Size and position

The game uses 64 pixels per tile: one tile is a 64 × 32 pixel diamond. Draw in the same isometric view as the game
(x runs down to the right, y down to the left, light from the upper left).

A picture stands on its tiles like this: it is centred left to right over what it covers, and its bottom edge sits on
the lowest corner of those tiles. A tree or a piece of furniture covers one tile, so a 64-pixel-wide picture's bottom
centre is the tile's bottom corner. A map's ground picture is (width + height) × 32 pixels wide and
(width + height) × 16 pixels tall; the open land is 56 × 44 tiles. Residents' own buildings are drawn from their
blocks and can't be replaced.

Pixels stay sharp (no smoothing), so draw at exactly this scale.
