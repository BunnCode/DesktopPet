"""Writes a staged DesktopPet data folder. Usage: python make_demo.py <folder> <variant>

Six pets, one per default (Fakemon) character, drawing the built-in art, plus a dead pet for the gravestone.
variants:
  base    furniture, an egg and a gravestone; stats high, chat heavy
  needy   stats low, so the pets use the bed, trough and couch
  court   breeding turned up
  climb   like base, but the pets walk and climb a lot (for the windows scene)
  lounge  only the Living Tower; happiness low, so the pets go and sit in it (and join each other)
  games   only the TV; happiness low, so the pets play video games together
"""
import json
import os
import sys
from datetime import datetime, timedelta, timezone

root, variant = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "base"
now = datetime.now(timezone.utc)
iso = lambda t: t.isoformat()  # noqa: E731
FLOOR = 1380  # work area bottom, physical px


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(data if isinstance(data, str) else json.dumps(data, indent=2))


# (id, name, character, species for the personality, personality)
PETS = [
    (1, "Mabel", "botrosa", "little mechanical creature with a floating ring", "curious and tinkers with everything"),
    (2, "Theo", "carycajolt", "little electric caracal-like cat", "quick, playful and a bit of a show-off"),
    (3, "Hazel", "courceel", "little tan foal", "gentle and loves a good nap in the sun"),
    (4, "Milo", "crimejr", "little bear-cub thief in a hood", "sneaky, cheeky and secretly very sweet"),
    (5, "Rosie", "emberdonkey", "little fire donkey", "warm-hearted and always up for a game"),
    (6, "Bruno", "redmuzzle", "stocky little dark bear with a big red muzzle", "calm, sturdy and fond of snacks"),
]
stats = {"base": (88, 90, 92), "climb": (88, 90, 92), "needy": (40, 35, 30), "court": (95, 97, 95), "lounge": (92, 45, 92),
         "games": (92, 45, 92)}[variant]
for n, (pid, name, character, species, trait) in enumerate(PETS):
    h, hap, e = stats
    folder = os.path.join(root, "pets", str(pid))
    write(os.path.join(folder, "personality.txt"), f"{name} is a {species} who is {trait}.")
    write(os.path.join(folder, "save.json"), {
        "Version": 4,
        "Traits": {"Name": name, "Character": character, "Fixed": variant != "court"},
        "Hunger": h - n, "Happiness": hap - n, "Energy": e - n * 2,
        "SavedAtUtc": iso(now), "BornUtc": iso(now - timedelta(days=20 + n)),
    })

# A pet that died, for the gravestone (not in the lounge or games, where the app would drop its stone in the shot).
DEAD = 9
dead = os.path.join(root, "pets", str(DEAD))
if variant not in ("lounge", "games"):
    write(os.path.join(dead, "personality.txt"), "A gentle soul.")
    write(os.path.join(dead, "save.json"), {
        "Version": 4, "Traits": {"Name": "Clover", "Character": "courceel"}, "Hunger": 0, "Happiness": 40,
        "Energy": 50, "SavedAtUtc": iso(now - timedelta(days=2)),
        "BornUtc": "2026-03-14T12:00:00+00:00", "DiedUtc": iso(now - timedelta(days=2)), "DiedOf": "Hunger",
    })

egg = {"Kind": "egg", "X": 1720, "Y": FLOOR, "Egg": {
    "LaidAtUtc": iso(now), "HatchAtUtc": iso(now + timedelta(days=3)),
    "Parents": ["Theo", "Rosie"],
    "Child": {"Name": "", "Neediness": {"Hunger": 1, "Happiness": 1, "Energy": 1},
              "Behavior": {"Idle": 1, "Walk": 1, "Sit": 1, "Sleep": 1, "Climb": 1, "Push": 1, "Mating": 1},
              "Chat": {"Needy": 1, "Banter": 1, "Jokes": 1, "Notes": 1, "Curiosity": 1},
              "Fixed": False, "Character": "emberdonkey", "Parents": ["Theo", "Rosie"]},
    "SpritesFrom": None}}
grave = {"Kind": "grave", "X": 2440, "Y": FLOOR, "Grave": {
    "PetId": DEAD, "Name": "Clover", "BornUtc": "2026-03-14T12:00:00+00:00",
    "DiedUtc": iso(now - timedelta(days=2)), "Cause": "Hunger"}}
if variant == "lounge":
    items = [{"Kind": "livingtower", "X": 1280, "Y": FLOOR}]
elif variant == "games":
    items = [{"Kind": "tv", "X": 1280, "Y": FLOOR}]
else:
    items = [
        {"Kind": "bed", "X": 300, "Y": FLOOR},
        {"Kind": "trough", "X": 500, "Y": FLOOR},
        {"Kind": "trampoline", "X": 800, "Y": FLOOR},
        {"Kind": "ball", "X": 940, "Y": FLOOR},
        *([] if variant == "court" else [egg]),  # in court, only the eggs laid during the shoot
        {"Kind": "tv", "X": 1880, "Y": FLOOR},
        {"Kind": "couch", "X": 2090, "Y": FLOOR},
        grave,
    ]
write(os.path.join(root, "room.json"), {"Version": 1, "Items": items})

settings = {
    "EnablePushing": False,
    "Weights": {"Idle": 3, "Walk": 5, "Sit": 1.5, "Sleep": 1},
    "Chat": {
        "NeedyPerHour": 40, "BanterPerHour": 240, "JokesPerHour": 60, "MinGapSeconds": 3, "PetGapSeconds": 8,
        "BubbleSeconds": 7, "NotesPerHour": 90, "NoteGapSeconds": 15, "PetNoteGapSeconds": 30, "MaxNotes": 3,
        "RemarksPerHour": 30,
    },
    "Breeding": {"MatesPerHour": 400 if variant == "court" else 0, "CooldownHours": 0.01, "MinStat": 50},
}
if variant == "climb":
    settings["Weights"] = {"Idle": 1, "Walk": 8, "Sit": 0.5, "Sleep": 0.2}
    settings["ClimbChance"] = 0.75
    settings["PushChance"] = 0
    settings["Furniture"] = {"Weight": 0.5}
if variant in ("lounge", "games"):
    settings["ClimbChance"] = 0
    settings["Furniture"] = {"NeedBelow": 95, "Weight": 40, "JoinChancePerSecond": 1, "JoinDistanceDip": 3000,
                             "NearbyToyDip": 3000}
    settings["Chat"]["NotesPerHour"] = 0
write(os.path.join(root, "settings.json"), settings)
print("wrote", root, variant)
