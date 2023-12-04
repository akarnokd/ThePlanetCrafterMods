using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.IO;
using System.Reflection;

namespace CrimShadowVehicle
{
    [BepInPlugin("akarnokd.theplanetcraftermods.crimshadowvehicle", "(CrimShadow) Vehicle", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        // Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		private void Awake()
		{
			Plugin.bepInExLogger = base.Logger;

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);
            var path = Path.Combine(dir, "spacecraft");
            
			Logger.LogInfo("Loading asset file: " + path);
            this.vehicleAssetBundle = AssetBundle.LoadFromFile(path);
			Logger.LogInfo("Loading GameObjects");
			this.assetBundleGameObjects = new List<GameObject>(this.vehicleAssetBundle.LoadAllAssets<GameObject>());
			Logger.LogInfo("Loading GroupDataConstructibles");
			Plugin.assetBundleGroupDataConstructibles = new List<GroupDataConstructible>(this.vehicleAssetBundle.LoadAllAssets<GroupDataConstructible>());
			Logger.LogInfo("Finding the SpaceCraft GameObject");
			GameObject gameObject = this.assetBundleGameObjects.Find((GameObject go) => go.name == "SpaceCraft");
			bool flag = gameObject != null;
			if (flag)
			{
				Logger.LogInfo("Adding ActionEnterVehicle to SpaceCraft");
				gameObject.transform.Find("TriggerEnter").gameObject.AddComponent<ActionEnterVehicle>();
			}
			this.harmony.PatchAll(typeof(Plugin));
			base.Logger.LogInfo("Plugin PlanetCrafterPlugins.Vehicle_Plugin is loaded!");
		}

		// Token: 0x06000002 RID: 2 RVA: 0x00002128 File Offset: 0x00000328
		[HarmonyPrefix]
		[HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
		private static bool StaticDataHandler_LoadStaticData_Prefix(ref List<GroupData> ___groupsData)
		{
			foreach (GroupData groupData in ___groupsData)
			{
				Plugin.groupDataById[groupData.id] = groupData;
			}
			Plugin.bepInExLogger.LogInfo(string.Format("Created index of previous group data. Size = {0}", Plugin.groupDataById.Count));
			foreach (GroupDataConstructible groupDataConstructible in Plugin.assetBundleGroupDataConstructibles)
			{
				Plugin.AddGroupDataToList(ref ___groupsData, groupDataConstructible);
			}
			return true;
		}

		// Token: 0x06000003 RID: 3 RVA: 0x000021F8 File Offset: 0x000003F8
		private static void AddGroupDataToList(ref List<GroupData> groupsData, GroupData toAdd)
		{
			bool flag = Plugin.groupDataById.ContainsKey(toAdd.id);
			bool flag2 = !flag;
			if (flag2)
			{
				Plugin.bepInExLogger.LogInfo("Adding " + toAdd.id + " to group data.");
				groupsData.Add(toAdd);
				Plugin.groupDataById[toAdd.id] = toAdd;
			}
		}

		// Token: 0x06000004 RID: 4 RVA: 0x0000225C File Offset: 0x0000045C
		private void OnDestroy()
		{
			Plugin.assetBundleGroupDataConstructibles = null;
			this.assetBundleGameObjects = null;
			bool flag = this.vehicleAssetBundle != null;
			if (flag)
			{
				this.vehicleAssetBundle.Unload(true);
			}
			this.vehicleAssetBundle = null;
			this.harmony.UnpatchSelf();
		}

		// Token: 0x04000001 RID: 1
		private const string NAME_GO_ENTER_TRIGGER = "TriggerEnter";

		// Token: 0x04000002 RID: 2
		private static ManualLogSource bepInExLogger;

		// Token: 0x04000003 RID: 3
		private AssetBundle vehicleAssetBundle;

		// Token: 0x04000004 RID: 4
		private List<GameObject> assetBundleGameObjects = new List<GameObject>();

		// Token: 0x04000005 RID: 5
		private static List<GroupDataConstructible> assetBundleGroupDataConstructibles = new List<GroupDataConstructible>();

		// Token: 0x04000006 RID: 6
		private static Dictionary<string, GroupData> groupDataById = new Dictionary<string, GroupData>();

		// Token: 0x04000007 RID: 7
		private readonly Harmony harmony = new Harmony("PlanetCrafterPlugins.Vehicle_Plugin");
    }
}
