using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System.Collections;
using UnityEngine;

namespace CheatMachineRemoteDeposit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatmachineremotedeposit", "(Cheat) Machines Deposit Into Remote Containers", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;

        static readonly Dictionary<string, string> depositAliases = new();

        static Func<Inventory, string, bool> InventoryCanAdd;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Produce detailed logs? (chatty)");

            ProcessAliases(Config.Bind("General", "Aliases", "", "A comma separated list of resourceId:aliasForId, for example, Iron:A,Cobalt:B,Uranim:C"));

            InventoryCanAdd = (inv, gid) => !inv.IsFull();

            // TODO, if Stacking then use its item dependent IsFull check.

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void ProcessAliases(ConfigEntry<string> cfe)
        {
            var s = cfe.Value.Trim();
            if (s.Length != 0)
            {
                var i = 0;
                foreach (var str in s.Split(','))
                {
                    var idalias = str.Split(':');
                    if (idalias.Length != 2)
                    {
                        Logger.LogWarning("Wrong alias @ index " + i + " value " + str);
                    }
                    else
                    {
                        depositAliases[idalias[0]] = idalias[1].ToLower();
                        log("Alias " + idalias[0] + " -> " + idalias[1]);
                    }
                    i++;
                }
            }
        }

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(
            List<GroupData> ___groupDatas,
            bool ___setGroupsDataViaLinkedGroup,
            WorldObject ___worldObject,
            List<GroupData> ___groupDatasTerraStage,
            ref WorldUnitsHandler ___worldUnitsHandler,
            TerraformStage ___terraStage
        )
        {
            if (!modEnabled.Value)
            {
                return true;
            }

            log("GenerateAnObject start");

            if (___worldUnitsHandler == null)
            {
                ___worldUnitsHandler = Managers.GetManager<WorldUnitsHandler>();
            }
            if (___worldUnitsHandler == null)
            {
                return false;
            }

            log("    begin ore search");

            Group group = null;
            if (___groupDatas.Count != 0)
            {
                List<GroupData> list = new(___groupDatas);
                if (___groupDatasTerraStage.Count != 0 && ___worldUnitsHandler.IsWorldValuesAreBetweenStages(___terraStage, null))
                {
                    list.AddRange(___groupDatasTerraStage);
                }
                group = GroupsHandler.GetGroupViaId(list[UnityEngine.Random.Range(0, list.Count)].id);
            }
            if (___setGroupsDataViaLinkedGroup)
            {
                if (___worldObject.GetLinkedGroups() != null && ___worldObject.GetLinkedGroups().Count > 0)
                {
                    group = ___worldObject.GetLinkedGroups()[UnityEngine.Random.Range(0, ___worldObject.GetLinkedGroups().Count)];
                }
                else
                {
                    group = null;
                }
            }

            // let's figure out what ore we generate

            if (group != null)
            {
                string oreId = group.id;

                log("    ore: " + oreId);

                var inventory = FindInventoryForOre(oreId);                

                if (inventory != null)
                {
                    InventoriesHandler.Instance.AddItemToInventory(group, inventory, (success, id) =>
                    {
                        if (!success)
                        {
                            log("GenerateAnObject: Machine " + ___worldObject.GetId() + " could not add " + oreId + " to inventory " + inventory.GetId());
                            if (id != 0)
                            {
                                WorldObjectsHandler.Instance.DestroyWorldObject(id);
                            }
                        }
                    });
                }
                else
                {
                    log("    No suitable inventory found, ore ignored");
                }
            }
            else
            {
                log("    ore: none");
            }

            log("GenerateAnObject end");
            return false;
        }

        /// <summary>
        /// When the vanilla sets up a Machine Generator, we have to launch
        /// the inventory cleaning routine to unclog it.
        /// Otherwise a full inventory will stop the ore generation in the
        /// MachineGenerator.TryToGenerate method.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.SetGeneratorInventory))]
        static void MachineGenerator_SetGeneratorInventory(
            MachineGenerator __instance, 
            Inventory _inventory)
        {
            __instance.StartCoroutine(ClearMachineGeneratorInventory(_inventory, __instance.spawnEveryXSec));
        }

        /// <summary>
        /// When the speed multiplier is changed, the vanilla code
        /// cancels all coroutines and starts a new generation coroutine.
        /// We have to also restart our inventory cleaning routine.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), nameof(MachineGenerator.AddToGenerationSpeedMultiplier))]
        static void MachineGenerator_AddToGenerationSpeedMultiplier(
            MachineGenerator __instance,
            Inventory ___inventory
        )
        {
            __instance.StartCoroutine(ClearMachineGeneratorInventory(___inventory, __instance.spawnEveryXSec));
        }

        static IEnumerator ClearMachineGeneratorInventory(Inventory _inventory, int delay)
        {
            var wait = new WaitForSeconds(delay);
            while (true)
            {
                // Server side is responsible for the transfer.
                if (InventoriesHandler.Instance != null && InventoriesHandler.Instance.IsServer)
                {
                    log("ClearMachineGeneratorInventory begin");
                    var items = _inventory.GetInsideWorldObjects();

                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        var item = items[i];
                        var oreId = item.GetGroup().GetId();
                        var candidateInv = FindInventoryForOre(oreId);
                        if (candidateInv != null)
                        {
                            log("    Transfer of " + item.GetId() + "(" + item.GetGroup().GetId() + ") from " + _inventory.GetId() + " to " + candidateInv.GetId());
                            InventoriesHandler.Instance.TransferItem(_inventory, candidateInv, item);
                        }
                    }
                    log("ClearMachineGeneratorInventory end");
                }
                yield return wait;
            }
        }

        static Inventory FindInventoryForOre(string oreId)
        {
            var containerNameFilter = "*" + oreId.ToLower();
            if (depositAliases.TryGetValue(oreId, out var alias))
            {
                containerNameFilter = alias;
            }

            foreach (var constructs in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                if (constructs != null && constructs.HasLinkedInventory())
                {
                    string txt = constructs.GetText();
                    if (txt != null && txt.ToLower().Contains(containerNameFilter))
                    {
                        Inventory candidateInventory = InventoriesHandler.Instance.GetInventoryById(constructs.GetLinkedInventoryId());
                        if (candidateInventory != null && InventoryCanAdd(candidateInventory, oreId))
                        {
                            log("    Found Inventory: " + candidateInventory.GetId());
                            break;
                        }
                        else
                        {
                            log("    This inventory is full: " + txt);
                        }
                    }
                }
            }
            return null;
        }
    }
}
