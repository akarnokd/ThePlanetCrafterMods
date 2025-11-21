// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace SaveAutoSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveautosave", "(Save) Auto Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<float> saveDelay;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            saveDelay = Config.Bind("General", "SaveDelay", 5f, "Save delay in minutes. Set to 0 to disable.");

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
                yield return new WaitForSecondsRealtime(saveDelay.Value * 60);

                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
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
                    if (!sdh.IsSaving())
                    { 
                        sdh.SaveWorldData(null);
                    }
                    else
                    {
                        logger.LogInfo("Game already saving, skipping this one.");
                    }
                }
            }
        }
        
    }
}
