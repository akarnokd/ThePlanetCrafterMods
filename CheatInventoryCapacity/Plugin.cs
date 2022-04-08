using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace CheatInventoryCapacity
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatinventorycapacity", "(Cheat) Inventory Capacity Override", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The overridden default inventory capacity.
        /// </summary>
        private static int capacity;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            capacity = Config.Bind("General", "Capacity", 250, "The overridden default inventory capacity.").Value;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), MethodType.Constructor, typeof(int), typeof(int), typeof(List<WorldObject>))]
        static void Inventory_Constructor(int _inventoryId, ref int _inventorySize, List<WorldObject> _worldObjectsInInventory)
        {
            if (_inventorySize != 1 && _inventorySize != 3)
            {
                _inventorySize = capacity;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetSize))]
        static void Inventory_SetSize(ref int _inventorySize)
        {
            if (_inventorySize != 1 && _inventorySize != 3)
            {
                _inventorySize = capacity;
            }
        }
    }
}
