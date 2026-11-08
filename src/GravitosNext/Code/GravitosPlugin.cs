using BepInEx;
using BepInEx.Logging;
using GravitosNext.Networking;
using R2API.Networking;
using R2API.Utils;

namespace GravitosNext;

[BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.prefab", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.language", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.content_management", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.networking", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
[BepInPlugin(GUID, NAME, VERSION)]
public class GravitosPlugin : BaseUnityPlugin
{
	public const string GUID = "com.ahren.GravitosNext";

	public const string NAME = "Gravitos NEXT";

	public const string VERSION = "0.1.0";

	internal static ManualLogSource Log;

	private void Awake()
	{
		Log = Logger;
		GravitosConfig.Init(Config);
		LodestarModel.Init(Config);
		NetworkingAPI.RegisterMessageType<GlideMessage>();
		GravitosAssets.Init();
		GravitosContent.Create();
		Hooks.Init();
		Log.LogInfo((object)"Gravitos NEXT loaded.");
	}
}
