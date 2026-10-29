# Using your own sounds

All of the game's music, ambience and effects are made by its own code (`Assets/Scripts/Sound`), so it needs no
sound files. You can still replace any sound with a WAV of your own. This is optional, and nothing changes if you
don't.

## Where the files go

In the Windows build, make a folder called `Audio` inside `TinyCity_Data\StreamingAssets\` (next to the
`Configuration` folder) and put WAV files in it. In the Unity editor the folder is `Assets/StreamingAssets/Audio/`.
Restart the game after adding or changing files. If a file can't be read, the game logs a warning and keeps its own
sound.

## File names

| File | Replaces |
| --- | --- |
| `music-day.wav` | the daytime music (loops) |
| `music-night.wav` | the evening and night music (loops), from dusk to dawn |
| `ambience-day.wav`, `ambience-night.wav` | the city outdoors by day and by night (loops) |
| `ambience-indoors.wav` | the room tone inside any building (loops) |
| `sfx-footstep-0.wav` … `sfx-footstep-3.wav` | footsteps, taken in turn |
| `sfx-door.wav` | going in or out of a building |
| `sfx-typing.wav` | desk and workbench work |
| `sfx-till.wav` | shop, café and bakery counters, buying groceries and pastries |
| `sfx-cooking.wav` | cooking, snacks and baking |
| `sfx-drink.wav` | drinks and coffee |
| `sfx-chess.wav`, `sfx-piano.wav`, `sfx-splash.wav`, `sfx-digging.wav` | chess, piano, fishing, gardening |
| `sfx-chime.wav` | a city news pop-up |

## Format

Use PCM WAV at 8, 16 or 24 bits (16-bit is best), or 32-bit float, at any common sample rate. Stereo files are mixed
down to mono. Music and ambience loop from the end straight back to the start, so cut them to join cleanly.

To hear the built-in sounds, run `dotnet run -- --sounds <folder>` in `Tools/ArtPreview`; it writes every one as a WAV.
