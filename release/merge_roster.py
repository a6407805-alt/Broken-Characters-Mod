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
import colorsys
import datetime
import hashlib
import json
import os
import pathlib
import re
import subprocess

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

# ---------------------------------------------------------------- automatic cards for new characters
# Owner's rule: every new character gets a launcher card + picture, even if its creator never made one (mods that
# arrive through auto-publish). Any mods/<Mod> that ships a dll, isn't a utility/map/mode mod and has no card yet gets
# one from its manifest. Picture: launcher/Assets/Roster/<Mod>.png if it exists, else the build server makes
# card-<Mod>.png (from the mod's icon.png / Icons/portrait.png, or a lettered placeholder) and attaches it to the release.
NOT_CHARACTERS = {
    "BCMDevTools", "CharacterStatus", "DropPodRevive", "EffectBudgetFix", "Jukebox", "MoreEnemiesMod",
    "ScrollableLobby", "winhttp", "LocustHorde", "SpecialForces", "AudiWPB",
}
REPO = "a6407805-alt/Broken-Characters-Mod"
GENERATE = os.environ.get("BCM_CARD_IMAGES_DIR")  # set on the publish job: where to write card-<Mod>.png


def pretty_name(folder: str, manifest: dict) -> str:
    desc = str(manifest.get("description", ""))
    if ":" in desc and len(desc.split(":", 1)[0]) <= 32:
        return desc.split(":", 1)[0].strip()
    return re.sub(r"(?<=[a-z])(?=[A-Z])", " ", str(manifest.get("name") or folder))


def tagline(manifest: dict) -> str:
    desc = str(manifest.get("description", ""))
    rest = desc.split(":", 1)[1] if ":" in desc and len(desc.split(":", 1)[0]) <= 32 else desc
    first = re.split(r"(?<=[.!?])\s", rest.strip())[0].strip()
    first = re.sub(r"\s*\(.*?\)", "", first)
    first = first[:1].upper() + first[1:]
    return (first[:44].rstrip(" ,;") + "...") if len(first) > 47 else first


def accent(folder: str) -> str:
    h = int(hashlib.md5(folder.encode()).hexdigest()[:6], 16)
    r, g, b = colorsys.hsv_to_rgb((h % 360) / 360.0, 0.65, 0.95)
    return "#%02X%02X%02X" % (int(r * 255), int(g * 255), int(b * 255))


def make_picture(folder: str, name: str, color: str, out: pathlib.Path) -> bool:
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        print("  (Pillow missing: no picture for " + folder + ")")
        return False
    mod = root / "mods" / folder
    for src in (mod / "Icons" / "portrait.png", mod / "icon.png", mod / "Icons" / "icon.png"):
        if src.is_file():
            Image.open(src).convert("RGBA").resize((512, 512)).save(out)
            return True
    img = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rgb = tuple(int(color[i:i + 2], 16) for i in (1, 3, 5))
    d.ellipse((76, 56, 436, 416), fill=rgb + (255,))
    d.ellipse((96, 76, 416, 396), fill=(16, 20, 32, 255))
    initials = "".join(w[0] for w in re.findall(r"[A-Za-z0-9]+", name) if w.lower() not in ("the", "of", "a"))[:2].upper() or "?"
    font = None
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "C:/Windows/Fonts/arialbd.ttf"):
        if os.path.isfile(f):
            font = ImageFont.truetype(f, 150)
            break
    font = font or ImageFont.load_default()
    box = d.textbbox((0, 0), initials, font=font)
    d.text((256 - (box[2] - box[0]) / 2 - box[0], 236 - (box[3] - box[1]) / 2 - box[1]), initials, font=font, fill=rgb + (255,))
    img.save(out)
    return True


known_folders = set(MOD_FOLDER.values()) | {p.stem for p in cards_dir.glob("*.json")} if cards_dir.is_dir() else set(MOD_FOLDER.values())
known_names = {str(c.get("name", "")).strip().upper() for c in data["characters"]}
for mod_dir in sorted((root / "mods").iterdir()) if (root / "mods").is_dir() else []:
    folder = mod_dir.name
    if folder in NOT_CHARACTERS or folder in known_folders or not any(mod_dir.glob("*.dll")):
        continue
    try:
        manifest = json.loads((mod_dir / "manifest.json").read_text(encoding="utf-8-sig"))
    except Exception:
        manifest = {}
    name = pretty_name(folder, manifest).upper()
    if name in known_names:
        continue
    color = accent(folder)
    image = "Assets/Roster/" + folder + ".png"
    if not (launcher / image).is_file():
        image = "https://github.com/" + REPO + "/releases/latest/download/card-" + folder + ".png"
        if GENERATE:
            pathlib.Path(GENERATE).mkdir(parents=True, exist_ok=True)
            make_picture(folder, name, color, pathlib.Path(GENERATE) / ("card-" + folder + ".png"))
    try:
        added = subprocess.run(["git", "log", "--diff-filter=A", "--format=%aI", "-1", "--", str(mod_dir)],
                               capture_output=True, text=True, cwd=root, timeout=20).stdout.strip()
    except Exception:
        added = ""
    ver = str(manifest.get("version_number", "")).strip()
    entry = {"name": name, "tagline": tagline(manifest), "added": added or datetime.datetime.now().astimezone().isoformat(timespec="seconds"),
             "accent": color, "status": "yellow" if ver and parse_ver(ver) >= (0, 3, 0) else "red", "version": ver, "image": image}
    data["characters"].insert(0, entry)
    known_names.add(name)
    print("auto card for " + folder + ": " + name)

base_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
