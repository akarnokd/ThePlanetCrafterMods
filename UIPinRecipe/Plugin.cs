// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using UnityEngine.InputSystem;
using System.Reflection;
using System.Linq;
using BepInEx.Logging;
using Unity.Netcode;
using BepInEx.Bootstrap;
using LibCommon;
using System.Text;

namespace UIPinRecipe
{
    [BepInPlugin(modUiPinRecipeGuid, "(UI) Pin Recipe to Screen", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatCraftFromNearbyContainersGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiPinRecipeGuid = "akarnokd.theplanetcraftermods.uipinrecipe";
        const string modCheatCraftFromNearbyContainersGuid = "akarnokd.theplanetcraftermods.cheatcraftfromnearbycontainers";

        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> panelWidth;
        static ConfigEntry<string> clearKey;
        static ConfigEntry<int> panelTop;
        static ConfigEntry<bool> debugMode;

        static readonly int shadowContainerId = 6000;
        static readonly int shadowContainerIdMulti = 6001;

        static ManualLogSource _logger;

        static Font font;

        static Action<MonoBehaviour, Vector3, Action<List<Inventory>>> apiGetInventoriesInRange;

        static List<Inventory> nearbyInventories = [];

        const string funcGetLoadout = "GetLoadout";
        const string funcSetLoadout = "SetLoadout";
        const string funcReceiveLoadout = "ReceiveLoadout";

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 25, "The size of the font used");
            panelWidth = Config.Bind("General", "PanelWidth", 850, "The width of the recipe panel");
            panelTop = Config.Bind("General", "PanelTop", 150, "Panel position from the top of the screen.");
            clearKey = Config.Bind("General", "ClearKey", "C", "The key to press to clear all pinned recipes");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging, chatty!");

            _logger = Logger;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (Chainloader.PluginInfos.TryGetValue(modCheatCraftFromNearbyContainersGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " found. Considering nearby containers");

                apiGetInventoriesInRange = (Action<MonoBehaviour, Vector3, Action<List<Inventory>>>)AccessTools.Field(pi.Instance.GetType(), "apiGetInventoriesInRange").GetValue(null);
            }
            else
            {
                Logger.LogInfo("Mod " + modCheatCraftFromNearbyContainersGuid + " not found.");
            }

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));

            ModNetworking.Init(modUiPinRecipeGuid, Logger);
            ModNetworking.Patch(h);
            ModNetworking._debugMode = debugMode.Value;
            ModNetworking.RegisterFunction(funcGetLoadout, OnGetLoadout);
            ModNetworking.RegisterFunction(funcSetLoadout, OnSetLoadout);
            ModNetworking.RegisterFunction(funcReceiveLoadout, OnReceiveLoadout);
            ModNetworking.RegisterFunction(ModNetworking.FunctionClientConnected, OnClientConnected);


            LibCommon.ModPlanetLoaded.Patch(h, modUiPinRecipeGuid, _ => PlanetLoader_HandleDataAfterLoad());
        }

        void Start()
        {
            StartCoroutine(UpdatePinnedRecipesCoroutine());
        }

        System.Collections.IEnumerator UpdatePinnedRecipesCoroutine()
        {
            for (; ; )
            {
                if (pinnedRecipes.Count != 0)
                {
                    nearbyInventories = [];
                    if (apiGetInventoriesInRange != null)
                    {
                        var pm = Managers.GetManager<PlayersManager>();
                        if (pm != null)
                        {
                            var ac = pm.GetActivePlayerController();
                            if (ac != null)
                            {
                                var callbackWaiter = new CallbackWaiter();
                                apiGetInventoriesInRange(this, ac.transform.position, list =>
                                {
                                    nearbyInventories = list;
                                    callbackWaiter.Done();
                                });

                                while (!callbackWaiter.IsDone)
                                {
                                    yield return null;
                                }
                            }
                        }
                    }
                    UpdateCounters();
                    foreach (PinnedRecipe pr in pinnedRecipes)
                    {
                        pr.UpdateState();
                    }
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        void Update()
        {
            if (pinnedRecipes.Count != 0) {
                FieldInfo pi = typeof(Key).GetField(clearKey.Value.ToString().ToUpper());
                Key k = Key.C;
                if (pi != null)
                {
                    k = (Key)pi.GetRawConstantValue();
                }
                WindowsHandler wh = Managers.GetManager<WindowsHandler>();
                if (Keyboard.current[k].isPressed && wh != null && !wh.GetHasUiOpen())
                {
                    ClearPinnedRecipes();
                    SavePinnedRecipes();
                }
            }
        }

        static readonly DictionaryCounter inventoryCounts = new(1024);
        static readonly DictionaryCounter equipmentCounts = new(32);

        static void UpdateCounters()
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var player = pm.GetActivePlayerController();
            if (player == null)
            {
                return;
            }
            var backpack = player.GetPlayerBackpack();
            if (backpack == null)
            {
                return;
            }
            Inventory inventory = backpack.GetInventory();
            if (inventory == null)
            {
                return;
            }

            inventoryCounts.Clear();

            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                inventoryCounts.Update(wo.GetGroup().id);
            }

            foreach (var inv in nearbyInventories)
            {
                if (inv != null)
                {
                    foreach (var wo in inv.GetInsideWorldObjects())
                    {
                        inventoryCounts.Update(wo.GetGroup().id);
                    }
                }
            }
        }

        class TextSpriteBackground {
            public GameObject text;
            public GameObject sprite;
            public GameObject background;
            public Group group;
            public int count;
            public void Destroy()
            {
                UnityEngine.Object.Destroy(text);
                UnityEngine.Object.Destroy(sprite);
                UnityEngine.Object.Destroy(background);

                text = null;
                sprite = null;
                background = null;
            }
        }
        class PinnedRecipe
        {
            public TextSpriteBackground product;
            public List<TextSpriteBackground> components;
            public Group group;
            public int bottom;
            public void Destroy()
            {
                product.Destroy();
                foreach (TextSpriteBackground component in components)
                {
                    component.Destroy();
                }
                group = null;
            }

            public void UpdateState()
            {
                var pm = Managers.GetManager<PlayersManager>();
                if (pm == null)
                {
                    return;
                }
                var player = pm.GetActivePlayerController();
                if (player == null)
                {
                    return;
                }
                bool includeEquipment = false;
                equipmentCounts.Clear();
                if (group is GroupItem gi && gi.GetEquipableType() != DataConfig.EquipableType.Null)
                {
                    includeEquipment = true;
                    foreach (WorldObject wo in player.GetPlayerEquipment().GetInventory().GetInsideWorldObjects())
                    {
                        equipmentCounts.Update(wo.GetGroup().id);
                    }
                }


                int craftableCount = int.MaxValue;
                foreach (TextSpriteBackground comp in components)
                {
                    int c = inventoryCounts.CountOf(comp.group.id);
                    if (includeEquipment) {
                        c += equipmentCounts.CountOf(comp.group.id);
                    }

                    comp.text.GetComponent<Text>().text = Readable.GetGroupName(comp.group) + " x " + comp.count + " ( " + c + " )";

                    craftableCount = Mathf.Min(craftableCount, c / comp.count);
                }
                if (craftableCount == int.MaxValue)
                {
                    craftableCount = 0;
                }

                var productCount = inventoryCounts.CountOf(group.id);
                if (includeEquipment)
                {
                    productCount += equipmentCounts.CountOf(group.id);
                }
                product.text.GetComponent<Text>().text =
                    Readable.GetGroupName(group) + " ( " + productCount + " ) < " + craftableCount + " >";
            }
        }

        static TextSpriteBackground CreateText(int x, int y, int indent, GameObject parent, string txt, Sprite sprite = null)
        {
            var result = new TextSpriteBackground();

            float pw = panelWidth.Value;
            float fs = fontSize.Value;

            RectTransform rectTransform;
            result.background = new GameObject();
            result.background.transform.parent = parent.transform;
            
            Image image = result.background.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

            rectTransform = image.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(x, y, 0);
            rectTransform.sizeDelta = new Vector2(pw - 1, fs + 5);

            result.text = new GameObject();
            result.text.transform.parent = parent.transform;

            Text text = result.text.AddComponent<Text>();
            text.font = font;
            text.text = txt ?? "" + x + ", " + y;
            text.color = new Color(1f, 1f, 1f, 1f);
            text.fontSize = (int)fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;

            rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(indent + x + fs + 10, y, 1);
            rectTransform.sizeDelta = new Vector2(pw - 35, fs + 15);

            if (sprite != null)
            {
                result.sprite = new GameObject();
                result.sprite.transform.parent = parent.transform;

                Image spriteRenderer = result.sprite.AddComponent<Image>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f);

                rectTransform = spriteRenderer.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(indent + x - pw / 2 + 2 + (fs + 5) / 2, y, 0);
                rectTransform.sizeDelta = new Vector2(fs + 5, fs + 5);
                result.sprite.transform.localScale = new Vector3(1f, 1f, 1f);
            }
            return result;
        }

        static readonly List<PinnedRecipe> pinnedRecipes = [];

        static GameObject parent;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static bool UiWindowCraft_OnImageClicked(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            if (_eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Middle)
            {
                PinUnpinGroup(_eventTriggerCallbackData.group);
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowConstruction), "OnImageClicked")]
        static bool UiWindowConstruction_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData)
        {
            if (eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Middle)
            {
                PinUnpinGroup(eventTriggerCallbackData.group);
                return false;
            }
            return true;
        }

        public static void PinUnpinGroup(Group group)
        {
            PinUnpinInternal(group);
            SavePinnedRecipes();
        }

        static void PinUnpinInternal(Group group)
        {
            if (parent == null)
            {
                parent = new GameObject("PinRecipeCanvas");
                Canvas canvas = parent.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            Recipe recipe = group.GetRecipe();
            if (recipe != null)
            {
                int fs = fontSize.Value;

                PinnedRecipe alreadyPinned = null;
                int dy = Screen.height / 2 - panelTop.Value;

                foreach (PinnedRecipe pr in pinnedRecipes)
                {
                    if (pr.group.GetId() == group.GetId())
                    {
                        alreadyPinned = pr;
                    }
                    dy = Math.Min(dy, pr.bottom);
                }
                if (pinnedRecipes.Count > 0)
                {
                    dy -= fs + 10;
                }
                if (alreadyPinned == null)
                {
                    pinnedRecipes.Add(CreatePinnedRecipe(group, recipe, dy, fs));
                }
                else
                {
                    alreadyPinned.Destroy();

                    dy = Screen.height / 2 - panelTop.Value;
                    List<PinnedRecipe> copy = [];

                    foreach (PinnedRecipe rec in pinnedRecipes)
                    {
                        if (rec != alreadyPinned)
                        {
                            Group gr = rec.group;
                            rec.Destroy();

                            PinnedRecipe pr = CreatePinnedRecipe(gr, gr.GetRecipe(), dy, fs);
                            copy.Add(pr);
                            dy = pr.bottom;

                            dy -= fs + 10;
                        }
                    }
                    pinnedRecipes.Clear();
                    pinnedRecipes.AddRange(copy);
                }
            }
        }

        static PinnedRecipe CreatePinnedRecipe(Group group, Recipe recipe, int dy, int fs)
        {
            Dictionary<string, int> recipeCounts = [];
            Dictionary<string, Group> recipeGroups = [];
            List<Group> ingredientsGroupInRecipe = recipe.GetIngredientsGroupInRecipe();
            foreach (Group g in ingredientsGroupInRecipe)
            {
                recipeCounts.TryGetValue(g.GetId(), out int c);
                recipeCounts[g.GetId()] = c + 1;
                recipeGroups[g.GetId()] = g;
            }

            string productStr = Readable.GetGroupName(group);

            int px = Screen.width / 2 - 150;
            var current = new PinnedRecipe
            {
                group = group,
                product = CreateText(px, dy, 0, parent, productStr, group.GetImage()),
                components = []
            };

            foreach (Group g in recipeGroups.Values)
            {
                dy -= fs + 5;
                string cn = Readable.GetGroupName(g) + " x " + recipeCounts[g.GetId()];
                TextSpriteBackground comp = CreateText(px, dy, fs + 5, parent, cn, g.GetImage());
                comp.group = g;
                comp.count = recipeCounts[g.GetId()];
                current.components.Add(comp);
            }
            current.bottom = dy;
            current.UpdateState();

            return current;
        }

        static void ClearPinnedRecipes()
        {
            foreach (PinnedRecipe pr in pinnedRecipes)
            {
                pr.Destroy();
            }
            pinnedRecipes.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            ClearPinnedRecipes();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }
        static void PlanetLoader_HandleDataAfterLoad()
        {
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                PlayerMainController player = playersManager.GetActivePlayerController();
                if (player != null)
                {
                    RestorePinnedRecipes();
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            if (parent != null)
            {
                parent.SetActive(active);
            }
        }

        static WorldObject EnsureHiddenContainer()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(shadowContainerId);
            if (wo == null)
            {
                wo = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), shadowContainerId);
                wo.SetText("");
            }
            wo.SetDontSaveMe(false);
            return wo;
        }

        static void SavePinnedRecipes()
        {
            var str = string.Join(",", pinnedRecipes.Select(slot =>
            {
                var g = slot.group;
                if (g == null)
                {
                    return "";
                }
                return g.id;
            }));

            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var wo = EnsureHiddenContainer();
                wo.SetText(str);
            }
            else
            {
                SavePinnedRecipesClient(str);
            }
        }

        static void RestorePinnedRecipes()
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var wo = EnsureHiddenContainer();
                ClearPinnedRecipes();

                RestorePinnedRecipesFromString(wo.GetText());

                Log("Restoring pinned recipes: " + wo.GetText());
            }
            else
            {
                Log("RestorePinnedRecipes Client");
                CallGetLoadout();
            }
        }

        static void RestorePinnedRecipesFromString(string s)
        {
            if (s == null || s.Length == 0)
            {
                ClearPinnedRecipes();
                return;
            }
            var current = s.Split(',');
            foreach (var gid in current)
            {
                var g = GroupsHandler.GetGroupViaId(gid);
                if (g != null)
                {
                    PinUnpinInternal(g);
                }
            }
        }

        static void OnModConfigChanged(ConfigEntryBase _)
        {
            ModNetworking._debugMode = debugMode.Value;

            var copy = new List<Group>();
            foreach (var pin in pinnedRecipes)
            {
                copy.Add(pin.group);
            }
            ClearPinnedRecipes();
            foreach (var gr in copy)
            {
                if (gr != null)
                {
                    PinUnpinInternal(gr);
                }
            }
        }

        static WorldObject EnsureHiddenContainerMultiplayer()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(shadowContainerIdMulti);
            if (wo == null)
            {
                wo = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), shadowContainerIdMulti);
                wo.SetText("");
            }
            wo.SetDontSaveMe(false);
            return wo;
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                _logger.LogInfo(message);
            }
        }

        static Dictionary<string, string> ParseLoadoutMulti(string str)
        {
            Dictionary<string, string> result = [];
            foreach (var entry in str.Split(';'))
            {
                var idStr = entry.Split("=");
                if (idStr.Length == 2)
                {
                    result[idStr[0]] = idStr[1];
                }
            }
            return result;
        }

        static string SerializeLoadoutMulti(Dictionary<string, string> result)
        {
            var sb = new StringBuilder(256);
            foreach (var kv in result)
            {
                if (sb.Length != 0)
                {
                    sb.Append(';');
                }
                sb.Append(kv.Key.Replace('|', ' ').Replace('@', ' ')).Append("=").Append(kv.Value);
            }
            return sb.ToString();
        }

        static void OnGetLoadout(ulong sender, string parameters)
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm != null)
            {
                var wo = EnsureHiddenContainerMultiplayer();
                var dict = ParseLoadoutMulti(wo.GetText() ?? "");

                var idName = parameters.Split(',');
                if (idName.Length == 2)
                {
                    Log("OnGetLoadout via parameters: " + parameters);
                    var nm = idName[1].Replace('|', ' ').Replace('@', ' ');
                    if (dict.TryGetValue(nm, out var str))
                    {
                        Log("OnGetLoadout via parameters: " + sender + " ~ " + parameters + " -> " + str);
                        ModNetworking.SendClient(sender, funcReceiveLoadout, str);
                        return;
                    }
                }
                Log("OnGetLoadout - Client info not found: " + sender + " ~ " + parameters);
            }
            else
            {
                Log("OnGetLoadout: No PlayersManager");
            }
        }

        static void OnSetLoadout(ulong sender, string parameters)
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm != null)
            {
                var wo = EnsureHiddenContainerMultiplayer();
                var dict = ParseLoadoutMulti(wo.GetText() ?? "");
                var idName = parameters.Split('=');
                if (idName.Length == 2)
                {
                    Log("OnSetLoadout via parameters: " + sender + " ~ " + parameters);
                    var nm = idName[0].Replace('|', ' ').Replace('@', ' ');
                    dict[nm] = idName[1];

                    wo.SetText(SerializeLoadoutMulti(dict));
                    return;
                }
                Log("OnSetLoadout - Client info not found: " + sender);
            }
            else
            {
                Log("OnSetLoadout: No PlayersManager");
            }
        }

        static void OnReceiveLoadout(ulong sender, string parameters)
        {
            Log("OnReceiveLoadout: " + parameters);
            ClearPinnedRecipes();
            RestorePinnedRecipesFromString(parameters);
        }

        static void OnClientConnected(ulong sender, string parameters)
        {
            if (!(NetworkManager.Singleton?.IsServer ?? true))
            {
                Log("OnClientConnected Client -> Call GetLoadout");
                CallGetLoadout();
            }
        }

        static void CallGetLoadout()
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm != null)
            {
                var pc = pm.GetActivePlayerController();
                if (pc != null)
                {
                    var msg = pc.id + "," + pc.playerName;
                    Log("CallGetLoadout: " + msg);
                    ModNetworking.SendHost(funcGetLoadout, msg);
                }
            }
        }

        static void SavePinnedRecipesClient(string str)
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm != null)
            {
                var ac = pm.GetActivePlayerController().playerName;
                str = ac + "=" + str;
                Log("SavePinnedRecipesClient: Call SetLoadout " + str);
                ModNetworking.SendHost(funcSetLoadout, str);
            }
            else
            {
                Log("SavePinnedRecipesClient: PlayersManager is null");
            }
        }
    }
}
