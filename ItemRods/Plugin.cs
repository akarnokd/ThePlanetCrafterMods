// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using BepInEx.Logging;
using System.Linq;

// Remake of cisox Rods mod: https://www.nexusmods.com/planetcrafter/mods/75

namespace ItemRods
{
    [BepInPlugin("akarnokd.theplanetcraftermods.itemrods", "(Item) Rods", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static readonly List<string> ores = ["Iron", "Sulfur", "Titanium", "Silicon", "Cobalt", "Magnesium", "Aluminium", "Zeolite"];

        static readonly Dictionary<string, Color> oreColors = new()
        {
            {
                "Iron",
                new Color(0.4078f, 0.4313f, 0.3882f, 1f)
            },
            {
                "Sulfur",
                new Color(0.9921f, 0.9647f, 0.2588f, 1f)
            },
            {
                "Titanium",
                new Color(0.2941f, 0.2549f, 0.1725f, 1f)
            },
            {
                "Silicon",
                new Color(0.2666f, 0.2666f, 0.2666f, 1f)
            },
            {
                "Cobalt",
                new Color(0.0078f, 0.0078f, 0.3607f, 1f)
            },
            {
                "Magnesium",
                new Color(0.3921f, 0.3098f, 0.2745f, 1f)
            },
            {
                "Aluminium",
                new Color(0.5f, 0.5f, 0.5f, 1f)
            },
            {
                "Zeolite",
                new Color(0.9568f, 0.9843f, 1f, 1f)
            }
        };

        static readonly Dictionary<string, ConfigEntry<bool>> oreConfigs = [];

        static Texture2D rodTexture;

        static Texture2D emissionTexture;

        static string dir;

        static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            Logger.LogInfo($"Plugin is enabled.");

            logger = Logger;
            
            foreach (string text in ores)
            {
                oreConfigs[text] = Config.Bind("General", text, true, "Enable rod for " + text);
            }
            Assembly me = Assembly.GetExecutingAssembly();
            dir = Path.GetDirectoryName(me.Location);

            string text2 = Path.Combine(dir, "Rod.png");
            byte[] array = File.ReadAllBytes(text2);
            
            rodTexture = new Texture2D(1, 1);
            rodTexture.LoadImage(array);
            rodTexture.name = "RodTexture";
            
            string text3 = Path.Combine(dir, "Emission.png");
            byte[] array2 = File.ReadAllBytes(text3);
            
            emissionTexture = new Texture2D(1, 1);
            emissionTexture.LoadImage(array2);
            emissionTexture.name = "EmissionTexture";

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));

