using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using BepInEx.Logging;

namespace CheatBirthday
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatbirthday", "(Cheat) Birthday", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> isEnabled;

        static ManualLogSource logger;

        static Plugin me;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            logger = Logger;
            me = this;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Sector), nameof(Sector.LoadSector))]
        static void Sector_LoadSector(Sector __instance)
        {
            if (__instance.gameObject.name.Contains("Birthday-Event"))
            {
                logger.LogInfo("SectorEnter_Start: " + __instance.gameObject.name);
                me.StartCoroutine(Birtday_Loader_Wait());
            }
        }

        static IEnumerator Birtday_Loader_Wait()
        {
            for (int j = 0; j < 30; j++)
            {
                bool ladder = false;
                bool kitchen = false;
                var go = GameObject.Find("SandCache_BirthdayEvent");
                if (go != null)
                {
                    for (int i = 0; i < go.transform.childCount; i++)
                    {
                        var child = go.transform.GetChild(i);
                        if (child.gameObject.name == "Ladder")
                        {
                            child.gameObject.SetActive(true);
                            logger.LogMessage("  Ladder found, enabled");
                            ladder = true;
                        }
                    }
                }
                else
                {
                    logger.LogMessage("  SandCache_BirthdayEvent not found (" + j + ")");
                }

                var go2 = GameObject.Find("P_Interior_Kitchen_01");
                if (go2 != null)
                {
                    if (go2.GetComponent<ActionCrafter>() == null)
                    {
                        var ac = go2.AddComponent<ActionCrafter>();
                        ac.craftableIdentifier = DataConfig.CraftableIn.CraftOvenT1;
                        ac.craftSpawn = new GameObject();
                        ac.craftSpawn.AddComponent<MeshRenderer>();
                        ac.transform.position = go2.transform.position;
                        ac.particlesOnCraft = new();
                    }
                    kitchen = true;
                }
                else
                {
                    logger.LogMessage("  P_Interior_Kitchen_01 not found (" + j + ")");
                }

                if (ladder && kitchen)
                {
                    yield break;
                }

                yield return new WaitForSeconds(1);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConstraintOnSurfaces), "LateUpdate")]
        static bool ConstraintOnSurfaces_LateUpdate(ConstraintOnSurfaces __instance)
        {
            return __instance.gameObject.GetComponent<ConstructibleGhost>() != null;
        }
    }
}
