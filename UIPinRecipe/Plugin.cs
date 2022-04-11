using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using System.Linq;
using BepInEx.Bootstrap;
using UnityEngine.InputSystem;

namespace UIPinRecipe
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uipinrecipe", "(UI) Pin Recipe to Screen", "1.0.0.4")]
    [BepInDependency(uiCraftEquipmentInPlaceGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string uiCraftEquipmentInPlaceGuid = "akarnokd.theplanetcraftermods.uicraftequipmentinplace";

        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> panelWidth;
        static ConfigEntry<int> holdTime;
        /// <summary>
        /// If the UICraftEquipmentInPlace plugin is also present, count the equipment too
        /// </summary>
        static bool craftInPlaceEnabled;
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 25, "The size of the font used");
            panelWidth = Config.Bind("General", "PanelWidth", 850, "The width of the recipe panel");
            holdTime = Config.Bind("General", "HoldToClearList", 2, "Amount of time to hold button to clear pinned recipe\r\n" +
                "Hold middle mouse button to clear the list");

            craftInPlaceEnabled = Chainloader.PluginInfos.ContainsKey(uiCraftEquipmentInPlaceGuid);

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Start()
        {
            StartCoroutine(UpdatePinnedRecipesCoroutine());
        }

        static float pressingCounter;
        void Update()
        {
            if (Mouse.current.middleButton.isPressed)
            {
                pressingCounter -= Time.smoothDeltaTime;
                
                if (pressingCounter >= 0)
                    return;

                if (pinnedRecipes.Count < 1)
                    return;

                //Clear all pinned
                List<Group> pinnedGroup = pinnedRecipes.Select(i => i.group).ToList();
                pinnedGroup.ForEach(i => PinUnpinGroup(i));
                pinnedRecipes.Clear();
            }
            else
            {
                pressingCounter = holdTime.Value;
            }
        }

        System.Collections.IEnumerator UpdatePinnedRecipesCoroutine()
        {
            for (; ; )
            {
                foreach (PinnedRecipe pr in pinnedRecipes)
                {
                    pr.UpdateState();
                }
                yield return new WaitForSeconds(0.25f);
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
                PlayerMainController player = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                Inventory inventory = player.GetPlayerBackpack().GetInventory();
                Dictionary<string, int> inventoryCounts = new Dictionary<string, int>();
                foreach (WorldObject wo in inventory.GetInsideWorldObjects())
                {
                    string gid = wo.GetGroup().GetId();
                    inventoryCounts.TryGetValue(gid, out int c);
                    inventoryCounts[gid] = c + 1;
                }

                if (craftInPlaceEnabled)
                {
                    foreach (WorldObject wo in player.GetPlayerEquipment().GetInventory().GetInsideWorldObjects())
                    {
                        string gid = wo.GetGroup().GetId();
                        inventoryCounts.TryGetValue(gid, out int c);
                        inventoryCounts[gid] = c + 1;
                    }
                }

                int craftableCount = int.MaxValue;
                foreach (TextSpriteBackground comp in components)
                {
                    inventoryCounts.TryGetValue(comp.group.id, out int c);
                    comp.text.GetComponent<Text>().text = Readable.GetGroupName(comp.group) + " x " + comp.count + " ( " + c + " )";

                    craftableCount = Mathf.Min(craftableCount, c / comp.count);
                }
                if (craftableCount == int.MaxValue)
                {
                    craftableCount = 0;
                }

                inventoryCounts.TryGetValue(group.GetId(), out int productCount);
                product.text.GetComponent<Text>().text =
                    Readable.GetGroupName(group) + " ( " + productCount + " ) < " + craftableCount + " >";
            }
        }

        static TextSpriteBackground CreateText(int x, int y, int indent, GameObject parent, string txt, Sprite sprite = null)
        {
            TextSpriteBackground result = new TextSpriteBackground();

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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        static List<PinnedRecipe> pinnedRecipes = new List<PinnedRecipe>();

        static GameObject parent;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static bool UiWindowCraft_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData)
        {
            if (eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Middle)
            {
                PinUnpinGroup(eventTriggerCallbackData.group);
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

        static void PinUnpinGroup(Group group)
        {
            if (parent == null)
            {
                parent = new GameObject();
                Canvas canvas = parent.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            Recipe recipe = group.GetRecipe();
            if (recipe != null)
            {
                int fs = fontSize.Value;

                PinnedRecipe alreadyPinned = null;
                int dy = Screen.height / 2 - 150;

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

                    dy = Screen.height / 2 - 150;
                    List<PinnedRecipe> copy = new List<PinnedRecipe>();

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
            Dictionary<string, int> recipeCounts = new Dictionary<string, int>();
            Dictionary<string, Group> recipeGroups = new Dictionary<string, Group>();
            List<Group> ingredientsGroupInRecipe = recipe.GetIngredientsGroupInRecipe();
            foreach (Group g in ingredientsGroupInRecipe)
            {
                recipeCounts.TryGetValue(g.GetId(), out int c);
                recipeCounts[g.GetId()] = c + 1;
                recipeGroups[g.GetId()] = g;
            }

            string productStr = Readable.GetGroupName(group);

            int px = Screen.width / 2 - 150;
            PinnedRecipe current = new PinnedRecipe();
            current.group = group;
            current.product = CreateText(px, dy, 0, parent, productStr, group.GetImage());
            current.components = new List<TextSpriteBackground>();

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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            pinnedRecipes.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiveDevTools), nameof(LiveDevTools.ToggleUi))]
        static void LiveDevTools_ToggleUi(List<GameObject> ___handObjectsToHide)
        {
            bool active = !___handObjectsToHide[0].activeSelf;
            parent?.SetActive(active);
        }
    }
}
