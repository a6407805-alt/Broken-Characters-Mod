#!/usr/bin/env python3
"""Writes mods/CharacterStatus/status.json (what the in-game status lines read) from the launcher's roster:
launcher/roster.json plus launcher/cards/*.json (cards win), enriched with live mod versions from mods/<Mod>/manifest.json.

Status tiers:
  - "green"  = COMPLETE (ONLY set when explicitly declared done by the owner; never auto-assigned)
  - "active" = ACTIVELY WORKING (currently in active development right now, with version + work note)
  - "yellow" = IN PROGRESS (auto-promoted from "red" once version >= 0.3.0, or after active work moves elsewhere)
  - "red"    = EARLY / UNTESTED (early stub < 0.3.0)
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
    "MOMMY MILKERS": ["mommymilkers", "mommy"],
}

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
    "MOMMY MILKERS": "MommyMilkers",
}


def parse_ver(v: str):
    nums = [int(x) for x in re.findall(r"\d+", v or "")]
    while len(nums) < 3:
        nums.append(0)
    return tuple(nums[:3])


def read_mod_version(name: str) -> str:
    folder = MOD_FOLDER.get(name)
    if not folder:
        return ""
    manifest = root / "mods" / folder / "manifest.json"
    if manifest.is_file():
        try:
            data = json.loads(manifest.read_text(encoding="utf-8-sig"))
            if data.get("version_number"):
                return str(data["version_number"]).strip()
        except Exception:
            pass
    src_dir = root / "src" / folder
    if src_dir.is_dir():
        for cs in src_dir.glob("*.cs"):
            try:
                m = re.search(r'PluginVersion\s*=\s*"([^"]+)"', cs.read_text(encoding="utf-8-sig"))
                if m:
                    return m.group(1).strip()
            except Exception:
                pass
    return ""


def auto_status(raw_status: str, version: str) -> str:
    st = (raw_status or "red").strip().lower()
    # "green" is ONLY assigned when the owner explicitly declares the character done.
    if st == "green":
        return "green"
    # "active" / "working" marks the character(s) currently being actively developed right now.
    if st in ("active", "working"):
        return "active"
    # Auto-promote "red" -> "yellow" once the mod version climbs to >= 0.3.0.
    if st == "red" and version and parse_ver(version) >= (0, 3, 0):
        return "yellow"
    return st if st in ("yellow", "red") else "yellow"


entries = {}
files = [root / "launcher" / "roster.json"] + sorted((root / "launcher" / "cards").glob("*.json"))
for f in files:
    for c in json.loads(f.read_text(encoding="utf-8-sig")).get("characters", []):
        name = c.get("name", "").strip()
        if name:
            key = name.upper()
            prev = entries.get(key, {})
            entries[key] = {
                "name": key,
                "status": c.get("status", prev.get("status", "red")),
                "version": c.get("version", prev.get("version", "")),
                "note": c.get("note", prev.get("note", "")),
            }

out = []
for name, info in entries.items():
    ver = info["version"] or read_mod_version(name)
    status = auto_status(info["status"], ver)
    match = MATCH.get(name) or [re.sub(r"[^a-z0-9]", "", name.lower())]
    item = {"name": name, "status": status, "match": match}
    if ver:
        item["version"] = ver
    if info.get("note"):
        item["note"] = info["note"]
    out.append(item)

dest = root / "mods" / "CharacterStatus" / "status.json"
dest.parent.mkdir(parents=True, exist_ok=True)
dest.write_text(json.dumps({"characters": out}, indent=2) + "\n", encoding="utf-8")
print("wrote", dest, len(out), "characters")
for o in out:
    print("  %-18s %-8s %-8s %s" % (o["name"], o["status"], o.get("version", ""), o.get("note", "")))
