# Resident profiles (Tiny City 2)

Who a new town starts with comes from `residents.json`, next to `settings.json`:
`TinyCity2_Data/StreamingAssets/Configuration/residents.json` in the game folder
(`Assets/StreamingAssets/Configuration/residents.json` in the project). Editing it changes only towns made
afterwards; towns already saved keep their people.

Each profile is short on purpose. From there it is up to the residents.

```json
{
  "format": "tiny-city-2-profiles",
  "residents": [
    {
      "id": "res-0004",
      "firstName": "Rosa",
      "lastName": "Pennywhistle",
      "age": 46,
      "about": "I run the general store and know what everyone buys.",
      "money": 600,
      "owns": "biz-store"
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `id` | One of the city's residents, `res-0001` to `res-0015`. The id fixes their look, home, job and hobbies. |
| `firstName`, `lastName` | Up to 24 letters each. A first name is required. |
| `age` | A whole number from 18 to 100. |
| `about` | Up to 300 letters of personality and background, given to the AI as the resident's own words. |
| `money` | Pounds they start with, from 0 to 1,000,000. |
| `owns` | Optional. The business they start as boss of: `biz-store`, `biz-cafe`, `biz-bakery`, `biz-clinic`, `biz-bar`, `biz-cue-club`, `biz-bowling`, `biz-arcade` or `biz-gym`. Each business has at most one owner. With none, a business has no boss until someone chooses to take it on. |

Only the residents listed start, in the order listed. A profile with a problem is left out or repaired, and the reason is written to `Player.log`; if no profile can be used, or the file is missing, the city starts with all fifteen residents as designed.
