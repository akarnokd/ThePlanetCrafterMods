// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatFlatlands
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featflatlands", "(Feat) Flatlands", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationpolish", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationestonian", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationukrainian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static Plugin me;

        static ConfigEntry<bool> isEnabled;

        static ConfigEntry<bool> debugMode;

        static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            me = this;
            logger = Logger;

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging (chatty!)");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(string message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetList), "InitPlanetList")]
        static void PlanetList_InitPlanetList(ref PlanetData[] ____planets)
        {
            if (!isEnabled.Value)
            {
                return;
            }
            Log("Checking Flatlands to the known planet list");
            var newPlanets = new List<PlanetData>(____planets);

            if (newPlanets.Any(d => d != null && d.id == "Flatlands"))
            {
                Log("Done, already setup.");
                return;
            }
            Log("  Locating Prime to become a template");
            var flatlands = Instantiate(newPlanets.Find(d => d.id == "Prime"));
            DontDestroyOnLoad(flatlands);
            Log("  Preparing Flatlands based on Prime");
            flatlands.id = "Flatlands";
            flatlands.name = "Flatlands";
            flatlands.meteoEvents.Clear();
            flatlands.tutorialSteps.Clear();
            flatlands.spawnPositions = [
                new() {
                        id = "Default",
                        positions = [
                            new(new(0, 100, 0), Quaternion.identity)
                        ]
                    }
            ];
            flatlands.availableStoryEvents.Clear();
            flatlands.manualStoryEvents.Clear();

            var idx = newPlanets.FindLastIndex(d => d.startingPlanet);
            Log("  Inserting Flatlands into the known planet list at " + (idx + 1));
            newPlanets.Insert(idx + 1, flatlands);
            Log("  Updating the known planet list");
            ____planets = [.. newPlanets];
            Log("Done adding Flatlands");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetUnlocker), nameof(PlanetUnlocker.LoadUnlockedPlanets))]
        static void PlanetUnlocker_LoadUnlockedPlanets()
        {
            // make sure planetlist is initialized
            Managers.GetManager<PlanetLoader>().planetList.GetPlanetList(true);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpacePlanetView), nameof(SpacePlanetView.InitPlanetSpaceView))]
        static void SpacePlanetView_InitPlanetSpaceView(PlanetList ____planetList)
        {
            // make sure planetlist is initialized
            if (____planetList != null)
            {
                ____planetList.GetPlanetList(true);
            }
            else
            {
                var pl = Managers.GetManager<PlanetLoader>();
                if (pl != null)
                {
                    pl.planetList.GetPlanetList(true);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        static void PlanetLoader_HandleDataAfterLoad(PlanetData ____selectedPlanet)
        {
            if (!isEnabled.Value)
            {
                return;
            }
            if (____selectedPlanet.id != "Flatlands")
            {
                return;
            }
            Log("FlatLands preparations after loading");
            Log("  Find /World");
            var world = GameObject.Find("World");
            Log("  Find /World/Terrains");
            var terrains = world.transform.Find("Terrains");
            Log("  Find /World/Terrains/Surface");
            var surface = terrains.transform.Find("Surface");
            Log("  Find /World/Terrains/Surface/Terrain-0");
            var terrain0 = surface.transform.Find("Terrain-0");

            var newSize = 4000;

            Log("  Moving all terrains out of the way");
            for (int i = terrains.transform.childCount - 1; i >= 0; i--)
            {
                var c = terrains.transform.GetChild(i);
                c.transform.position += new Vector3(0, -10000, 0);
            }
            Log("  Moving all surfaces out of the way");
            for (int i = surface.transform.childCount - 1; i >= 0; i--)
            {
                var c = surface.transform.GetChild(i);
                c.transform.position += new Vector3(0, -10000, 0);
            }

            Log("  Duplicating the default terrain");
            var terrainNew = Instantiate(terrain0.gameObject);
            terrainNew.name = "Flatworld";
            var terrain = terrainNew.GetComponent<Terrain>();
            var terrainData = terrain.terrainData;

            var res = terrainData.heightmapResolution;
            Log("  Gettig the terrain height map");
            var heights = terrainData.GetHeights(0, 0, res, res);

            Log("  Flattening the terrain height map");
            for (int i = 0; i < res; i++)
            {
                for (int j = 0; j < res; j++)
                {
                    heights[i, j] = 0f;
                }
            }
            Log("  Updating the terrain height map");
            terrainData.SetHeights(0, 0, heights);

            Log("  Placing the terrain height map");
            var s = terrainData.size.y;
            terrainData.size = new Vector3(newSize, s, newSize);
            terrainNew.transform.position = new Vector3(-newSize / 2, 100, -newSize / 2);

            Log("  Hiding /World/Water");
            world.transform.Find("Water").gameObject.SetActive(false);
            Log("  Hiding /World/Sectors");
            world.transform.Find("Sectors").gameObject.SetActive(false);
            Log("  Hiding /World/Always_Visibles");
            world.transform.Find("Always_Visibles").gameObject.SetActive(false);
            Log("  Hiding /World/OreVeins");
            world.transform.Find("OreVeins").gameObject.SetActive(false);
            Log("  Hiding /World/OnlyOnMap");
            var onlyOnMap = world.transform.Find("OnlyOnMap").gameObject;
            onlyOnMap.SetActive(false);
            if (onlyOnMap.GetComponent<DisableParent>() == null)
            {
                onlyOnMap.AddComponent<DisableParent>();
            }
            Log("  Hiding /World/WorldVegetation");
            world.transform.Find("WorldVegetation").gameObject.SetActive(false);
            Log("  Hiding /World/WorldMarkers");
            world.transform.Find("WorldMarkers").gameObject.SetActive(false);
            Log("  Hiding /World/Occluders");
            world.transform.Find("Occluders").gameObject.SetActive(false);

            Log("  Hiding /World/Asteroids");
            var asteroidsGo = world.transform.Find("Asteroids");
            var asteroid = asteroidsGo.GetComponent<AsteroidsHandler>();
            asteroidsGo.gameObject.SetActive(false);
            Log("  Disabling asteroids");
            asteroid.InitAsteroidsHandler();

            Log("Done preparations.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        static void PlanetLoader_HandleDataAfterLoad_Post(PlanetData ____selectedPlanet)
        {
            if (!isEnabled.Value)
            {
                return;
            }
            if (____selectedPlanet.id != "Flatlands")
            {
                return;
            }

            me.StartCoroutine(HidePodAfterLoad());

        }

        static IEnumerator HidePodAfterLoad()
        {
            yield return new WaitForSecondsRealtime(10);
            var woh = WorldObjectsHandler.Instance;
            if (woh == null)
            {
                yield break;
            }
            List<WorldObject> list = [.. woh.GetAllWorldObjects().Values];
            Log("  Hiding Escape pods (" + list.Count + " world objects loaded)");
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var wo = list[i];
                //Log("    " + wo.id + " at " + wo.pos);
                if (wo.GetGroup().id == GameConfig.groupIdForEscapePod && wo.GetId() < 300_000_000)
                {
                    Log("     Escape pod: " + wo.GetId() + " at " + wo.GetId());
                    wo.SetPositionAndRotation(new(0, 0, 0), wo.GetRotation());
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        go.transform.position = wo.GetPosition();
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
                Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["Planet_Flatlands"] = "Laposföld";
                dict["Planet_Desc_Flatlands"] = "Az egész bolygó lapos. Se hegyek, se vizek. Kreatív mód ajánlott.";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["Planet_Flatlands"] = "Flatlands";
                dict["Planet_Desc_Flatlands"] = "The entire planet is flat. No mountains, no water. Creative mode recommended.";
            }
        }

        class DisableParent : MonoBehaviour
        {
            void OnEnable()
            {
                gameObject.SetActive(false);
            }
        }
    }
}
