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

## Character status (green / yellow / red)
- Every roster entry/card has `"status"`: `green` = complete, `yellow` = in progress, `red` = early / untested. The launcher
  card shows it as a tag and `src/CharacterStatus/` adds a status line to each survivor's description in game.
- The owner sets statuses by clicking answers in the chat (AskUserQuestion). To change one: edit `"status"` in
  `launcher/roster.json` (existing characters) or `launcher/cards/<Mod>.json` (new ones; ALWAYS give a new card a status),
  run `python3 release/sync_status.py` (rewrites `mods/CharacterStatus/status.json`; add a MATCH entry there for new names), commit both.
- Current: green Billy Bigsuits, Brother, Clay Rebel, Reaper; yellow CarMax, Audi Technician, Wilbur, Nidus, Phantom Assassin,
  Infested General, Emberwarden, Duelist; red Surveyor, Ashwing Drake, Gravitos.

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
