using BepInEx;
using BepInEx.Logging;
using R2API.Utils;

// Safety net for the publicized GameLibs: asks Mono to skip access checks, so a private game member that slips
// through compiles AND runs. The known private members are still accessed properly (reflection).
#pragma warning disable CS0618
[assembly: System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SpecialForces
{
    // Special Forces: an alternative game mode (main menu > Alternate game modes). Up to 12 players pick survivors,
    // land by dropship next to a control tower on Stormreach Valley (a 1 km rainy night map), defend it against
    // escalating waves from the Hurricane, push north killing Storm Nests for items, build defences, and kill the
    // Hurricane. Inspired by StarCraft II's "Special Forces Elite 5". See CLAUDE.md.
    [BepInDependency("com.bepis.r2api")]
    [BepInDependency("com.bepis.r2api.prefab")]
    [BepInDependency("com.bepis.r2api.language")]
    [BepInDependency("com.bepis.r2api.content_management")]
    [BepInDependency("com.bepis.r2api.networking")]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class SpecialForcesPlugin : BaseUnityPlugin
    {
        public const string GUID = "com.VincentMacaluso.SpecialForces";
        public const string NAME = "SpecialForces";
        public const string VERSION = "1.0.4";

        internal static ManualLogSource Log;
        internal static string Directory;

        private void Awake()
        {
            Log = Logger;
            Directory = System.IO.Path.GetDirectoryName(Info.Location);

            SFConfig.Init(Config);
            Safe("tokens", Tokens.Init);
            Safe("sounds", () => SFAudio.Init(Directory));
            Safe("assets", () => SFAssets.Init(Directory));
            Safe("players", SFPlayers.Init);
            Safe("network", SFNet.Init);
            Safe("structures", SFStructures.Init);
            Safe("hurricane", SFBoss.Init);
            Safe("model container", SFModelContainer.Init);
            Safe("effects", SFEffects.Init);
            Safe("game mode", SFGameMode.Init);
            Safe("scene", SFScene.Init);
            Safe("building", SFBuilding.Init);
            Safe("menu", SFMenu.Init);

            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }

        // One part failing (e.g. a game asset missing in a new game version) is logged and the rest still loads.
        private static void Safe(string what, System.Action init)
        {
            try { init(); }
            catch (System.Exception e) { Log.LogError($"Special Forces: {what} failed to set up: {e}"); }
        }

        private void Update()
        {
            SFBuilding.ClientUpdate();
            SFAudio.Tick();
        }
    }
}
