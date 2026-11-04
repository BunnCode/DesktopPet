"""Writes a staged DesktopPet data folder. Usage: python make_demo.py <folder> <variant>

variants: base (4 pets, furniture, egg, grave), needy (stats low so they use furniture), court (breeding on)
"""
import json
import os
import re
import shutil
import sys
from datetime import datetime, timedelta, timezone

root, variant = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "base"
now = datetime.now(timezone.utc)
iso = lambda t: t.isoformat()  # noqa: E731
FLOOR = 1380

def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        f.write(data if isinstance(data, str) else json.dumps(data, indent=2))


stats = {"base": (88, 90, 92), "needy": (40, 35, 30), "court": (95, 97, 95)}[variant]
# The pets are the ones in publish/data/pets (their traits, personalities and sprites), with the stats reset.
SRC = os.path.join(os.path.dirname(__file__), "..", "..", "publish", "data", "pets")
for n, pid in enumerate(sorted(os.listdir(SRC), key=int)):
    folder = os.path.join(root, "pets", pid)
    shutil.copytree(os.path.join(SRC, pid), folder, dirs_exist_ok=True)
    path = os.path.join(folder, "save.json")
    text = open(path, encoding="utf-8").read()
    h, hap, e = stats
    for key, val in (("Hunger", h - n), ("Happiness", hap - n), ("Energy", e - n * 2)):
        text = re.sub(rf'^  "{key}": .*,', f'  "{key}": {val},', text, flags=re.M)
    text = re.sub(r'^  "SavedAtUtc": .*,', f'  "SavedAtUtc": "{iso(now)}",', text, flags=re.M)
    text = re.sub(r'^  "(LastMatedUtc|DiedUtc|DiedOf)": .*\n', "", text, flags=re.M)
    text = re.sub(r'("Hunger": )1000,', r"\g<1>1,", text)  # no pet starves during the shoot
    write(path, text)

# A pet that died, for the gravestone.
DEAD = 9
dead = os.path.join(root, "pets", str(DEAD))
write(os.path.join(dead, "personality.txt"), "A gentle soul.")
write(os.path.join(dead, "save.json"), {
    "Version": 4, "Traits": {"Name": "Clover"}, "Hunger": 0, "Happiness": 40, "Energy": 50,
    "SavedAtUtc": iso(now - timedelta(days=2)),
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
        "Parents": ["Josiah", "Mia"],
        "Child": {"Name": "", "Neediness": {"Hunger": 1, "Happiness": 1, "Energy": 1},
                  "Behavior": {"Idle": 1, "Walk": 1, "Sit": 1, "Sleep": 1, "Climb": 1, "Push": 1, "Mating": 1},
                  "Chat": {"Needy": 1, "Banter": 1, "Jokes": 1, "Notes": 1, "Curiosity": 1},
                  "Fixed": False, "Jacket": "#B25A9E", "Parents": ["Josiah", "Mia"]},
        "SpritesFrom": None}},
    {"Kind": "grave", "X": 2420, "Y": FLOOR, "Grave": {
        "PetId": DEAD, "Name": "Clover", "BornUtc": "2026-03-14T12:00:00+00:00",
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
