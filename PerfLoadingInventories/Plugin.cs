using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PerfLoadingInventories
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfloadinginventories", "(Perf) Loading Inventories Faster", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "LoadInventoriesData")]

        static bool SavedDataHandler_LoadInventoriesData(List<JsonableInventory> _jsonableInventories)
        {
            List<WorldObject> all = WorldObjectsHandler.GetAllWorldObjects();
            Dictionary<int, WorldObject> objectMap = new Dictionary<int, WorldObject>(10 + all.Count);
            foreach (WorldObject wo in all)
            {
                int key = wo.GetId();
                if (!objectMap.ContainsKey(key)) {
                    objectMap[key] = wo;
                }
            }
            List<Inventory> list = new List<Inventory>();
            foreach (JsonableInventory jsonableInventory in _jsonableInventories)
            {
                List<WorldObject> list2 = new List<WorldObject>();
                foreach (string text in jsonableInventory.woIds.Split(new char[]
                {
                    ','
                }))
                {
                    if (!(text == ""))
                    {
                        int id;
                        int.TryParse(text, out id);
                        WorldObject worldObject;
                        if (objectMap.TryGetValue(id, out worldObject))
                        {
                            list2.Add(worldObject);
                        }
                    }
                }
                Inventory item = new Inventory(jsonableInventory.id, jsonableInventory.size, list2);
                list.Add(item);
            }
            InventoriesHandler.SetAllWorldInventories(list);

            return false;
        }
    }
}
