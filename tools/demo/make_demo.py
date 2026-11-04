"""Writes a staged DesktopPet data folder. Usage: python make_demo.py <folder> <variant>

variants: base (4 pets, furniture, egg, grave), needy (stats low so they use furniture), court (breeding on)
"""
import json
import os
import sys
from datetime import datetime, timedelta, timezone

root, variant = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "base"
now = datetime.now(timezone.utc)
iso = lambda t: t.isoformat()  # noqa: E731
FLOOR = 1380

pets = {
    1: ("Mabel", "Cheerful and nosy; loves carrots and naps in sunbeams."),
    2: ("Otis", "A grumpy old soul who secretly loves cuddles."),
    3: ("Juniper", "Bouncy, curious and always up to something."),
    4: ("Biscuit", "Sleepy and sweet; tells terrible jokes."),
}


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(data if isinstance(data, str) else json.dumps(data, indent=2))


stats = {"base": (88, 90, 92), "needy": (40, 35, 30), "court": (95, 97, 95)}[variant]
for pid, (name, personality) in pets.items():
    folder = os.path.join(root, "pets", str(pid))
    h, hap, e = stats
    write(os.path.join(folder, "traits.json"), {"Name": name, "Fixed": False})
    write(os.path.join(folder, "personality.txt"), personality)
    write(os.path.join(folder, "save.json"), {
        "Version": 3, "Hunger": h + pid, "Happiness": hap - pid, "Energy": e - pid * 2,
        "SavedAtUtc": iso(now), "BornUtc": iso(now - timedelta(days=12 + pid)),
    })

# A pet that died, for the gravestone.
dead = os.path.join(root, "pets", "5")
write(os.path.join(dead, "traits.json"), {"Name": "Clover"})
write(os.path.join(dead, "personality.txt"), "A gentle soul.")
write(os.path.join(dead, "save.json"), {
    "Version": 3, "Hunger": 0, "Happiness": 40, "Energy": 50, "SavedAtUtc": iso(now - timedelta(days=2)),
    "BornUtc": "2026-03-14T12:00:00+00:00", "DiedUtc": iso(now - timedelta(days=2)), "DiedOf": "Hunger",
})

items = [
    {"Kind": "bed", "X": 260, "Y": FLOOR},
    {"Kind": "trough", "X": 470, "Y": FLOOR},
    {"Kind": "ball", "X": 1820, "Y": FLOOR},
    {"Kind": "tv", "X": 1960, "Y": FLOOR},
    {"Kind": "couch", "X": 2140, "Y": FLOOR},
    {"Kind": "trampoline", "X": 700, "Y": FLOOR},
    {"Kind": "egg", "X": 1720, "Y": FLOOR, "Egg": {
        "LaidAtUtc": iso(now), "HatchAtUtc": iso(now + timedelta(days=3)),
        "Parents": ["Mabel", "Otis"],
        "Child": {"Name": "", "Neediness": {"Hunger": 1, "Happiness": 1, "Energy": 1},
                  "Behavior": {"Idle": 1, "Walk": 1, "Sit": 1, "Sleep": 1, "Climb": 1, "Push": 1, "Mating": 1},
                  "Chat": {"Needy": 1, "Banter": 1, "Jokes": 1, "Notes": 1, "Curiosity": 1},
                  "Fixed": False, "Jacket": "#B25A9E", "Parents": ["Mabel", "Otis"]},
        "SpritesFrom": None}},
    {"Kind": "grave", "X": 2420, "Y": FLOOR, "Grave": {
        "PetId": 5, "Name": "Clover", "BornUtc": "2026-03-14T12:00:00+00:00",
        "DiedUtc": iso(now - timedelta(days=2)), "Cause": "Hunger"}},
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
write(os.path.join(root, "settings.json"), settings)
print("wrote", root, variant)
