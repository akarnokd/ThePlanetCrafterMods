using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Reflection.Emit;
using System.Text;
using System.Linq;

namespace UIShowRocketCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowrocketcount", "(UI) Show Rocket Counts", "1.0.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        static FieldInfo associatedGroup;
        static ConfigEntry<int> fontSize;
        static ManualLogSource logger;

        static DataConfig.WorldUnitType[] rocketTypes = new DataConfig.WorldUnitType[]
        {
            DataConfig.WorldUnitType.Oxygen,
            DataConfig.WorldUnitType.Heat,
            DataConfig.WorldUnitType.Pressure,
            DataConfig.WorldUnitType.Biomass,
        };
        static Dictionary<DataConfig.WorldUnitType, int> rocketsCount = new Dictionary<DataConfig.WorldUnitType, int>();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            associatedGroup = AccessTools.Field(typeof(EventHoverShowGroup), "associatedGroup");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the counter text on the craft screen");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        public static void GetAllRockets()
        {
            List<WorldObject> constructedWorldObjects = WorldObjectsHandler.GetConstructedWorldObjects();
            foreach (DataConfig.WorldUnitType unitType in rocketTypes)
            {
                if (GameConfig.spaceGlobalMultipliersGroupIds.ContainsKey(unitType))
                {
                    Group unitGroup = GroupsHandler.GetGroupViaId(GameConfig.spaceGlobalMultipliersGroupIds[unitType]);
                    if (unitGroup != null)
                    {
                        WorldObject unitStorage = null;
                        foreach (WorldObject worldObject in constructedWorldObjects)
                        {
                            if (worldObject.GetGroup() == unitGroup)
                            {
                                unitStorage = worldObject;
                                break;
                            }
                        }

                        if (unitStorage == null)
                        {
                            // This is the same code the game uses, so theoretically it should not break anything.
                            unitStorage = WorldObjectsHandler.CreateNewWorldObject(unitGroup, 0);
                            unitStorage.SetPositionAndRotation(GameConfig.spaceLocation, Quaternion.identity);

                            WorldObjectsHandler.InstantiateWorldObject(unitStorage, false);
                        }

                        Inventory rocketInventory = InventoriesHandler.GetInventoryById(unitStorage.GetLinkedInventoryId());
                        if (rocketInventory != null)
                        {
                            int rocketCount = rocketInventory.GetInsideWorldObjects().Where(worldObject => worldObject.GetGroup().GetId().StartsWith($"Rocket{unitType:G}")).Count();
                            rocketsCount[unitType] = rocketCount;

                            logger.LogInfo($"We found {rocketCount} of {unitType:G} rockets.");
                            logger.LogDebug($"Current rocket count is: {rocketsCount[unitType]}");

                            rocketInventory.inventoryContentModified += RocketInventoryModified;
                        }
                    }
                }
            }
        }

        private static void RocketInventoryModified(WorldObject _worldObjectModified, bool _added)
        {
            foreach (DataConfig.WorldUnitType unitType in rocketTypes)
            {
                if (_worldObjectModified.GetGroup().GetId().StartsWith($"Rocket{unitType:G}"))
                {
                    int countChange = (_added ? 1 : -1);
                    rocketsCount[unitType] += countChange;

                    logger.LogInfo($"Rocket count for {unitType:G} has changed by {countChange}.");
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(WorldUnitsDisplayer), "RefreshDisplay")]
        static IEnumerable<CodeInstruction> JumpOut_RefreshDisplay(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo targetMethod = typeof(Plugin).GetMethod(nameof(RefreshDisplay_Modded), BindingFlags.Static | BindingFlags.Public);

            FieldInfo worldUnitsHandler = AccessTools.Field(typeof(WorldUnitsDisplayer), "worldUnitsHandler");
            FieldInfo correspondingUnit = AccessTools.Field(typeof(WorldUnitsDisplayer), "correspondingUnit");
            FieldInfo textFields = AccessTools.Field(typeof(WorldUnitsDisplayer), "textFields");

            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, worldUnitsHandler);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, correspondingUnit);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, textFields);
            yield return new CodeInstruction(OpCodes.Call, targetMethod);
            yield return new CodeInstruction(OpCodes.Ret);
        }

        public static IEnumerator RefreshDisplay_Modded(float timeRepeat, WorldUnitsHandler worldUnitsHandler, List<DataConfig.WorldUnitType> correspondingUnit, List<TextMeshProUGUI> textFields)
        {
            for (; ; )
            {
                if (worldUnitsHandler != null)
                {
                    for (int index = 0; index < textFields.Count; ++index)
                    {
                        WorldUnit unit = worldUnitsHandler.GetUnit(correspondingUnit[index]);
                        if (unit != null)
                        {
                            StringBuilder displayText = new StringBuilder();

                            if (rocketsCount.TryGetValue(unit.GetUnitType(), out int rocketCount))
                                displayText.AppendFormat("{0} x -----    ", rocketCount);

                            displayText.Append(unit.GetValueString());

                            textFields[index].text = displayText.ToString();
                        }
                    }
                }

                yield return new WaitForSeconds(timeRepeat);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "CreateGrid")]
        static void UiWindowCraft_CreateGrid(GridLayoutGroup ___grid)
        {
            int fs = fontSize.Value;

            foreach (Transform tr in ___grid.transform)
            {
                EventHoverShowGroup ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                if (ehg != null)
                {
                    Group g = associatedGroup.GetValue(ehg) as Group;
                    if (g != null)
                    {
                        foreach (DataConfig.WorldUnitType unitType in rocketTypes)
                        {
                            if (g.GetId().StartsWith($"Rocket{unitType:G}"))
                            {
                                int rocketCount = rocketsCount[unitType];
                                if (rocketCount > 0)
                                {
                                    GameObject go = new GameObject();
                                    Transform parent = tr.gameObject.transform;
                                    go.transform.parent = parent;

                                    Text text = go.AddComponent<Text>();
                                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                                    text.text = rocketCount + " x";
                                    text.color = new Color(1f, 1f, 1f, 1f);
                                    text.fontSize = fs;
                                    text.resizeTextForBestFit = false;
                                    text.verticalOverflow = VerticalWrapMode.Truncate;
                                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                                    text.alignment = TextAnchor.MiddleCenter;

                                    Vector2 v = tr.gameObject.GetComponent<Image>().GetComponent<RectTransform>().sizeDelta;

                                    RectTransform rectTransform = text.GetComponent<RectTransform>();
                                    rectTransform.localPosition = new Vector3(0, v.y / 2 + 10, 0);
                                    rectTransform.sizeDelta = new Vector2(fs * 3, fs + 5);
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
