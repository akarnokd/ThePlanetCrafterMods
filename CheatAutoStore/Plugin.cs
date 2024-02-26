// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

namespace CheatAutoStore
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautostore", "(Cheat) Auto Store", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<int> range;

        static ConfigEntry<string> includeList;

        static ConfigEntry<string> excludeList;

        static ConfigEntry<string> key;

        static InputAction storeAction;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled");
            range = Config.Bind("General", "Range", 20, "The range to look for containers within.");
            includeList = Config.Bind("General", "IncludeList", "", "The list of item ids to include only. If empty, all items are considered except the those listed in ExcludeList.");
            excludeList = Config.Bind("General", "ExcludeList", "", "The list of item ids to exclude. Only considered if IncludeList is empty.");
            key = Config.Bind("General", "Key", "<Keyboard>/K", "The input action shortcut to trigger the storing of items.");

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            storeAction = new InputAction(name: "Store Items", binding: key.Value);
            storeAction.Enable();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        public void Update()
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return;
            }
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (wh.GetHasUiOpen())
            {
                return;
            }
            if (!storeAction.WasPressedThisFrame())
            {
                return;
            }
            var ppos = ac.transform.position;

            List<int> inventoryCandidates = [];
            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var grid = wo.GetGroup().GetId();

                if (grid.StartsWith("Container")
                    && wo.GetLinkedInventoryId() != 0
                    && Vector3.Distance(ppos, wo.GetPosition()) <= range.Value
                )
                {
                    inventoryCandidates.Add(wo.GetLinkedInventoryId());
                }
            }

            var backpackInv = ac.GetPlayerBackpack().GetInventory();

            var candidateInv = new List<Inventory>();

            StartCoroutine(WaitForInventories(backpackInv, candidateInv, inventoryCandidates.Count));
        }

        static IEnumerator WaitForInventories(Inventory backpackInv, List<Inventory> candidateInventory, int n)
        {
            while (candidateInventory.Count != n)
            {
                yield return null;
            }
        }
    }
}
