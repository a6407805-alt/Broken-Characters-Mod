#!/usr/bin/env python3
"""Merges launcher/cards/*.json into launcher/roster.json.

Runs on the build server only (the merged file is never committed). Each creator owns their own card file,
launcher/cards/<Mod>.json, so two people never edit the same file. A card has any of the lists
"characters" (matched by name), "maps" and "modes" (matched by title). An entry with the same name/title as one in
roster.json replaces it; a new one is added at the front.
"""
import json
import pathlib

launcher = pathlib.Path(__file__).resolve().parent.parent / "launcher"
base_path = launcher / "roster.json"
data = json.loads(base_path.read_text(encoding="utf-8-sig"))

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

base_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
