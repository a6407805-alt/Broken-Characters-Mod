#!/usr/bin/env python3
"""Writes mods/CharacterStatus/status.json (what the in-game status lines read) from the launcher's roster:
launcher/roster.json plus launcher/cards/*.json (cards win). Run it whenever a "status" changes, then commit both.

"status" in a roster entry or card: "green" = complete, "yellow" = in progress, "red" = early / untested.
MATCH lists the letters-only, lowercase pieces of a survivor's name (display name, body or def name) that identify it in game;
a roster name that is not listed here falls back to its own name.
"""
import json
import pathlib
import re

root = pathlib.Path(__file__).resolve().parent.parent
MATCH = {
    "CARMAX SPECIALIST": ["carmax"],
    "AUDI TECHNICIAN": ["auditechnician", "auditech"],
    "BILLY BIGSUITS": ["billybigsuits", "billy"],
    "WILBUR": ["wilbur"],
    "BROTHER": ["oatsbrother", "brother"],
    "ASHWING DRAKE": ["ashwing"],
    "NIDUS": ["nidus"],
    "PHANTOM ASSASSIN": ["phantomassassin", "phantom"],
    "INFESTED GENERAL": ["infestedgeneral"],
    "GRAVITOS": ["gravitos"],
    "CLAY REBEL": ["clayrebel", "rebel"],
    "EMBERWARDEN": ["emberwarden"],
    "REAPER": ["reaper"],
    "THE DUELIST": ["duelist"],
    "THE SURVEYOR": ["surveyor"],
}

entries = {}
files = [root / "launcher" / "roster.json"] + sorted((root / "launcher" / "cards").glob("*.json"))
for f in files:
    for c in json.loads(f.read_text(encoding="utf-8-sig")).get("characters", []):
        name = c.get("name", "").strip()
        if name and c.get("status"):
            entries[name.upper()] = c["status"].strip().lower()

out = []
for name, status in entries.items():
    match = MATCH.get(name) or [re.sub(r"[^a-z0-9]", "", name.lower())]
    out.append({"name": name, "status": status, "match": match})

dest = root / "mods" / "CharacterStatus" / "status.json"
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text(json.dumps({"characters": out}, indent=2) + "\n", encoding="utf-8")
print("wrote", dest, len(out), "characters")
for o in out:
    print("  %-18s %s" % (o["name"], o["status"]))
