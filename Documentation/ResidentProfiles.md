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
- `roles`: up to 20 roles anyone may take up, each a `name` (up to 40 letters), an `about` paragraph (up to 600)
  and an optional `canTax` (true lets whoever takes the role ask people for a share of their goods), or just a
  name. Nobody is given one. Each morning, as they plan their day, each resident chooses whether to take
  one up, change it or have none (`"none"`), and the others see who has chosen what. A role unlocks and blocks
  nothing: anyone can still do anything.

Tax: someone in a role with `canTax` (the shipped "tax man") may, in any chat, name a percentage of the other
person's food and materials. The other person's next line says whether they pay; if they do, that share of each
kind (rounded down) is handed over. Nobody has to pay. Anyone can also hand money over in a chat (a gift, a wage
for a security enforcer, a payment), and it moves at once if they have it.

Barter: the shipped profiles start everyone with no money and the brief says there is none, so residents
trade goods. In a chat anyone can hand over a material (wood, stone, hardwood, iron ore, heartwood, crystal) or
a tool (axe, pickaxe, hoe, fishing rod; a tool only to someone without one of that kind), and food as before.
While nobody alive has any money, the prompts leave money, prices and `give_money` out. Give someone `money`
here and money comes back into play for everyone.

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
      "about": "I kept a shop for years and I can't stand waste or mess.",
      "personality": "You keep careful count of everything, and you never give away what you can trade.",
      "money": 0
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
| `personality` | Optional. One sentence, up to 200 letters, on how they treat others and the world, such as "You live to help others and always put other people first." In Tiny City 2 it replaces the personality traits in every prompt. |
| `money` | Pounds they start with, from 0 to 1,000,000. |
| `owns` | Optional. The business they start as boss of: `biz-store`, `biz-cafe`, `biz-bakery`, `biz-clinic`, `biz-bar`, `biz-cue-club`, `biz-bowling`, `biz-arcade` or `biz-gym`. Each business has at most one owner. With none, a business has no boss until someone chooses to take it on. |

Only the residents listed start, in the order listed. A profile with a problem is left out or repaired, and the reason is written to `Player.log`; if no profile can be used, or the file is missing, the city starts with all fifteen residents as designed.
