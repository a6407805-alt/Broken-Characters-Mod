using System;
using System.Reflection;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Marks the Special Forces run (on the ClassicRun clone; the game's own Run component does the work).
    // One endless stage: no drop pods (the lander cutscene instead), no stage advance. 1.0.0 swapped the Run
    // component for a subclass, but Unity won't remove Run while RunArtifactManager requires it: the game mode was
    // never registered. Now the two differences are hooks that check for this marker.
    public class SpecialForcesRunMarker : MonoBehaviour
    {
        public static bool IsActive => Run.instance && Run.instance.GetComponent<SpecialForcesRunMarker>();
    }

    internal static class SFGameMode
    {
        public const string RunName = "SpecialForcesRun";
        public static GameObject RunPrefab;
        public static SceneDef MapScene;
        public static GameEndingDef Victory;

        public static void Init()
        {
            MapScene = ScriptableObject.CreateInstance<SceneDef>();
            ((ScriptableObject)MapScene).name = SFAssets.SceneName;
            SFUtil.SetProperty(MapScene, "cachedName", SFAssets.SceneName);
            MapScene.baseSceneNameOverride = SFAssets.SceneName;
            MapScene.sceneType = SceneType.Stage;
            MapScene.isOfflineScene = false;
            MapScene.nameToken = Tokens.Prefix + "MAP_NAME";
            MapScene.subtitleToken = Tokens.Prefix + "MAP_SUBTITLE";
            MapScene.loreToken = Tokens.Prefix + "MAP_SUBTITLE";
            MapScene.stageOrder = 99;
            MapScene.shouldIncludeInLogbook = false;
            MapScene.validForRandomSelection = false;
            MapScene.blockOrbitalSkills = false;
            MapScene.suppressPlayerEntry = false;
            // Custom stages leave the scene address empty (the scene comes from an asset bundle, loaded by name);
            // an empty reference rather than null, like a serialized SceneDef.
            FieldInfo address = typeof(SceneDef).GetField("sceneAddress");
            if (address != null)
            {
                try { address.SetValue(MapScene, Activator.CreateInstance(address.FieldType, "")); }
                catch (Exception e) { SpecialForcesPlugin.Log.LogWarning("SceneDef address: " + e.Message); }
            }
            ContentAddition.AddSceneDef(MapScene);

            Victory = ScriptableObject.CreateInstance<GameEndingDef>();
            ((ScriptableObject)Victory).name = "SpecialForcesVictory";
            SFUtil.SetProperty(Victory, "cachedName", "SpecialForcesVictory");
            Victory.isWin = true;
            Victory.showCredits = false;
            Victory.endingTextToken = Tokens.Prefix + "VICTORY";
            Victory.backgroundColor = new Color(0.10f, 0.25f, 0.40f, 0.9f);
            Victory.foregroundColor = new Color(0.55f, 0.85f, 1f);
            Victory.lunarCoinReward = 10;
            var main = RoR2Content.GameEndings.MainEnding;
            if (main)
            {
                Victory.gameOverControllerState = main.gameOverControllerState;
                Victory.icon = main.icon;
                Victory.material = main.material;
            }
            ContentAddition.AddGameEndingDef(Victory);

            BuildRunPrefab();
        }

        private static void BuildRunPrefab()
        {
            GameObject classic = SFUtil.Load<GameObject>(Keys.ClassicRun, "ClassicRun");
            if (!classic)
            {
                SpecialForcesPlugin.Log.LogError("ClassicRun prefab not found: the Special Forces game mode is unavailable.");
                return;
            }
            RunPrefab = PrefabAPI.InstantiateClone(classic, RunName, true);
            RunPrefab.AddComponent<SpecialForcesRunMarker>();
            Run run = RunPrefab.GetComponent<Run>();
            run.nameToken = Tokens.Prefix + "GAMEMODE_NAME";
            run.userPickable = true;
            // The run's first stage comes from a scene collection in game 1.5 (startingScenes is the old way):
            // a collection holding only Stormreach Valley. The private entries array is set by reflection.
            var collection = ScriptableObject.CreateInstance<SceneCollection>();
            ((ScriptableObject)collection).name = "sgSpecialForces";
            var entry = new SceneCollection.SceneEntry { sceneDef = MapScene };
            SFUtil.SetField(collection, "_sceneEntries", new[] { entry });
            run.startingSceneGroup = collection;
#pragma warning disable CS0618
            run.startingScenes = new[] { MapScene };    // older games
#pragma warning restore CS0618
            ContentAddition.AddGameMode(RunPrefab, Tokens.Prefix + "GAMEMODE_DESCRIPTION");

            On.RoR2.Run.HandlePlayerFirstEntryAnimation += (orig, self, body, position, rotation) =>
            {
                if (!self.GetComponent<SpecialForcesRunMarker>()) { orig(self, body, position, rotation); return; }
                // No drop pod: the squad is in the lander. Keep them safe until it has landed.
                if (NetworkServer.active && body) body.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, SFCutscene.Length + 3f);
            };
            On.RoR2.Run.AdvanceStage += (orig, self, nextScene) =>
            {
                if (!self.GetComponent<SpecialForcesRunMarker>()) { orig(self, nextScene); return; }
                // There is only one stage; anything that tries to move on (a stray teleporter, a portal) is ignored.
                SpecialForcesPlugin.Log.LogInfo("Special Forces: stage advance to " + (nextScene ? nextScene.cachedName : "null") + " ignored.");
            };
            SpecialForcesPlugin.Log.LogInfo("Special Forces game mode registered (console: gamemode " + RunName + ").");
        }
    }

    // 12 players: the game's lobby limit lives in two static fields (TooManyFriends raises the same ones).
    internal static class SFPlayers
    {
        public static void Init()
        {
            int n = Mathf.Clamp(SFConfig.MaxPlayers.Value, 1, 16);
            if (n <= 4) return;
            SFUtil.SetStatic(typeof(RoR2Application), "maxPlayers", n);
            SFUtil.SetStatic(typeof(RoR2Application), "hardMaxPlayers", n);
            RoR2Application.onLoad += () =>
            {
                try { RoR2.Console.instance.SubmitCmd((NetworkUser)null, "sv_maxplayers " + n, false); }
                catch (Exception e) { SpecialForcesPlugin.Log.LogWarning("sv_maxplayers: " + e.Message); }
                SpecialForcesPlugin.Log.LogInfo("Lobby limit raised to " + n + " players.");
            };
        }
    }
}
