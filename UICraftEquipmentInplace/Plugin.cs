using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System.Reflection;
using BepInEx.Logging;
using System.Linq;

namespace UICraftEquipmentInplace
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uicraftequipmentinplace", "(UI) Craft Equipment Inplace", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.cheatinventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("AdvancedMode", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(mobileCrafterGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        const string mobileCrafterGuid = "Mobile_crafter";

        static ConfigEntry<bool> crafts;

        static ManualLogSource logger;

        /// <summary>
        /// The ActionCrafter object the Mobile Crafter mod uses
        /// </summary>
        static ActionCrafter mobileCrafterTestss;

        static FieldInfo playerEquipmentHasCleanConstructionChip;
        static FieldInfo playerEquipmentHasDeconstructT2;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue("AdvancedMode", out BepInEx.PluginInfo pi))
            {
                Logger.LogInfo("Disabling AdvancedMode's Free Crafting mode.");
                FieldInfo fi = AccessTools.Field(pi.Instance.GetType(), "Crafts");
                crafts = (ConfigEntry<bool>)fi.GetValue(null);

                ConfigEntry<bool> ovr = Config.Bind("General", "OverrideAdvancedModeCrafts", false, "Override the configuration object inside the AdvancedMode plugin");
                ovr.Value = false; // this will pass the execution along to our patch
                fi.SetValue(null, ovr);
                Logger.LogInfo("Disabled AdvancedMode's Free Crafting mode (" + crafts.Value + ")");
            } 
            else
            {
                Logger.LogInfo("AdvancedMode Plugin not found.");
            }

            if (Chainloader.PluginInfos.TryGetValue(mobileCrafterGuid, out pi))
            {
                Logger.LogInfo(mobileCrafterGuid + " found, unpatching CraftManager::TryToCraftInInventory");

                mobileCrafterTestss = (ActionCrafter)AccessTools.Field(pi.Instance.GetType(), "testss").GetValue(null);
                Harmony theirHarmony = (Harmony)AccessTools.Field(pi.Instance.GetType(), "harmony").GetValue(pi.Instance);

                theirHarmony.Unpatch(typeof(CraftManager).GetMethod(nameof(CraftManager.TryToCraftInInventory)), HarmonyPatchType.Prefix);

                Logger.LogInfo(mobileCrafterGuid + " found, unpatching CraftManager::TryToCraftInInventory - DONE");
            }
            else
            {
                Logger.LogInfo(mobileCrafterGuid + " not found.");
            }

            playerEquipmentHasCleanConstructionChip = AccessTools.Field(typeof(PlayerEquipment), "hasCleanConstructionChip");
            playerEquipmentHasDeconstructT2 = AccessTools.Field(typeof(PlayerEquipment), "hasDeconstructT2");

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.SaveModInfo.Patch(harmony);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItems))]
        static bool Inventory_RemoveItems(Inventory __instance)
        {
            //logger.LogInfo("Inventory_RemoveItems called");
            // no AdvancedMode || FreeCrafts disabled || inventory is the equipment
            return crafts == null || !crafts.Value || __instance.GetId() == 2;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionCrafter), nameof(ActionCrafter.CraftAnimation))]
        static bool ActionCrafter_CraftAnimation(ActionCrafter __instance)
        {
            return __instance != mobileCrafterTestss;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static bool CraftManager_TryToCraftInInventory(ref bool __result,
            ActionCrafter _sourceCrafter, PlayerMainController _playerController, GroupItem groupItem,
            ref int ___totalCraft)
        {
            // In Free Craft mode, skip this mod.
            bool isFreeCraft = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetFreeCraft();

            // Unfortunately, the whole method has to be rewritten
            DataConfig.EquipableType equipType = groupItem.GetEquipableType();
            if (equipType == DataConfig.EquipableType.OxygenTank
                || equipType == DataConfig.EquipableType.BackpackIncrease
                || equipType == DataConfig.EquipableType.EquipmentIncrease
                || equipType == DataConfig.EquipableType.MultiToolMineSpeed
                || equipType == DataConfig.EquipableType.BootsSpeed
                || equipType == DataConfig.EquipableType.Jetpack
                || equipType == DataConfig.EquipableType.MultiToolLight
                || equipType == DataConfig.EquipableType.AirFilter
                || equipType == DataConfig.EquipableType.MultiToolCleanConstruction
                || equipType == DataConfig.EquipableType.MultiToolDeconstruct)
            {
                logger.LogInfo("Crafting inplace: " + equipType);
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
                    bool checkBackpack = true;
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
                            checkBackpack = false;
                            logger.LogInfo("Found ingredient in equipment: " + ingredient.GetId());
                            break;
                        }
                    }
                    if (checkBackpack)
                    {
                        for (int j = 0; j < backpacks.Count; j++)
                        {
                            WorldObject wo = backpacks[j];
                            if (wo.GetGroup().GetId() == ingredient.GetId())
                            {
                                ingredients.RemoveAt(i);
                                backpacks.RemoveAt(j);
                                fromBackpack.Add(ingredient);
                                logger.LogInfo("Found ingredient in backpack: " + ingredient.GetId());
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

                if (ingredients.Count == 0 || isFreeCraft)
                {
                    if (_sourceCrafter != mobileCrafterTestss)
                    {
                        _sourceCrafter.CraftAnimation(groupItem);
                    }

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
                                .SetMoveSpeedChangePercentage(groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.Jetpack)
                        {
                            _playerController.GetPlayerEquipment()
                                .GetComponent<PlayerMovable>()
                                .SetJetpackFactor(groupItem.GetGroupValue() / 100f);
                        }
                        else if (equipType == DataConfig.EquipableType.MultiToolLight)
                        {
                            _playerController.GetMultitool().SetCanUseLight(true, groupItem.GetGroupValue());
                        }
                        else if (equipType == DataConfig.EquipableType.AirFilter)
                        {
                            _playerController.GetGaugesHandler().SetHasRebreather(equipment);
                        }
                        else if (equipType == DataConfig.EquipableType.MultiToolCleanConstruction)
                        {
                            playerEquipmentHasCleanConstructionChip.SetValue(
                                _playerController.GetPlayerEquipment(), true);
                        }
                        else if (equipType == DataConfig.EquipableType.MultiToolDeconstruct)
                        {
                            playerEquipmentHasDeconstructT2.SetValue(_playerController.GetPlayerEquipment(), true);
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
                    logger.LogInfo("Missing ingredients: " + string.Join(", ", ingredients.Select(g => g.GetId())));
                }

                return false;
            }
            // not equipable, run the original method
            return true;
        }
    }
}