            Logger.LogInfo("Plugin is loaded!");
        }

        static GroupDataItem GetOreRodGroupDataItem(List<GroupData> ___groupsData, string ore, int quantity, Color oreColor)
        {
            string text = ore.ToLowerInvariant();
            GroupDataItem groupDataItem = (GroupDataItem)___groupsData.Find((GroupData data) => data.id == ore);
            bool flag = groupDataItem == null;
            GroupDataItem groupDataItem2;
            if (flag)
            {
                groupDataItem2 = null;
            }
            else
            {
                GroupDataItem groupDataItem3 = (GroupDataItem)Instantiate(___groupsData.Find((GroupData data) => data.id == "Rod-uranium"));
                groupDataItem3.name = "Rod-" + text;
                groupDataItem3.id = "Rod-" + text;
                groupDataItem3.terraformStageUnlock = null;
                groupDataItem3.unlockingWorldUnit = DataConfig.WorldUnitType.Terraformation;
                groupDataItem3.unlockingValue = 0f;
                groupDataItem3.tradeValue = 0;
                groupDataItem3.recipeIngredients = [];
                for (int i = 0; i < quantity; i++)
                {
                    groupDataItem3.recipeIngredients.Add(Instantiate(groupDataItem));
                }
                groupDataItem3.associatedGameObject = Instantiate(groupDataItem3.associatedGameObject);
                groupDataItem3.associatedGameObject.name = ore + "Rod";
                MeshRenderer componentInChildren = groupDataItem3.associatedGameObject.GetComponentInChildren<MeshRenderer>();
                bool flag2 = componentInChildren != null;
                if (flag2)
                {
                    componentInChildren.material = new Material(componentInChildren.material);
                    componentInChildren.material.name = "Rod" + ore;
                    componentInChildren.material.SetTexture("_MainTex", rodTexture);
                    componentInChildren.material.SetTexture("_BaseMap", rodTexture);
                    componentInChildren.material.SetTexture("_EmissionMap", emissionTexture);
                    componentInChildren.material.SetColor("_EmissionColor", oreColor);
                    componentInChildren.material.EnableKeyword("_EMISSION");
                }
                Light componentInChildren2 = groupDataItem3.associatedGameObject.GetComponentInChildren<Light>();
                bool flag3 = componentInChildren2 != null;
                if (flag3)
                {
                    componentInChildren2.color = oreColor;
                }
                string text2 = Path.Combine(dir, ore + ".png");
                bool flag4 = File.Exists(text2);
                if (flag4)
                {
                    byte[] array = File.ReadAllBytes(text2);
                    Texture2D texture2D = new Texture2D(1, 1);
                    texture2D.LoadImage(array);
                    groupDataItem3.icon = Sprite.Create(texture2D, new Rect(0f, 0f, (float)texture2D.width, (float)texture2D.height), new Vector2(0f, 0f));
                }
                groupDataItem2 = groupDataItem3;
            }
            return groupDataItem2;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnlockedGroupsHandler), "SetUnlockedGroups")]
        private static void UnlockedGroupsHandler_SetUnlockedGroups(NetworkList<int> ____unlockedGroups)
        {
            foreach (string text in ores)
            {
                bool flag = !oreConfigs.ContainsKey(text) || !oreConfigs[text].Value;
                if (!flag)
                {
                    string text2 = "Rod-" + text.ToLowerInvariant();
                    Group groupViaId = GroupsHandler.GetGroupViaId(text2);
                    logger.LogInfo("Unlocking " + text2);
                    ____unlockedGroups.Add(groupViaId.stableHashCode);
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
        private static void StaticDataHandler_LoadStaticData(List<GroupData> ___groupsData)
        {
            for (var i = ___groupsData.Count - 1; i >= 0; i--)
            {
                var groupData = ___groupsData[i];
                if (groupData == null || (groupData.associatedGameObject == null && groupData.id.StartsWith("Rod-")))
                {
                    ___groupsData.RemoveAt(i);
                }
            }
            
            var existingGroups = ___groupsData.Select(gd => gd.id).ToHashSet();

            foreach (string text in ores)
            {
                bool flag = !oreConfigs.ContainsKey(text) || !oreConfigs[text].Value;
                if (!flag)
                {
                    var oreRodGroupDataItem = GetOreRodGroupDataItem(___groupsData, text, 9, oreColors[text]);
                    if (!existingGroups.Contains(oreRodGroupDataItem.id))
                    {
                        ___groupsData.Add(oreRodGroupDataItem);
                        logger.LogInfo("Added " + text);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        private static void Localization_LoadLocalization(Dictionary<string, Dictionary<string, string>> ___localizationDictionary)
        {
            if (___localizationDictionary.TryGetValue("english", out var dictionary))
            {
                foreach (string text in ores)
                {
                    bool flag2 = !oreConfigs.ContainsKey(text) || !oreConfigs[text].Value;
                    if (!flag2)
                    {
                        string text2 = text.ToLowerInvariant();
                        dictionary["GROUP_NAME_Rod-" + text2] = text + " Rod";
                        dictionary["GROUP_DESC_Rod-" + text2] = "Extremely condensed " + text;
                    }
                }
            }
            if (___localizationDictionary.TryGetValue("hungarian", out dictionary))
            {
                foreach (string text in ores)
                {
                    bool flag2 = !oreConfigs.ContainsKey(text) || !oreConfigs[text].Value;
                    if (!flag2)
                    {
                        string element = dictionary["GROUP_NAME_" + text];
                        string text2 = text.ToLowerInvariant();
                        dictionary["GROUP_NAME_Rod-" + text2] = element + " Rúd";
                        dictionary["GROUP_DESC_Rod-" + text2] = "Extrém tömör " + element;
                    }
                }
            }
        }
    }
}
