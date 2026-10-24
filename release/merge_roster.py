#!/usr/bin/env python3
"""Merges launcher/cards/*.json into launcher/roster.json and auto-enriches character versions & statuses.

Runs on the build server only (the merged file is never committed). Each creator owns their own card file,
launcher/cards/<Mod>.json, so two people never edit the same file. A card has any of the lists
"characters" (matched by name), "maps" and "modes" (matched by title). An entry with the same name/title as one in
roster.json replaces it; a new one is added at the front.

Status auto-rules:
  - "green" is ONLY kept when explicitly set (owner declared complete).
  - "active" marks characters currently being actively worked on right now.
  - "red" auto-promotes to "yellow" when the mod's version climbs to >= 0.3.0.
"""
import json
import pathlib
import re

root = pathlib.Path(__file__).resolve().parent.parent
launcher = root / "launcher"
base_path = launcher / "roster.json"
data = json.loads(base_path.read_text(encoding="utf-8-sig"))

MOD_FOLDER = {
    "CARMAX SPECIALIST": "CarMaxSpecialist",
    "AUDI TECHNICIAN": "AudiTechnician",
    "BILLY BIGSUITS": "BillyBigsuits",
    "WILBUR": "WilburMod",
    "BROTHER": "OatsBrother",
    "ASHWING DRAKE": "AshwingDrake",
    "NIDUS": "Nidus",
    "PHANTOM ASSASSIN": "PhantomAssassin",
    "INFESTED GENERAL": "InfestedGeneral",
    "GRAVITOS": "Gravitos",
    "CLAY REBEL": "ClayRebel",
    "EMBERWARDEN": "Emberwarden",
    "REAPER": "Reaper",
    "THE DUELIST": "Duelist",
    "THE SURVEYOR": "Surveyor",
}


def parse_ver(v: str):
    nums = [int(x) for x in re.findall(r"\d+", v or "")]
    while len(nums) < 3:
        nums.append(0)
    return tuple(nums[:3])


def read_mod_version(name: str) -> str:
    folder = MOD_FOLDER.get(name.strip().upper())
    if not folder:
        return ""
    manifest = root / "mods" / folder / "manifest.json"
    if manifest.is_file():
        try:
            mdata = json.loads(manifest.read_text(encoding="utf-8-sig"))
            if mdata.get("version_number"):
                return str(mdata["version_number"]).strip()
        except Exception:
            pass
    return ""


sections = {"characters": "name", "maps": "title", "modes": "title"}
for section in sections:
    data.setdefault(section, [])

cards_dir = launcher / "cards"
for card_file in sorted(cards_dir.glob("*.json")) if cards_dir.is_dir() else []:
    card = json.loads(card_file.read_text(encoding="utf-8-sig"))
    for section, key in sections.items():
        for entry in card.get(section, []):
            wanted = str(entry.get(key, "")).strip().lower()
            items = data[section]
            for i, old in enumerate(items):
                if str(old.get(key, "")).strip().lower() == wanted:
                    items[i] = entry
                    break
            else:
                items.insert(0, entry)
    print("merged " + card_file.name)

for char in data.get("characters", []):
    ver = char.get("version") or read_mod_version(char.get("name", ""))
    if ver:
        char["version"] = ver
    st = str(char.get("status", "red")).strip().lower()
    if st == "red" and ver and parse_ver(ver) >= (0, 3, 0):
        char["status"] = "yellow"

base_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
