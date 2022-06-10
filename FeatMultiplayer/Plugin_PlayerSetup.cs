using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static int shadowInventoryWorldId = 50;
        static int shadowInventoryId;
        static int shadowEquipmentWorldId = 51;
        static int shadowEquipmentId;

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

        static void PrepareShadowInventories()
        {
            // The other player's shadow inventory
            if (TryPrepareShadowInventory(shadowInventoryWorldId, ref shadowInventoryId))
            {
                SetupInitialInventory();
            }
            TryPrepareShadowInventory(shadowEquipmentWorldId, ref shadowEquipmentId);
        }

        static bool TryPrepareShadowInventory(int id, ref int inventoryId)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                LogInfo("Creating special inventory " + id);

                wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), id);
                wo.SetPositionAndRotation(new Vector3(-500, -500, -450), Quaternion.identity);
                WorldObjectsHandler.InstantiateWorldObject(wo, true);
                Inventory inv = InventoriesHandler.CreateNewInventory(1000, 0);
                int invId = inv.GetId();
                inventoryId = invId;
                wo.SetLinkedInventoryId(invId);
                wo.SetDontSaveMe(false);
                return true;
            }
            else
            {
                inventoryId = wo.GetLinkedInventoryId();
            }
            return false;
        }

        static void AddToInventory(int iid, Dictionary<string, int> itemsToAdd)
        {
            var inv = InventoriesHandler.GetInventoryById(iid);
            foreach (var kv in itemsToAdd)
            {
                var gr = GroupsHandler.GetGroupViaId(kv.Key);
                if (gr != null)
                {
                    for (int i = 0; i < kv.Value; i++)
                    {
                        var wo = WorldObjectsHandler.CreateNewWorldObject(gr);
                        inv.AddItem(wo);
                    }
                }
                else
                {
                    LogWarning("SetupInitialInventory: Unknown groupId " + kv.Key);
                }
            }
        }

        static void SetupInitialInventory()
        {
            LogInfo("SetupInitialInventory");

            int stacks = 1;
            if (stackSize != null)
            {
                stacks = Math.Min(10, Math.Max(stacks, stackSize.Value));
            }

            AddToInventory(shadowInventoryId, new()
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
            });
        }

        static void SetupHostInventory()
        {
            LogInfo("SetupHostInventory");

            List<GroupItem> groupsToAdd = new();
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                if (gr is GroupItem gi)
                {
                    string item = gi.GetId();
                    if (!item.StartsWith("Rocket") || item == "RocketReactor")
                    {
                        groupsToAdd.Add(gi);
                    }
                }
            }
            groupsToAdd.Sort((a, b) =>
            {
                if (a.GetEquipableType() != DataConfig.EquipableType.Null && b.GetEquipableType() == DataConfig.EquipableType.Null)
                {
                    return 1;
                }
                if (a.GetEquipableType() == DataConfig.EquipableType.Null && b.GetEquipableType() != DataConfig.EquipableType.Null)
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

                    SendWorldObject(wo, false);
                    Send(new MessageInventorySize()
                    {
                        inventoryId = inv.GetId(),
                        size = inv.GetSize()
                    });

                    AddToInventory(inv.GetId(), dict);

                    pos.y += 1f;
                }
            }
        }
    }
}
