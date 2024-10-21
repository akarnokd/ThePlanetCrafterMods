// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine;
using LibCommon;
using System;

namespace CheatMinimap
{
    public partial class Plugin
    {
        void UpdatePhoto()
        {
            if (photographMap.Value)
            {
                var healthGauge = AccessTools.Field(typeof(GaugesConsumptionHandler), "baseHealthChangeValuePerSec");
                healthGauge.SetValue(null, 0.0001f);
                var waterGauge = AccessTools.Field(typeof(GaugesConsumptionHandler), "baseThirstChangeValuePerSec");
                waterGauge.SetValue(null, 0.0001f);
                var oxygenGauge = AccessTools.Field(typeof(GaugesConsumptionHandler), "baseOxygenChangeValuePerSec");
                oxygenGauge.SetValue(null, 0.0001f);

                PlayersManager playersManager = Managers.GetManager<PlayersManager>();
                if (playersManager != null)
                {
                    PlayerMainController pm = playersManager.GetActivePlayerController();
                    if (Keyboard.current[Key.U].wasPressedThisFrame)
                    {
                        if (photoroutine == null)
                        {
                            Assembly me = Assembly.GetExecutingAssembly();
                            string dir = Path.GetDirectoryName(me.Location) + "\\map";

                            if (!Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            else
                            {
                                foreach (string f in Directory.EnumerateFiles(dir))
                                {
                                    string n = Path.GetFileName(f);
                                    if (n.StartsWith("map_") && n.EndsWith(".png"))
                                    {
                                        File.Delete(f);
                                    }
                                }
                            }

                            photoroutine = PhotographMap(dir, pm);
                        }
                        else
                        {
                            photoroutine = null;
                        }
                    }
                }

                if (photoroutine != null)
                {
                    if (!photoroutine.MoveNext())
                    {
                        photoroutine = null;
                    }
                }
            }
        }

        static IEnumerator photoroutine;

        void SetVisuals(int step, PlayerMainController pm)
        {
            Logger.LogInfo("    Camera");
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = step;
            Camera.main.aspect = 1;
            Camera.main.farClipPlane = 700;
            Camera.main.nearClipPlane = -400;
            Camera.main.layerCullDistances = new float[32];

            Logger.LogInfo("    RenderSettings");
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = Color.white;
            RenderSettings.ambientGroundColor = Color.white;
            RenderSettings.ambientEquatorColor = Color.white;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.sun.color = Color.white;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

            Logger.LogInfo("    Shadows");
            QualitySettings.shadows = ShadowQuality.Disable;

            var pw = pm.GetComponentInChildren<PlayerView>();
            pw.enabled = false;
            pw.damageViewVolume.weight = 0;
            pw.damageViewVolume.enabled = false;

            /*
             * The following either crashes Unity 2023.4.19f or hangs while doing a screenshot.
            Logger.LogInfo("    Lights");
            foreach (Light lg in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                lg.shadows = LightShadows.None;
                lg.color = Color.white;
                lg.range = 1000;
            }
            */
            /*
            foreach (var ps in FindObjectsOfType<ParticleSystem>())
            {
                ps.gameObject.SetActive(false);
            }
            */
        }

        public static bool allowUnload = true;
        static bool loaded;

        IEnumerator PhotographMap(string dir, PlayerMainController pm)
        {
            var q = Quaternion.Euler(new Vector3(0, 90, 0)) * Quaternion.Euler(new Vector3(90, 0, 0));
            var move = pm.GetPlayerMovable();
            move.flyMode = true;
            int playerX = (primeMapMaxX + primeMapMinX) / 2;
            int playerZ = (primeMapMaxY + primeMapMinY) / 2;
            int visualStep = (primeMapMaxX - primeMapMinX) / 2;
            pm.SetPlayerPlacement(new Vector3(playerX, 300, playerZ), q);
            SetVisuals(visualStep, pm);
            foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                var em = ps.emission;
                em.enabled = false;
            }
            AccessTools.Field(typeof(PlayerCanAct), "_hasToBeBlocked").SetValue(pm.GetPlayerCanAct(), true);

            allowUnload = false;
            Time.timeScale = 1f;

            Logger.LogInfo("Begin Sector loading");

            // disable all decoys
            var sectors = FindObjectsByType<Sector>(FindObjectsSortMode.None);
            Logger.LogInfo("Sector count: " + sectors.Length);
            foreach (Sector sector in sectors)
            {
                Logger.LogInfo("Checking a sector");
                if (sector == null || sector.gameObject == null)
                {
                    continue;
                }
                string name = sector.gameObject.name;
                Logger.LogInfo("Sector: " + name);
                if (/*name.StartsWith("Sector-Cave-") || */name.Contains("_Interior"))
                {
                    Logger.LogInfo("Sector: " + name + " Ignored");
                    continue;
                }
                bool found = false;
                /*
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.name == name)
                    {
                        found = true;
                        break;
                    }
                }
                */
                if (!found)
                {
                    var info = "Sector: " + name + " loading";
                    Logger.LogInfo(info);
                    //File.AppendAllLines(Application.persistentDataPath + "/larvae-dump.txt", new List<string>() { info });
                    loaded = false;
                    var scl = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);

                    if (scl != null)
                    {
                        scl.completed += OnSceneLoaded;
                        while (!loaded)
                        {
                            yield return 0;
                        }
                    }

                    Logger.LogInfo("        " + name + " hiding decoys");
                    foreach (GameObject gameObject in sector.decoyGameObjects)
                    {
                        gameObject.AsNullable()?.SetActive(true);
                    }
                    Logger.LogInfo("        " + name + " loaded successfully");
                }
            }

            foreach (var lg in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                lg.enabled = false;
            }


            Logger.LogInfo("Screenshot all");

            int[] ys = [50, 75, 100, 125, 150, 200, 300, 325, 350, 375, 400, 500];

            foreach (int y in ys)
            {
                Logger.LogInfo("  Place");
                pm.SetPlayerPlacement(new Vector3(playerX, y, playerZ), q);
                yield return 0;
                Logger.LogInfo("  Visuals");
                SetVisuals(visualStep, pm);
                yield return 0;
                yield return 0;
                yield return 0;
                yield return 0;
                var fn = dir + "\\map_" + y + ".png";
                // var fn = "c:\\temp\\map_" + y + ".png";
                Logger.LogInfo("  Capture @ " + y + " -> " + fn);
                try
                {
                    ScreenCapture.CaptureScreenshot(fn);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
                Logger.LogInfo("  Capture Done " + y);
                yield return 0;
            }

            allowUnload = true;
            Time.timeScale = 1f;
            yield break;
        }
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMovable), nameof(PlayerMovable.UpdatePlayerMovement))]
        static bool PlayerMovable_UpdatePlayerMovement()
        {
            return photoroutine == null;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerFallDamage), "Update")]
        static bool PlayerFallDamage_Update()
        {
            return photoroutine == null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Sector), nameof(Sector.UnloadSector))]
        static bool Sector_UnloadSector()
        {
            return allowUnload;
        }
        */

        static void OnSceneLoaded(AsyncOperation obj)
        {
            loaded = true;
        }
    }
}
