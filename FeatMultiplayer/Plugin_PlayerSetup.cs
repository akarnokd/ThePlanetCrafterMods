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
            if (updateMode != MultiplayerMode.SinglePlayer)
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
            if (updateMode != MultiplayerMode.SinglePlayer)
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
            if (updateMode != MultiplayerMode.SinglePlayer)
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
            AddToInventory(shadowInventoryId, new()
            {
                { "MultiBuild", 1 },
                { "MultiDeconstruct", 1 },
                { "MultiToolLight", 1 },
                { "Iron", 10 },
                { "Magnesium", 10 },
                { "Silicon", 10 },
                { "Titanium", 10},
                { "Cobalt", 10 },
                { "BlueprintT1", 10 },
                { "OxygenCapsule1", 10 },
                { "WaterBottle1", 10 },
                { "astrofood", 10 }
            });
        }

        static void SetupHostInventory()
        {
            LogInfo("SetupHostInventory");
            AddToInventory(1, new()
            {
                /*
                { "Aluminium", 50 },
                { "Alloy", 50 },
                { "Uranim", 50 },
                { "Iridium", 50 },
                { "Osmium", 50 },
                { "Zeolite", 50 },
                { "PulsarQuartz", 50 },
                { "RocketReactor", 50 },
                { "WaterBottle1", 50 },
                { "Magnesium", 50 },
                { "Vegetable0Growable", 50 },
                */
                { "Alloy", 50 },
                { "Bioplastic1", 50 },
                { "Bacteria1", 50 },
                { "Fertilizer1", 50 },
                { "Tree0Seed", 50 },
                { "TreeRoot", 50 },
                { "EquipmentIncrease1", 2 },
                { "Backpack1", 2 },
            });
        }
    }
}
