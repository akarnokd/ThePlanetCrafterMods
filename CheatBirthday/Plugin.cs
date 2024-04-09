// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using BepInEx.Logging;

namespace CheatBirthday
{
    [BepInPlugin(modCheatBirthdayGuid, "(Cheat) Birthday", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatBirthdayGuid = "akarnokd.theplanetcraftermods.cheatbirthday";

        static ConfigEntry<bool> isEnabled;

        static ManualLogSource logger;

        static Plugin me;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            logger = Logger;
            me = this;

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(h, modCheatBirthdayGuid, PlanetLoader_HandleDataAfterLoad);
        }

        static void PlanetLoader_HandleDataAfterLoad(PlanetLoader __instance)
        {
            if (isEnabled.Value)
            {
                __instance.StartCoroutine(BirthdayLocator());
            }
        }

        static IEnumerator BirthdayLocator()
        {
            for (int i = 1; i < 31; i++)
            {
                yield return new WaitForSeconds(1);
                foreach (var go in FindObjectsByType<Sector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (go.name == "Sector-Birthday-Event")
                    {
                        go.gameObject.SetActive(true);
                        logger.LogInfo("Found Sector-Birthday-Event and enabled its GameObject after " + i + " seconds");
                        yield break;
                    }
                }
            }
            logger.LogInfo("Sector-Birthday-Event not found.");

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Sector), nameof(Sector.LoadSector))]
        static void Sector_LoadSector(Sector __instance)
        {
            if (isEnabled.Value)
            {
                // logger.LogInfo("SectorEnter_Start: " + __instance.gameObject.name);
                if (__instance.gameObject.name.Contains("Birthday-Event"))
                {
                    logger.LogInfo("SectorEnter_Start: " + __instance.gameObject.name);
                    me.StartCoroutine(Birtday_Loader_Wait());
                }
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
                        go2.AddComponent<SuppressCraftAnimationCall>();

                        var ac = go2.AddComponent<ActionCrafter>();
                        ac.craftableIdentifier = DataConfig.CraftableIn.CraftOvenT1;
                        ac.craftSpawn = new GameObject();
                        ac.craftSpawn.AddComponent<MeshRenderer>();
                        ac.transform.position = go2.transform.position;
                        ac.particlesOnCraft = [];
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

        class SuppressCraftAnimationCall : MonoBehaviour
        {

        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionCrafter), nameof(ActionCrafter.CraftAnimation))]
        static bool ActionCrafter_CraftAnimation(ActionCrafter __instance)
        {
            if (__instance.GetComponent<SuppressCraftAnimationCall>() != null)
            {
                __instance.PlayCraftSound();
                return false;
            }
            return true;
        }
    }
}
