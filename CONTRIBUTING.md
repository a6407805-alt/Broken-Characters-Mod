# Contributing to Broken Characters Collection

A just-for-fun mod pack between friends. Not for public release.

This repo is a Risk of Rain 2 mod pack: the Duelist (repo root) plus the group's other mods. Every push to `main`
is compiled by GitHub Actions, the installer is tested on Windows, and the result is published as the
`latest` release (`Broken-Characters-Collection.zip`). Players update with `Update Broken Characters.bat`.

## Where things go

| What | Where |
|---|---|
| The Duelist's source | repo root (`*.cs`, `DuelistMod.csproj`) |
| Another mod's **source** | `src/<ModFolder>/` with its `.csproj` (e.g. `src/WilburMod/WilburMod.csproj`) |
| Another mod's **files that ship** (icons, sounds, asset bundles, or a prebuilt dll) | `mods/<ModFolder>/` |
| Installer / packaging | `release/`, `.github/workflows/build.yml` |

- A mod in `src/<ModFolder>/` is compiled on every push and its dll lands in `mods/<ModFolder>/` in the download.
  If it fails to build, the run shows the errors and the download keeps that mod's last good dll.
- Projects must build **without the game installed**: reference the game through NuGet like `DuelistMod.csproj`
  (BepInEx.Core, UnityEngine.Modules, RiskOfRain2.GameLibs, R2API.* packages). No HintPaths to a local game folder.
  The repo's `NuGet.config` already has the BepInEx feed.
- Library mods by other people (R2API, HookGenPatcher, etc.) are **not** stored here; the installer downloads them
  from Thunderstore at pinned versions (top of `release/install.ps1`). If your mod needs another library, add it there.
- This repo is **public**. Anything copyrighted (sounds, art or models from other games, etc.) goes in the **private**
  repo `a6407805-alt/BrokenCharacters-Extras` under `files/<ModFolder>/` instead; the installer adds it for anyone
  with the extras key. Never commit copyrighted files here, and never put the extras key in this repo.

## How to contribute (for you and your Claude)

1. Work on a **branch**, not `main`: `git checkout -b <yourname>/<what-you-changed>`.
2. Only change your own mod's folders (`src/<YourMod>/`, `mods/<YourMod>/`) unless you've agreed otherwise.
3. Push the branch and open a **pull request** to `main` describing what changed and how to test it.
4. GitHub Actions builds it; fix any errors it reports (`gh run list -R a6407805-alt/DuelistMod`).
5. Once it's reviewed and merged, the new download is published automatically.

Multiplayer needs everyone on the same build, so tell the group when a merge goes in.
