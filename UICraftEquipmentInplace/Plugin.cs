using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;

namespace UICraftEquipmentInPlace
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicraftequipmentinplace", "(UI) Craft Equipment Inplace", "1.0.0.3")]
    [BepInDependency("akarnokd.theplanetcraftermods.cheatinventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool CraftManager_TryToCraftInInventory(ref bool __result,
            ActionCrafter _sourceCrafter, PlayerMainController _playerController, GroupItem groupItem,
            ref int ___totalCraft)
        {
            // In Free Craft mode, skip this mod.
            if (Managers.GetManager<PlayModeHandler>().GetFreeCraft())
            {
                return true;
            }
            // Unfortunately, the whole method has to be rewritten
            DataConfig.EquipableType equipType = groupItem.GetEquipableType();
            if (equipType == DataConfig.EquipableType.OxygenTank
                || equipType == DataConfig.EquipableType.BackpackIncrease
                || equipType == DataConfig.EquipableType.EquipmentIncrease
                || equipType == DataConfig.EquipableType.MultiToolMineSpeed
                || equipType == DataConfig.EquipableType.BootsSpeed
                || equipType == DataConfig.EquipableType.Jetpack)
            {
                List<Group> ingredients = new List<Group>(groupItem.GetRecipe().GetIngredientsGroupInRecipe());

                Inventory backpack = _playerController.GetPlayerBackpack().GetInventory();
                Inventory equipment = _playerController.GetPlayerEquipment().GetInventory();
                List<Group> fromBackpack = new List<Group>();
                List<Group> fromEquipment = new List<Group>();

                List<WorldObject> equipments = new List<WorldObject>(equipment.GetInsideWorldObjects());
                List<WorldObject> backpacks = new List<WorldObject>(backpack.GetInsideWorldObjects());

                bool inEquipment = false;
                for (int i = ingredients.Count - 1; i >= 0; i--)
                {
                    Group ingredient = ingredients[i];
                    for (int j = 0; j < equipments.Count; j++)
                    {
                        WorldObject wo = equipments[j];
                        if (wo.GetGroup().GetId() == ingredient.GetId())
                        {
                            ingredients.RemoveAt(i);
                            equipments.RemoveAt(j);
                            fromEquipment.Add(ingredient);
                            inEquipment = true;
                            break;
                        }
                    }
                    if (!inEquipment)
                    {
                        for (int j = 0; j < backpacks.Count; j++)
                        {
                            WorldObject wo = backpacks[j];
                            if (wo.GetGroup().GetId() == ingredient.GetId())
                            {
                                ingredients.RemoveAt(i);
                                backpacks.RemoveAt(j);
                                fromBackpack.Add(ingredient);
                                break;
                            }
                        }
                    }
                }
                // if we are not replacing equipment then check if there is room for the backpack
                // CheatInventoryStacking: this should be the first invocation of IsFull so the
                //                         prefix would have already saved the groupItem.GetId()
                if (!inEquipment && backpack.IsFull())
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f, "");
                    __result = false;
                    return false;
                }

                if (ingredients.Count == 0)
                {
                    _sourceCrafter.CraftAnimation(groupItem);

                    backpack.RemoveItems(fromBackpack, true, true);
                    equipment.RemoveItems(fromEquipment, true, true);

                    WorldObject worldObject = WorldObjectsHandler.CreateNewWorldObject(groupItem, 0);
                    if (fromEquipment.Count != 0)
                    {
                        equipment.AddItem(worldObject);

                        foreach (Group group in fromEquipment)
                        {
                            if (equipType == DataConfig.EquipableType.BackpackIncrease)
                            {
                                backpack.SetSize(backpack.GetSize() - ((GroupItem)group).GetGroupValue());
                            }
                            else if (equipType == DataConfig.EquipableType.EquipmentIncrease)
                            {
                                equipment.SetSize(equipment.GetSize() - ((GroupItem)group).GetGroupValue());
                            }
                        }

                        if (equipType == DataConfig.EquipableType.BackpackIncrease)
                        {
                            backpack.SetSize(backpack.GetSize() + groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.EquipmentIncrease)
                        {
                            equipment.SetSize(equipment.GetSize() + groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.OxygenTank)
                        {
                            _playerController.GetPlayerEquipment()
                                .GetComponent<PlayerGaugesHandler>()
                                .UpdateGaugesDependingOnEquipment(equipment);
                        }
                        else if (equipType == DataConfig.EquipableType.MultiToolMineSpeed)
                        {
                            _playerController.GetPlayerEquipment()
                                .GetComponent<PlayerMultitool>()
                                .GetMultiToolMine()
                                .SetMineTimeReducer(groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.BootsSpeed)
                        {
                            _playerController.GetPlayerEquipment()
                                .GetComponent<PlayerMovable>()
                                .SetMoveSpeedChangePercentage((float)groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.Jetpack)
                        {
                            _playerController.GetPlayerEquipment()
                                .GetComponent<PlayerMovable>()
                                .SetJetpackFactor((float)groupItem.GetGroupValue() / 100f);
                        }

                    }
                    else
                    {
                        backpack.AddItem(worldObject);
                    }
                    ___totalCraft++;

                    __result = true;
                } 
                else
                {
                    // missing ingredients, do nothing
                    __result = false;
                }

                return false;
            }
            // not equipable, run the original method
            return true;
        }
    }
}
