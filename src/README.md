# Mod source code

Put a mod's source here as `src/<ModFolderName>/` with its `.csproj` (for example `src/WilburMod/WilburMod.csproj`).
On every push GitHub compiles it and the fresh dll replaces `mods/<ModFolderName>/` in the download.
Assets (Icons, Sounds, bundles) stay in `mods/<ModFolderName>/`.

The project must build without the game installed: reference the game through NuGet, like `DuelistMod.csproj` does
(BepInEx.Core, UnityEngine.Modules, RiskOfRain2.GameLibs, R2API.* packages), not through HintPaths to a local game folder.
If a mod fails to build, the run shows the errors and the download keeps that mod's previous dll.
