using BepInEx;
using BepInEx.Logging;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Text;
using System.IO;
using UnityEngine;

namespace LibModLoadSaveSupport
{
    /// <summary>
    /// Manages the saving and loading of custom data (plugin save states).
    /// </summary>
    [BepInPlugin(pluginGuid, "(Lib) Support Mods with Load n Save", "1.0.0.1")]
    public class LibModLoadSaveSupportPlugin : BaseUnityPlugin
    {

        const string pluginGuid = "akarnokd.theplanetcraftermods.libmodloadsavesupport";
        /// <summary>
        /// The dictionary referencing the load-save callbacks
        /// for a string guid used for registering it.
        /// </summary>
        private static readonly Dictionary<string, LoadSaveRegistry> registry 
            = new Dictionary<string, LoadSaveRegistry>();

        /// <summary>
        /// Stores the plugin save data to be deferred until the original SessionController.Start
        /// executed so that all game object data is initialized properly.
        /// </summary>
        private static readonly Dictionary<string, string> savedata = new Dictionary<string, string>();

        private static ManualLogSource logger;

        /// <summary>
        /// Register an onLoad and onSave callback for a given guid.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="onLoad"></param>
        /// <param name="onSave"></param>
        /// <returns>The IDisposable to call to unregister these specific callbacks</returns>
        public IDisposable RegisterLoadSave(string guid, Action<string> onLoad, Func<string> onSave)
        {
            if (registry.TryGetValue(guid, out LoadSaveRegistry reg))
            {
                reg.Dispose();
            }
            reg = new LoadSaveRegistry();
            reg.guid = guid;
            reg.onLoad = onLoad;
            reg.onSave = onSave;
            registry[guid] = reg;

            return reg;
        }

        /// <summary>
        /// Structure to remember what load and save callbacks to use.
        /// </summary>
        internal class LoadSaveRegistry : IDisposable
        {
            internal string guid;
            internal Action<string> onLoad;
            internal Func<string> onSave;

