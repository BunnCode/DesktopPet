# Furniture

Furniture is placed from a pet's menu (**Furniture → Add …**). It drops in beside that pet, lands on whatever is
below (the taskbar or a window), rides windows that move and falls when its window goes. Drag it to move it; right-click
it to remove it. All pets share it, and where it stands is saved in `room.json`.

Pets use furniture on their own when a need it meets is low (below 75 by default), and now and then just for fun,
a bit more often when a toy (the ball, TV, couch, trampoline or Living Tower) is close by. When a pet is playing something that has
room for more (the TV, the ball, jumping on the couch, the Living Tower), other pets nearby often come over and play along for a little
while. They walk to it, climbing window sides if it stands on another window; if there's no way there, they don't go. Furniture is
slower than your care: Feed and Play from the menu are still the quickest way to help a pet.

## The defaults

| Furniture | What pets do |
|---|---|
| Ball | Chase it and kick it around (happiness). Click it to poke it |
| TV | Play video games, up to 4 pets at once (happiness) |
| Couch | Nap on it (energy) or jump on it (happiness) |
| Trampoline | Bounce, then launch high: a quick way onto tall window tops (happiness) |
| Bed | Sleep (energy) |
| Play pen | Pets never go in by themselves. Drop a pet in: it stays inside and its needs don't drop until you drag it out |
| Trough | Eat (hunger), two pets at once |
| Living Tower | Verner Panton's 1969 lounge: up to 4 pets hang out on its four levels, legs dangling over the seats (happiness), or curl up for a nap (energy) |
| Breeding pill | Swallowed (it's used up). The pet then goes round every other pet, nearest first, and asks to mate; each one that says yes mates with it and an egg is laid. Fixed pets leave it alone |

New defaults in later versions (like the breeding pill) are added to your furniture folder once, on the next start.
`defaults-written.txt` in the folder lists the defaults already put there; remove a line to get that piece back.

## Making your own

Each kind is a JSON file in `data\furniture\` with its image next to it. The file name is the
kind (`ball.json` is the ball). Edits apply as soon as you save; problems show in a notification and in
`problems.txt`, and a broken file keeps its last good version. Delete a file to remove that kind (and all of it from the
room); delete the whole folder to get the defaults back on the next start.

```jsonc
{
  "Name": "Trough",            // shown in the menu
  "WidthDip": 48,              // size; the pet is 64
  "HeightDip": 20,
  "Art": "trough.png",         // frames side by side, all the same width
  "Frames": 1,
  "Fps": 4,
  "Animate": "Always",         // Always, Moving (a ball rolling) or InUse (a TV that's on)
  "Smooth": false,             // true for painted art, false for pixel art
  "Slots": 2,                  // pets at once
  "SlotOffsetsDip": [-40, 40], // where each pet stands, from the centre (optional: spread evenly)
  "SlotSeatsDip": [0, 0],      // optional: each slot's own seat height (the Living Tower's levels), instead of SeatDip
  "SlotFacing": ["Toward", "Toward"], // optional: Toward (face the furniture from beside it), Left or Right
  "Bounciness": 0,             // 0..0.95: how much it bounces when it lands
  "Friction": 1,               // lower slides further
  "Consumable": false,         // true: used up once a pet has finished using it (a pill)
  "Uses": [
    {
      "Need": "Hunger",        // Hunger, Happiness, Energy or None
      "RatePerHour": 2400,     // points added per hour of use, on top of normal sleep etc.
      "Animation": "eat",      // the pet's animation (see SPRITES.md)
      "Seconds": { "Min": 15, "Max": 25 },
      "MinSeconds": 0,         // stay at least this long, even once the need is full (the Living Tower: 30)
      "Effect": "None",        // None, Hop, Bounce (launch), Kick (chase it), Confine (a pen) or Breed (a breeding pill)
      "SeatDip": 0             // draw the pet this high (on a bed or couch); "sit_ledge" sits on the edge, legs over
    }
  ]
}
```

When to go and how hard balls are kicked or trampolines launch are in `settings.json` under `Furniture`. How long a
breeding pill lasts and how likely pets are to say yes are under `Breeding` (`PillMinutes`, `PillVisitSeconds`,
`PillAskSeconds`, `PillAgreeChance`).
