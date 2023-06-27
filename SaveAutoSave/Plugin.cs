using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using System.IO.Compression;
using System.Text;
using System.Collections;
using UnityEngine;
using BepInEx.Bootstrap;

namespace SaveAutoSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveautosave", "(Save) Auto Save", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        static ConfigEntry<float> saveDelay;

        static Func<string> multiplayerMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            saveDelay = Config.Bind("General", "SaveDelay", 5f, "Save delay in minutes. Set to 0 to disable.");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);
            }

            if (saveDelay.Value > 0)
            {
                Logger.LogInfo("Auto Save will happen every " + saveDelay.Value + " minutes");
                StartCoroutine(SaveDelayLoop());
            }
            else
            {
                Logger.LogInfo("Auto Save disabled");
            }
        }

        static IEnumerator SaveDelayLoop()
        {
            for (; ; )
            {
                yield return new WaitForSeconds(saveDelay.Value * 60);

                // Do not auto-save in client mode
                if (multiplayerMode != null && multiplayerMode.Invoke() == "CoopClient")
                {
                    continue;
                }

                PlayersManager p = Managers.GetManager<PlayersManager>();
                if (p != null && p.GetActivePlayerController() != null)
                {
                    var sdh = Managers.GetManager<SavedDataHandler>();
                    if (sdh == null)
                    {
                        logger.LogWarning("Unable to find the SavedDataHandler; can't auto save");
                    } 
                    else 
                    { 
                        sdh.SaveWorldData(null);
                    }
                }
            }
        }
        
    }
}