            public void Dispose()
            {
                registry.Remove(guid);
                guid = null;
                onLoad = null;
                onSave = null;
            }
        }

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(LibModLoadSaveSupportPlugin));
        }

        private void OnDestroy()
        {
            Logger.LogInfo($"Plugin destroyed!");

            registry.Clear();
            savedata.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), "SaveStringsInFile")]
        static void JSONExport_SaveStringsInFile(List<string> _saveStrings)
        {
            if (registry.Count != 0)
            {
                StringBuilder sb = new StringBuilder(32 * 1024);
                // This is the marker for the section of this plugin's data
                sb.Append(pluginGuid).Append("|");

                foreach (LoadSaveRegistry reg in registry.Values)
                {
                    sb.Append(reg.guid);
                    sb.Append(':');
                    sb.Append(reg.onSave());
                    sb.Append('|');
                }
                sb.Remove(sb.Length - 1, 1);
                _saveStrings.Add(sb.ToString());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), nameof(JSONExport.LoadFromJson))]
        static bool JSONExport_LoadFromJson(
                    string _saveFileName, 
                    ref bool __result,
                    ref JsonableWorldState ___worldState,
                    ref JsonablePlayerState ___playerState,
                    ref JsonablePlayerStats ___playerStats,
                    ref JsonableGameState ___gameState,
                    ref List<JsonableWorldObject> ___worldObjects,
                    ref List<JsonableInventory> ___inventories,
                    ref List<JsonableMessage> ___messages,
                    ref List<JsonableStoryEvent> ___storyEvents,
                    ref List<JsonableTerrainLayer> ___terrainLayers,
                    char ___chunckDelimiter,
                    char ___listDelimiter
        )
        {
            // Unfortunately, the entire method has to be rewritten
            savedata.Clear();
            try
            {
                // clear jsonable state
                ___worldState = new JsonableWorldState();
                ___playerState = new JsonablePlayerState();
                ___playerStats = new JsonablePlayerStats();
                ___gameState = new JsonableGameState();
                ___worldObjects = new List<JsonableWorldObject>();
                ___inventories = new List<JsonableInventory>();
                ___messages = new List<JsonableMessage>();
                ___storyEvents = new List<JsonableStoryEvent>();
                ___terrainLayers = new List<JsonableTerrainLayer>();

                // load the file
                string worldStateSaveFilePath = string.Format("{0}/{1}.json", UnityEngine.Application.persistentDataPath, _saveFileName);

                logger.LogInfo("Loading " + worldStateSaveFilePath);

                string text = File.ReadAllText(worldStateSaveFilePath);

                // split the file along the chunk delimiters (default @)
                string[] lines = text.Split(new char[] { ___chunckDelimiter });

                JsonUtility.FromJsonOverwrite(lines[0], ___worldState);
                JsonUtility.FromJsonOverwrite(lines[1], ___playerState);
                JsonUtility.FromJsonOverwrite(lines[4], ___playerStats);
                ParseJsonList(lines[2], ___worldObjects, ___listDelimiter);
                ParseJsonList(lines[3], ___inventories, ___listDelimiter);

                // scan through the lest of the lines and pick out the
                // pluginGuid marker
                for (int i = 5; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int j = line.IndexOf(pluginGuid);
                    if (j >= 0)
                    {
                        logger.LogInfo("Found savedata " + pluginGuid);
                        ParsePluginData(line.Substring(j + pluginGuid.Length + 1), ___listDelimiter);
                        // Remove this gui entry from the array of lines 
                        // so the next section looks like a vanilla save if necessary
                        string[] newlines = new string[lines.Length - 1];
                        Array.Copy(lines, 0, newlines, 0, i);
                        Array.Copy(lines, i + 1, newlines, i, lines.Length - i - 1);
                        lines = newlines;
                        break;
                    }
                }

                // check if any optional fields are there and load them
                if (lines.Length > 5)
                {
                    ParseJsonList(lines[5], ___messages, ___listDelimiter);
                }
                if (lines.Length > 6)
                {
                    ParseJsonList(lines[6], ___storyEvents, ___listDelimiter);
                }
                if (lines.Length > 7)
                {
                    JsonUtility.FromJsonOverwrite(lines[7], ___gameState);
                }
                if (lines.Length > 8)
                {
                    ParseJsonList(lines[8], ___terrainLayers, ___listDelimiter);
                }
                __result = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                logger.LogError(ex.StackTrace);
                __result = false;
            }

            return false;
        }

        static void ParseJsonList<T>(string line, List<T> output, char listDelimiter)
        {
            line = line.Trim();
            if (line.Length != 0)
            {
                string[] elements = line.Split(new char[] { listDelimiter });
                if (elements.Length != 0)
                {
                    output.Capacity = Math.Max(output.Capacity, elements.Length);
                    foreach (string element in elements)
                    {
                        output.Add(JsonUtility.FromJson<T>(element));
                    }
                }
            }
        }

        static void ParsePluginData(string line, char listDelimiter)
        {
            line = line.Trim();
            if (line.Length != 0) { 
                string[] elements = line.Split(new char[] { listDelimiter });
                if (elements.Length != 0)
                {
                    foreach (string element in elements)
                    {
                        int i = element.IndexOf(':');
                        if (i != -1)
                        {
                            string guid = element.Substring(0, i).Trim();
                            string content = element.Substring(i + 1);

                            savedata[guid] = content;

                            logger.LogInfo("Savedata for " + guid + " (" + content.Length + " chars)");
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            try
            {
                foreach (string guid in savedata.Keys)
                {
                    string content = savedata[guid];

                    if (registry.TryGetValue(guid, out LoadSaveRegistry reg))
                    {
                        try
                        {
                            logger.LogInfo("LibModLoadSavePlugin handing over content to GUID " + guid);
                            reg.onLoad?.Invoke(content);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("LibModLoadSaveSupportPlugin failed to load content for GUID " + guid);
                            logger.LogError(ex.Message);
                            logger.LogError(ex.StackTrace);
                        }
                    }
                }
            }
            finally
            {
                savedata.Clear();
            }
        }
    }
}
