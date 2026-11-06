# Resident profiles (Tiny City 2)

Who a new town starts with comes from `residents.json`, next to `settings.json`:
`TinyCity2_Data/StreamingAssets/Configuration/residents.json` in the game folder
(`Assets/StreamingAssets/Configuration/residents.json` in the project). Editing it changes only towns made
afterwards; towns already saved keep their people.

Each profile is short on purpose. From there it is up to the residents.

## The brief and the roles (Tiny City 2)

Two more entries at the top of the file speak to everyone:

- `brief`: up to 1,500 letters that every resident hears in every prompt, as "what everyone here knows". Use it
  for the ground rules of the world.
- `roles`: up to 20 roles anyone may take up, each a `name` (up to 40 letters), an `about` paragraph (up to 600),
  an optional `canTax` (true lets whoever takes the role ask people for a share of their goods) and an
  optional `can`: the work only that role may do, as activity ids (for example `"can": ["plant-crops",
  "water-crops", "harvest"]`; an unknown id is left out and reported). Work no role lists is open to everyone,
  but fishing, washing in the pond and every kind of gathering, growing, cooking and building always need a role (owner, 6 November). An optional
  `max` (a whole number from 1 to 100) is how many people may hold the role at once; once it is full nobody else
  can take it up, or be voted into it, until a place comes free. Leave it out for no limit. Nobody is
  given a role. Each morning, as they plan their day, each resident may take one up; their first choice is
  theirs at once. After that, changing role or giving it up (`"none"`) is a request put to the weekly town
  meeting (Monday 10:00 at the meeting circle, from a week after the start; there is no first-day meeting),
  where everyone there takes a turn to raise what the land needs and votes on each request; it passes only if
  more than half of those there vote yes. A request from someone who stays away waits for the next meeting.

The shipped roles: farmer (crops), fisher (the full catch), lumberjack (trees), miner (rocks), toolmaker
(every tool), cook, builder (planning and building), water purifier (cleaning pond water into clean water,
which anyone can drink and trade) and merchant (a stall), plus tax man, security enforcer and mayor, who run
no work of their own. Without a role you can drink and wash in the dirty pond, eat raw food and catch a fish by
hand; fallen branches are the lumberjack's and loose stones the miner's. Dirty water and raw food fill less and cost a little
health; clean water and cooked meals don't.

Tax: someone in a role with `canTax` (the shipped "tax man") may, in any chat, name a percentage of the other
person's food and materials. The other person's next line says whether they pay; if they do, that share of each
kind (rounded down) is handed over. Nobody has to pay.

Barter: there is no money in Tiny City 2 at all (owner, 6 November), so residents trade goods. In a chat anyone can hand over clean water, a material (wood, stone, hardwood, iron ore, heartwood, crystal) or
a tool (axe, pickaxe, hoe, fishing rod; a tool only to someone without one of that kind), and food. A
merchant's stall hands food out to whoever comes.

Illness is switched off in Tiny City 2, so there is no doctor role.

A file with neither gets the shipped brief and roles. A new world keeps the brief and roles it began with,
saved with it.

```json
{
  "format": "tiny-city-2-profiles",
  "brief": "Stay alive first ...",
  "roles": [ { "name": "farmer", "about": "plants, waters and picks crops so there is food" }, { "name": "tax man", "about": "...", "canTax": true }, "fisher" ],
  "residents": [ ... ]
}
```

## Residents

```json
{
  "format": "tiny-city-2-profiles",
  "residents": [
    {
      "id": "res-0004",
      "firstName": "Rosa",
      "lastName": "Pennywhistle",
      "age": 46,
      "about": "",
      "personality": "Tidy, careful and tight-fisted. Counts everything and hates waste."
    }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `id` | One of the twenty starting residents, `res-0001` to `res-0020` (the first fifteen also live in v1's city; the last five only on the open land, with looks made from their id). The id fixes their look and hobbies. Twenty is also the most there can be. |
| `firstName`, `lastName` | Up to 24 letters each. A first name is required. |
| `age` | A whole number from 18 to 100. |
| `about` | Optional, up to 300 letters of background, given to the AI as the resident's own words. The shipped file leaves it empty for everyone (owner, 5 November: no backstories), and on the open land a resident with no profile has none either. |
| `personality` | Optional, up to 200 letters: bluntly, what type of person they are, such as "Kind, patient and gentle. Puts everyone else first and never complains." In Tiny City 2 it replaces the personality traits in every prompt. |
| `owns` | Optional. The business they start as boss of: `biz-store`, `biz-cafe`, `biz-bakery`, `biz-clinic`, `biz-bar`, `biz-cue-club`, `biz-bowling`, `biz-arcade` or `biz-gym`. Each business has at most one owner. With none, a business has no boss until someone chooses to take it on. |

Only the residents listed start, in the order listed. A profile with a problem is left out or repaired, and the reason is written to `Player.log`; if no profile can be used, or the file is missing, everyone starts as designed: all twenty on the open land (fifteen in v1's city).
