using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The id at which the shadow inventory world objects start.
        /// </summary>
        static readonly int shadowInventoryWorldIdStart = 50;
        static readonly int maxShadowInventoryCount = 25;
        /// <summary>
        /// The id at which the shadow inventory world objects end + 1.
        /// </summary>
        static readonly int shadowInventoryWorldIdEnd = shadowInventoryWorldIdStart + 2 * maxShadowInventoryCount;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetThirstConsumptionRate))]
        static bool GaugesConsumptionHandler_GetThirstConsumptionRate(ref float __result)
        {
            if (updateMode != MultiplayerMode.SinglePlayer && slowdownConsumption.Value)
            {
                __result = -0.0001f;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetOxygenConsumptionRate))]
        static bool GaugesConsumptionHandler_GetOxygenConsumptionRate(ref float __result)
        {
            if (updateMode != MultiplayerMode.SinglePlayer && slowdownConsumption.Value)
            {
                __result = -0.0001f;
                return false;
            }
            return true;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GaugesConsumptionHandler), nameof(GaugesConsumptionHandler.GetHealthConsumptionRate))]
        static bool GaugesConsumptionHandler_GetHealthConsumptionRate(ref float __result)
        {
            if (updateMode != MultiplayerMode.SinglePlayer && slowdownConsumption.Value)
            {
                __result = -0.0001f;
                return false;
            }
            return true;
        }

        static void PrepareShadowInventories(ClientConnection cc)
        {
            string playerName = cc.clientName;
            string playerNamePrefix = "~" + playerName + ";";

            int createNewAt = -1;
            int id;
            for (id = shadowInventoryWorldIdStart; id < shadowInventoryWorldIdEnd; id += 2)
            {
                WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(id);
                if (wo != null && wo.GetText() != null && wo.GetText().StartsWith(playerNamePrefix))
                {
                    LogInfo("Found shadow world objects for " + playerName + " at id " + id);
                    createNewAt = -1;
                    break;
                }
                else if (wo == null || (wo.GetText() != null && wo.GetText().Length == 0))
                {
                    if (createNewAt < 0)
                    {
                        createNewAt = id;
                    }
                }
            }

            if (createNewAt >= 0)
            {
                id = createNewAt;
                LogInfo("Creating new set of shadow world objects for " + playerName + " at " + id);
            }

            if (TryPrepareShadowInventory(id, ref cc.shadowBackpack, out var wo2))
            {
                SetupInitialInventory(cc);
                wo2.SetText(playerNamePrefix);
            }
            TryPrepareShadowInventory(id + 1, ref cc.shadowEquipment, out _);

            cc.shadowBackpackWorldObjectId = id;
            cc.shadowEquipmentWorldObjectId = id + 1;

            LogInfo("ReceiveLogin: Player " + playerName + " has " + cc.shadowBackpack.GetInsideWorldObjects().Count + " items in its backpack");
            LogInfo("  backpack inventory  id " + cc.shadowBackpack.GetId());
            LogInfo("ReceiveLogin: Player " + playerName + " has " + cc.shadowEquipment.GetInsideWorldObjects().Count + " items in its equipment");
            LogInfo("  equipment inventory id " + cc.shadowEquipment.GetId());

            StorageRestoreClient(cc);
            StorageNotifyClient(cc);
        }

        static bool TryPrepareShadowInventory(int id, ref Inventory inventoryOut, out WorldObject wo)
        {
            wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                LogInfo("Creating special inventory " + id);

                wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), id);
                wo.SetText("");
                wo.SetPositionAndRotation(new Vector3(-500, -500, -450), Quaternion.identity);
                WorldObjectsHandler.InstantiateWorldObject(wo, true);
                Inventory inv = InventoriesHandler.CreateNewInventory(1000, 0);
                int invId = inv.GetId();
                wo.SetLinkedInventoryId(invId);
                wo.SetDontSaveMe(false);
                inventoryOut = inv;
                return true;
            }
            else
            {
                int invId = wo.GetLinkedInventoryId();
                var inv = InventoriesHandler.GetInventoryById(invId);
                if (inv == null)
                {
                    LogInfo("Recreating special inventory " + id);
                    inv = InventoriesHandler.CreateNewInventory(1000, 0);
                    wo.SetLinkedInventoryId(inv.GetId());
                    inventoryOut = inv;
                    return true;
                }
                inventoryOut = inv;
            }
            return false;
        }

        static void AddToInventory(Inventory inv, Dictionary<string, int> itemsToAdd, ClientConnection cc)
        {
            foreach (var kv in itemsToAdd)
            {
                var gr = GroupsHandler.GetGroupViaId(kv.Key);
                if (gr != null)
                {
                    for (int i = 0; i < kv.Value; i++)
                    {
                        var wo = WorldObjectsHandler.CreateNewWorldObject(gr);
                        if (cc != null)
                        {
                            SendWorldObjectTo(wo, false, cc);
                        }
                        else
                        {
                            SendWorldObjectToClients(wo, false);
                        }
                        if (!inv.AddItem(wo))
                        {
                            LogWarning("Could not add " + kv.Key + " to " + inv.GetId() + ". Inventory full?!");
                        }
                        /*
                        else
                        {
                            LogInfo("AddToInventory > " + iid + ": " + kv.Key + " [" + i + " / " + kv.Value + "] @ " + inv.GetInsideWorldObjects().Count);
                        }
                        */
                    }
                }
                else
                {
                    LogWarning("SetupInitialInventory: Unknown groupId " + kv.Key);
                }
            }
        }

        static void SetupInitialInventory(ClientConnection cc)
        {
            LogInfo("SetupInitialInventory");

            int stacks = 1;
            if (stackSize != null)
            {
                stacks = Math.Min(10, Math.Max(stacks, stackSize.Value));
            }

            AddToInventory(cc.shadowBackpack, new()
            {
                { "MultiBuild", 1 },
                { "MultiDeconstruct", 1 },
                { "MultiToolLight", 1 },
                { "Iron", stacks },
                { "Magnesium", stacks },
                { "Silicon", stacks },
                { "Titanium", stacks},
                { "Cobalt", stacks },
                { "BlueprintT1", stacks },
                { "OxygenCapsule1", stacks },
                { "WaterBottle1", stacks },
                { "astrofood", stacks }
            }, cc);
        }

        static void SetupHostInventory()
        {
            LogInfo("SetupHostInventory");

            List<Group> groupsToAdd = new();
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                if (gr is GroupItem gi)
                {
                    string item = gi.GetId();
                    /* FIXME not sure what happened to random effigie spawing yet
                    if (item == "GoldenEffigieSpawner")
                    {
                        foreach (var gc in gi.GetAssociatedGroups())
                        {
                            groupsToAdd.Add(GroupsHandler.GetGroupViaId(gc.id));
                        }
                        continue;
                    }
                    */
                    if (item.StartsWith("Rocket") && item != "RocketReactor")
                    {
                        continue;
                    }
                    if (item.EndsWith("Hatched")) {
                        continue;
                    }
                    if (item.StartsWith("Algae") && item.EndsWith("Growable"))
                    {
                        continue;
                    }
                    if (item.StartsWith("Algae") && item.EndsWith("Growable"))
                    {
                        continue;
                    }
                    if (item.StartsWith("Tree") && item.EndsWith("Growable"))
                    {
                        continue;
                    }
                    if (item.StartsWith("Seed") && item.EndsWith("Growable"))
                    {
                        continue;
                    }
                    if (item.StartsWith("DebrisContainer"))
                    {
                        continue;
                    }
                    groupsToAdd.Add(gi);
                }
            }
            groupsToAdd.Sort((a, b) =>
            {
                var ga = a as GroupItem;
                var gb = b as GroupItem;
                var ea = ga != null ? ga.GetEquipableType() : DataConfig.EquipableType.Null;
                var eb = gb != null ? gb.GetEquipableType() : DataConfig.EquipableType.Null;

                if (ea != DataConfig.EquipableType.Null && eb == DataConfig.EquipableType.Null)
                {
                    return 1;
                }
                if (ea == DataConfig.EquipableType.Null && eb != DataConfig.EquipableType.Null)
                {
                    return -1;
                }
                var aid = a.GetId();
                var bid = b.GetId();
                // --------------------------------
                if (aid.StartsWith("Golden") && !bid.StartsWith("Golden"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Golden") && bid.StartsWith("Golden"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("Tree") && !bid.StartsWith("Tree"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Tree") && bid.StartsWith("Tree"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("Seed") && !bid.StartsWith("Seed"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Seed") && bid.StartsWith("Seed"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("Vegetable") && !bid.StartsWith("Vegetable"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Vegetable") && bid.StartsWith("Vegetable"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("astrofood") && !bid.StartsWith("astrofood"))
                {
                    return -1;
                }
                if (!aid.StartsWith("astrofood") && bid.StartsWith("astrofood"))
                {
                    return 1;
                }
                // --------------------------------
                if (aid.StartsWith("WaterBottle") && !bid.StartsWith("WaterBottle"))
                {
                    return -1;
                }
                if (!aid.StartsWith("WaterBottle") && bid.StartsWith("WaterBottle"))
                {
                    return 1;
                }
                // --------------------------------
                if (aid.StartsWith("OxygenCapsule") && !bid.StartsWith("OxygenCapsule"))
                {
                    return -1;
                }
                if (!aid.StartsWith("OxygenCapsule") && bid.StartsWith("OxygenCapsule"))
                {
                    return 1;
                }
                // --------------------------------
                if (aid.StartsWith("Larvae") && !bid.StartsWith("Larvae"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Larvae") && bid.StartsWith("Larvae"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("Butterfly") && !bid.StartsWith("Butterfly"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Butterfly") && bid.StartsWith("Butterfly"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("Bee") && !bid.StartsWith("Bee"))
                {
                    return 1;
                }
                if (!aid.StartsWith("Bee") && bid.StartsWith("Bee"))
                {
                    return -1;
                }
                // --------------------------------
                if (aid.StartsWith("SilkWorm") && !bid.StartsWith("SilkWorm"))
                {
                    return 1;
                }
                if (!aid.StartsWith("SilkWorm") && bid.StartsWith("SilkWorm"))
                {
                    return -1;
                }

                return a.GetId().CompareTo(b.GetId());
            });

            int stacks = 1;
            if (stackSize != null)
            {
                stacks = Math.Min(50, Math.Max(stacks, stackSize.Value));
            }

            int perChest = 30;
            Vector3 pos = GetPlayerMainController().transform.position;
            for (int i = 0; i < groupsToAdd.Count; i += perChest)
            {
                Dictionary<string, int> dict = new();
                for (int j = i; j < groupsToAdd.Count && j < i + perChest; j++)
                {
                    dict.Add(groupsToAdd[j].GetId(), stacks);
                }
                if (dict.Count != 0)
                {
                    var wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"), 0);
                    wo.SetPositionAndRotation(pos, Quaternion.identity);
                    wo.SetDontSaveMe(false);

                    var go = WorldObjectsHandler.InstantiateWorldObject(wo, false);

                    var inv = go.GetComponent<InventoryAssociated>().GetInventory();
                    inv.SetSize(perChest);

                    SendWorldObjectToClients(wo, false);
                    SendAllClients(new MessageInventorySize()
                    {
                        inventoryId = inv.GetId(),
                        size = inv.GetSize()
                    });

                    AddToInventory(inv, dict, null);

                    pos.y += 1f;
                }
            }
        }
    }
}
