# Contributing to Broken Characters Mod

A just-for-fun mod pack between friends. Not for public release.

This repo is a Risk of Rain 2 mod pack: the Duelist (`src/Duelist`) plus the group's other mods. Every push to `main`
is compiled by GitHub Actions, the installer is tested on Windows, and the result is published as the
`latest` release (`Broken-Characters-Mod.zip`). Players update with `Update Broken Characters.bat`.

## Where things go

| What | Where |
|---|---|
| The Duelist's source | `src/Duelist/` (`*.cs`, `DuelistMod.csproj`, models as `.bin`) |
| Another mod's **source** | `src/<ModFolder>/` with its `.csproj` (e.g. `src/WilburMod/WilburMod.csproj`) |
| Another mod's **files that ship** (icons, sounds, asset bundles, or a prebuilt dll) | `mods/<ModFolder>/` |
| Installer / packaging | `release/`, `.github/workflows/build.yml` |
| Unity source files for asset bundles (not shipped) | `unity/<ModFolder>/` |
| The launcher (Windows app) | `launcher/` |

- A mod in `src/<ModFolder>/` is compiled on every push and its dll lands in `mods/<ModFolder>/` in the download.
  If it fails to build, the run shows the errors and the download keeps that mod's last good dll.
- Projects must build **without the game installed**: reference the game through NuGet like `src/Duelist/DuelistMod.csproj`
  (BepInEx.Core, UnityEngine.Modules, RiskOfRain2.GameLibs, R2API.* packages). No HintPaths to a local game folder.
  The repo's `NuGet.config` already has the BepInEx feed.
- Library mods by other people (R2API, HookGenPatcher, etc.) are **not** stored here; the installer downloads them
  from Thunderstore at pinned versions (top of `release/install.ps1`). If your mod needs another library, add it there.
- This repo is **public**. Anything copyrighted (sounds, art or models from other games, etc.) goes in the **private**
  repo `a6407805-alt/BrokenCharacters-Extras` under `files/<ModFolder>/` instead; the installer adds it for anyone
  with the extras key. Never commit copyrighted files here, and never put the extras key in this repo.

## How to contribute (for you and your Claude)

Both owners of the pack can **push straight to `main`**: no pull request or approval needed.

1. Pull first so you build on the latest version: `git pull --rebase`.
2. Make your change and commit it with a clear message (it shows up as the changelog in the launcher).
3. Push to `main`. GitHub Actions builds it, tests the installer and publishes the new download automatically.
4. Check the build passed (`gh run list -R a6407805-alt/Broken-Characters-Mod`). If it failed, the previous download
   stays published, so fix it and push again.
5. If the push is rejected because the other person pushed first, run `git pull --rebase` and push again.

Tips:
- Mostly change your own mod's folders (`src/<YourMod>/`, `mods/<YourMod>/`); tell each other before changing shared
  files (`release/`, `launcher/`, `.github/`, the Duelist).
- A branch and pull request is still handy for a big or risky change you want the other person to look at first,
  but it's optional.
- Every push reaches everyone's updater, so test in game before pushing when you can.

Multiplayer needs everyone on the same build, so tell the group when a merge goes in.

## Adding a new character (checklist for you and your Claude)

1. **Source:** `src/<Name>/<Name>.csproj` + code. Copy the setup of `src/CarMaxSpecialist/` (NuGet references,
   `netstandard2.1`, `BepInDependency` on the R2API modules you use). A new mod needs no workflow changes: every
   `src/*/*.csproj` is built automatically and its dll lands in `mods/<Name>/`.
2. **Unique names:** your own BepInEx GUID (`com.<you>.<name>`), language token prefix and prefab names, so nothing
   clashes with the other mods.
3. **Files that ship:** icons in `mods/<Name>/Icons/` (`portrait.png`, `primary.png`, ...), sounds in
   `mods/<Name>/Sounds/`, plus a short `README.md` and `manifest.json`.
4. **Launcher card:** add an entry at the top of `launcher/roster.json`
   (`name`, `tagline`, `added` = now in ISO format, `accent` = hex colour, `image` = `Assets/Roster/<Name>.png`) and
   put a square portrait at `launcher/Assets/Roster/<Name>.png`. The newest `added` date sits on the far left with a
   NEW badge. Launchers read this file from the repo, so the card shows up without a new launcher.
5. **Copyrighted files** (sounds, models, art from other games or shows) go in the private
   `BrokenCharacters-Extras` repo under `files/<Name>/`, never here.
6. Push, check the build (`gh run list`), then test in game.

### Gotchas we already hit (save yourself the time)

- **Shaders:** `Shader.Find` only works for the game's own shaders. Use `"Hopoo Games/Deferred/Standard"` (fall back to
  `LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard")`). `"Sprites/Default"` and `"Standard"` throw and
  stop your skill mid-way. For lines, borrow a material from a game prefab (e.g. `TracerCommandoDefault`).
- **Publicized game library:** GameLibs makes private fields look public, and using them compiles but crashes in
  game with `FieldAccessException`. Known trap: `characterBody.transform` → use `characterBody.gameObject.transform`.
- **Networking:** damage via `BulletAttack`/`BlastAttack` from the player's own client (`isAuthority`); gold, items,
  buffs and summons on the host (`NetworkServer.active`). Skill states also run on the host, so host-only work can
  go in `OnEnter` behind `if (NetworkServer.active)`.
- **Sounds:** Unity audio is off in RoR2. Either play vanilla events with `Util.PlaySound("Play_...", gameObject)` or
  play `.wav` files through winmm like `CarMaxSpecialist/Ui.cs` (`CmxAudio`).
- **Debugging:** everything that goes wrong in game is in `Risk of Rain 2\BepInEx\LogOutput.log`. Give that file to
  your Claude first; it shows the exact line that broke.
