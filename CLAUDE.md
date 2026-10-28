# Broken Characters Mod — notes for Claude

Friends-only Risk of Rain 2 mod pack (BepInEx 5, R2API, netstandard2.1). Repo: `a6407805-alt/Broken-Characters-Mod`
(public). Copyrighted files live in the private `a6407805-alt/BrokenCharacters-Extras` (`files/<Mod>/`). Two owners push
straight to `main`; read CONTRIBUTING.md for the new-character checklist and the gotchas we already hit.

## Layout
- `src/Duelist/` the Duelist (DuelistMod.csproj, models as `.bin`). `src/<Mod>/` every other mod built from source.
- `mods/<Mod>/` what ships for each group mod (dll, Icons/, Sounds/, bundles). CI copies built dlls here.
- `unity/<Mod>/` Unity sources for asset bundles (not shipped). `launcher/` WPF launcher (`roster.json` = its cards).
- `release/` installer (`install.ps1`), updater (`update.ps1`), `package.sh`. `.github/workflows/build.yml` does it all.
- Launcher cards: one file per mod in `launcher/cards/<Mod>.json` (CI merges them into `roster.json` via
  `release/merge_roster.py` and attaches the result to the release). Never rewrite `launcher/roster.json` wholesale.
- Creator window (`launcher/Publisher.cs`, `PublishWindow.xaml*`): lets any creator upload mods through the
  GitHub API. Every creator may update EVERY mod (owner's rule: one shared pack); `creators.json` just records who added a mod. Don't commit dlls into `mods/<Mod>/` when `src/<Mod>/` exists.
  Before changing a mod you don't own, check `creators.json` and `git log -- src/<Mod>`.
  Quick upload (`QuickUpload.cs`, `PublishWindow.Quick.cs`): pick/drag a friend's .zip/.dll, it's staged as
  `%TEMP%\BrokenCharacters-QuickUpload\<id>\mods\<Mod>\` and the normal scan/publish runs on that.

- Silent staging (`StagingSync.cs`, `MainWindow.Staging.cs`): a creator's launcher (key saved, not the owner) copies
  EVERY file in plugins\ (pack mods + new mod folders, not libraries) that differs from main to `staging/<login>` as
  "main + changes" with `[skip ci]`; never main. Owner sees a bell (badge = count) next to the title; it opens ReviewWindow for staging/* ahead of main:
  Approve applies the changed files on top of the NEWEST main (builds + publishes), Revert resets the branch to main.
  No creator key? Friends with the extras use the shared upload key (`launcher/keys/staging.json`, a fine-grained PAT
  locked with the extras key itself; the owner saves it in the review window) and stage as `staging/<windows user>`.
  DeAndre6969 is Vince's GitHub name (same person).

## Build & release (nothing builds locally: no dotnet in the sandbox)
- Every push to main: build Duelist + every `src/*/*.csproj` → package `Broken-Characters-Mod.zip` → Windows installer
  test → launcher exe + `launcher-preview.png` → GitHub release `latest` (also `Broken-Characters-Collection.zip` alias).
- Wait: `gh run watch <id> -R a6407805-alt/Broken-Characters-Mod --exit-status`.
- Compile errors: job logs are blocked; read annotations:
  `gh api repos/a6407805-alt/Broken-Characters-Mod/check-runs/<job_id>/annotations`. A src mod that fails shows
  `::error::<Mod> failed to build` and the pack keeps its old dll, so always check for the "built from source" notice.
- Use REST `gh api` (GraphQL blocked). Artifact downloads are blocked; fetch release assets from
  `https://github.com/a6407805-alt/Broken-Characters-Mod/releases/latest/download/<file>` instead.
- Repo settings/renames can't be done through the API; the user does those in a browser.

## Deploying to the owner's PC (when the Claude desktop app links it)
- Game: `D:\SteamLibrary\steamapps\common\Risk of Rain 2`. Mods load from `BepInEx\plugins\<Mod>\`; the Duelist from
  `BepInEx\plugins\Duelist\` (the old dev folder `plugins\DuelistMod` is retired; never re-enable its dll).
- Procedure: wait for the release, `curl` the zip, unzip only the changed mod
  (`unzip pack.zip 'Mods/<Mod>/*'`), copy files to `/mnt/user-data/outputs/<Mod>/`, then `device_commit_files` with
  `force: true` to `...\plugins\<Mod>\`. Verify with `device_list_dir` (size matches the release).
- Never run the installer on the owner's PC yourself. Game log for bugs: `BepInEx\LogOutput.log` (stage it, grep for
  the mod's namespace and "Exception").

## Character status & Active Work Tracker (green / active / yellow / red)
- Every roster entry/card has `"status"`, `"version"`, and optional `"note"`:
  - `green` (`COMPLETE`, `#4CD964`): **ONLY assigned when the owner explicitly states they are done with the character.** Never auto-assign `green`.
  - `active` (`ACTIVE WIP · vX.Y.Z` on Launcher card in Cyan `#00E5FF` / `STATUS: ACTIVELY WORKING (vX.Y.Z)` in-game): Assigned to the character(s) currently being actively developed right now, along with `"version"` and `"note"` describing what is being built/tested.
  - `yellow` (`IN PROGRESS vX.Y.Z`, `#FFD22E`): Auto-assigned once a character's version climbs to `>= 0.3.0` (via `release/sync_status.py` and `release/merge_roster.py`), or when active work shifts to another character before this one is declared `green`.
  - `red` (`EARLY · vX.Y.Z`, `#FF4D4D`): Early / untested stub (`< 0.3.0`).
- **Rule when working on any character or mod**:
  1. As you bump a mod's version (`PluginVersion` + `mods/<Mod>/manifest.json`), update its card (`launcher/cards/<Mod>.json` or `launcher/roster.json`) with `"status": "active"`, `"version": "<new_version>"`, and `"note": "<what is currently being worked on>"`.
  2. Update `mods/CharacterStatus/status.json` (and the Active Work table below) so both the Launcher and in-game Character Select show the live version and active work status. (CI also runs `python3 release/sync_status.py` automatically on every build.)
  3. Only switch a character to `"green"` when the owner explicitly states it is finished.
- **Current Status & Active Work Log**:
  - **Actively Working (`active`)**:
    - **The Surveyor** (`v0.5.0`): MMB mode wheel/radial dial, 8 hidden LiDAR map upgrades, Proton Tether & Ghost-Trap spirit capture, 3D mesh item/chest/teleporter outlines, smooth Peek dot fade, heavy motion fix.
    - **Jukebox** (`v1.1.3`, utility mod): RoR2-styled HUD panel under objective box, keys 1/2 volume boost, Vote to Skip (`0`).
  - **Complete (`green` — owner confirmed done)**: Billy Bigsuits (`v1.0.2`), Brother (`v1.0.2`), Clay Rebel (`v1.0.1`), Reaper (`v1.0.0`).
  - **In Progress (`yellow` — `>= 0.3.0`, not yet declared done)**: CarMax Specialist (`v1.0.0`), Audi Technician (`v1.0.1`), Wilbur (`v1.0.0`), Ashwing Drake (`v1.8.4`), Nidus (`v1.0.9`), Phantom Assassin (`v1.0.3`), Infested General (`v1.1.11`), Gravitos (`v1.2.0`), Emberwarden (`v1.0.0`), The Duelist (`v1.0.0`).

## Dev branch (optional, for risky experiments)
- Owner's rule (Oct 24 2026): PLAY and TEST LEVEL load EVERY character, finished or not. Unfinished characters go to `main`
  too; the owner tells friends what to expect with a **status**, not with a branch (see "Character status" below).
  `dev` is still available for risky experiments: a push to `dev` builds the mods and the launcher (no installer test)
  and publishes `Broken-Characters-Mod-dev.zip` to the `dev` pre-release. Launchers/updaters read only `latest`.
  There is ONE launcher for everyone (from main); never publish a separate dev launcher.
- Fetch: `https://github.com/a6407805-alt/Broken-Characters-Mod/releases/download/dev/Broken-Characters-Mod-dev.zip`.
- Hot reload: ScriptEngine is installed on the owner's PC (`BepInEx\plugins\ScriptEngine\`). A mod being
  hot-reloaded lives in `BepInEx\scripts\` (F6 reloads it) and must NOT also be in `plugins\`. Only code reloads;
  new bodies/skills/tokens registered at startup still need a restart.
- Dev kit: `src/BCMDevTools/` ships to everyone but is ONLY ON when started via TEST LEVEL/RELAUNCH (`_testSession`; PLAY = off) and SINGLEPLAYER ONLY (`Allowed` turns all tools off and hides the
  panel in any multiplayer lobby/run). Launcher TEST LEVEL button shows once the mod has loaded once (marker
  `%APPDATA%\BrokenCharacters\devtools-installed.txt`; flag `quicktest.flag`). Keep that guard on every new tool.

## Working cheaply (the owner asked for fewer tokens)
- Big files: Duelist `Duel.cs` (3.3k lines), `DuelUI.cs`, `Partners.cs`; `src/WilburMod/WilburPlugin.cs` (7k lines).
  Grep for what you need and read ranges; never read them whole.
- Start new characters from `src/CarMaxSpecialist/` (small, current patterns: body clone, skills, net messages, HUD,
  winmm sounds, safe materials) rather than from Wilbur or the Duelist.
- Push once per finished change, then one `gh run watch`; don't poll in loops.
- Keep replies short; the owner is a visual learner and likes concrete steps.
- Never print or commit the extras key (a `github_pat_...`).
  The password-locked copy is fine to commit: `launcher/keys/extras.json` (AES-GCM + PBKDF2, `launcher/KeyVault.cs`),
  unlocked by the guide's password box or the EXTRAS box. Creator keys have NO passwords (owner removed them).

## Jukebox (src/Jukebox + launcher JUKEBOX window)
- Every song is shared (no only-me, no Suno links anymore). UPLOAD SONGS (or drag MP3s onto the window) uploads the
  files to the `jukebox` release. `launcher/jukebox.json` lists them (`audio` = release URL) plus `removed` ids (Remove = gone
  for everyone, every PC deletes its copy). Edited with the creator key, `[skip ci]`. Only PCs with the extras key sync.
- Launcher downloads `cdn1.suno.ai/<id>.mp3` to `%APPDATA%\BrokenCharacters\Jukebox\` and writes `playlist.json`
  (`JukeboxLibrary.cs`). The mod plays mp3 directly with winmm `type mpegvideo` (`MciPlayer.cs`), volume via setaudio.
- Votes: host picks 3, players press 7/8/9, host tallies (R2API.Networking messages in `SongLibrary.cs`), each PC plays
  its own copy. Game music muted via Wwise RTPC `Volume_MSX` by reflection.
