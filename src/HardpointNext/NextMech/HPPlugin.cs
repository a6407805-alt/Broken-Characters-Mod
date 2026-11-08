using System.IO;
using BepInEx;
using BepInEx.Logging;
using NextMech.Components;
using R2API.Utils;

namespace NextMech;

[BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.prefab", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.language", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.content_management", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.bepis.r2api.recalculatestats", BepInDependency.DependencyFlags.HardDependency)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
[BepInDependency("com.VincentMacaluso.Hardpoint", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(GUID, NAME, VERSION)]
public class HPPlugin : BaseUnityPlugin
{
	public const string GUID = "com.ahren.HardpointNext";

	public const string NAME = "Hardpoint NEXT";

	public const string VERSION = "0.2.3";

	internal static ManualLogSource Log;

	private void Awake()
	{
		Log = base.Logger;
		HPConfig.Init(base.Config);
		NextConfig.Init(base.Config);
		HPAssets.Init();
		GunFx.Init();
		string directoryName = System.IO.Path.GetDirectoryName(base.Info.Location);
		HPAssets.LoadIcons(directoryName);
		CustomModel.Init(directoryName);
		CustomAudio.Init(directoryName);
		GhostFix.Init();
		HPContent.Create();
		Tokens.Add();
		Stagger.Init();
		PrimalArmor.Init();
		Tuning.Init();
		ACHud.Init();
		Log.LogInfo("HardpointNext loaded. Main system, engaging combat mode.");
	}
}
