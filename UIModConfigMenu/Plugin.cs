using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Collections;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace UIModConfigMenu
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uimodconfigmenu", "(UI) Mod Config Menu", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static string filterValue = "";

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            __instance.StartCoroutine(DeferredStart());
        }

        static IEnumerator DeferredStart()
        {
            yield return new WaitForSeconds(0.1f);

            enterMainMenu = true;
            try
            {
                CreateModPanel();
            }
            finally
            {
                enterMainMenu = false;
            }
        }

        static List<BepInEx.PluginInfo> pluginList;
        static GameObject modScrollContent;
        static Transform otherOptions;
        static float otherOptionsX;
        static GameObject buttonMods;
        static GameObject modScroll;
        static GameObject filterGo;
        static GameObject filterCount;
        static bool enterMainMenu;
        static Coroutine coroutineRenderPluginListDelayed;

        static void CreateModPanel()
        {
            GameObject buttonControls = default;
            GameObject buttonGraphics = default;
            GameObject scrollViewControls = default;
            OptionsPanel optionsPanel = default;

            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == "ButtonControls")
                {
                    buttonControls = go;
                }
                if (go.name == "ButtonGraphics")
                {
                    buttonGraphics = go;
                }
                if (go.name == "Scroll View Controls")
                {
                    scrollViewControls = go;
                }
                if (go.name == "Game Options Panel")
                {
                    optionsPanel = go.GetComponent<OptionsPanel>();
                }
            }

            if (buttonControls != null && buttonGraphics != null && scrollViewControls != null
                && optionsPanel != null)
            {
                buttonMods = Instantiate(buttonGraphics);
                buttonMods.name = "ButtonMods";
                buttonMods.transform.SetParent(buttonGraphics.transform.parent, false);

                buttonMods.transform.localPosition = buttonGraphics.transform.localPosition
                    + new Vector3(buttonGraphics.transform.localPosition.x - buttonControls.transform.localPosition.x, 0, 0);

                var lt = buttonMods.GetComponentInChildren<LocalizedText>();
                lt.textId = "ModConfigMenu_Button";
                lt.UpdateTranslation();

                var btn = buttonMods.GetComponent<Button>();
                var bc = new Button.ButtonClickedEvent();
                btn.onClick = bc;

                otherOptions = scrollViewControls.transform.Find("Viewport/Content/Other Options");
                otherOptionsX = otherOptions.GetComponent<RectTransform>().localPosition.x;

                modScroll = Instantiate(scrollViewControls);
                modScroll.name = "Scroll View Mods";
                modScroll.transform.SetParent(scrollViewControls.transform.parent, false);
                modScroll.SetActive(false);

                optionsPanel.tabsContent.Add(modScroll);
                var tabIndex = optionsPanel.tabsContent.Count - 1;

                modScrollContent = modScroll.transform.Find("Viewport/Content").gameObject;

                pluginList = new(Chainloader.PluginInfos.Values);
                pluginList.Sort((a, b) =>
                {
                    return a.Metadata.Name.CompareTo(b.Metadata.Name);
                });

                CreateFilterInput();

                bc.AddListener(() => {
                    optionsPanel.OnClickTab(tabIndex);
                    filterGo.SetActive(true);
                });

                RenderPluginList();
            }
            else
            {
                logger.LogInfo("ButtonControls" + (buttonControls != null));
                logger.LogInfo("ButtonGraphics" + (buttonGraphics != null));
                logger.LogInfo("Scroll View Control" + (scrollViewControls != null));
                logger.LogInfo("OptionsPanel" + (optionsPanel != null));
            }
        }

        void Update()
        {
            if (modScroll != null && filterGo != null && !modScroll.activeSelf && filterGo.activeSelf)
            {
                filterGo.SetActive(false);
            }
        }


        static void CreateFilterInput() {

            filterGo = new GameObject("ModConfigMenu_Filter");
            filterGo.transform.SetParent(modScroll.transform.parent, false);

            var filterImg = filterGo.AddComponent<Image>();
            filterImg.color = new Color(0.4f, 0.4f, 0.4f);

            var filterRt = filterGo.GetComponent<RectTransform>();
            filterRt.localPosition = new Vector3(-900, -810, 0);

            var filterInput = filterGo.AddComponent<InputField>();

            var filterTextGo = new GameObject("ModConfigMenu_Filter_Text");
            filterTextGo.transform.SetParent(filterGo.transform, false);

            var buttonTxtRef = buttonMods.GetComponentInChildren<Text>();

            var filterTextPlaceholderGo = new GameObject("ModConfigMenu_Filter_Text_PlaceHolder");
            filterTextPlaceholderGo.transform.SetParent(filterGo.transform, false);

            var filterPlaceholderText = filterTextPlaceholderGo.AddComponent<Text>();
            filterPlaceholderText.font = buttonTxtRef.font;
            filterPlaceholderText.fontSize = buttonTxtRef.fontSize;
            filterPlaceholderText.fontStyle = FontStyle.Italic;
            filterPlaceholderText.text = "Filter by mod name parts or #parameter name";
            filterPlaceholderText.color = new Color(0.7f, 0.7f, 0.7f);

            var filterText = filterTextGo.AddComponent<Text>();
            filterText.font = buttonTxtRef.font;
            filterText.fontSize = buttonTxtRef.fontSize;
            filterText.fontStyle = buttonTxtRef.fontStyle;

            filterRt.sizeDelta = new Vector2(1000, buttonTxtRef.fontSize + 20);
            RectTransform filterTextPlaceholderRt = filterTextPlaceholderGo.GetComponent<RectTransform>();
            filterTextPlaceholderRt.localPosition = new Vector3(5, 0, 0);
            filterTextPlaceholderRt.sizeDelta = filterRt.sizeDelta;
            RectTransform filterTxtRt = filterTextGo.GetComponent<RectTransform>();
            filterTxtRt.sizeDelta = filterRt.sizeDelta;
            filterTxtRt.localPosition = new Vector3(5, 0, 0);

            filterInput.textComponent = filterText;
            filterInput.text = filterValue;
            filterInput.targetGraphic = filterImg;
            filterInput.placeholder = filterPlaceholderText;
            filterInput.caretWidth = 3;
            filterInput.caretColor = Color.white;

            filterInput.onValueChanged.AddListener(new UnityAction<string>(s =>
            {
                if (filterValue != s)
                {
                    filterValue = s ?? "";
                    RenderPluginListDebounce();
                }
            }));

            filterInput.enabled = false;
            filterInput.enabled = true;


            filterCount = new GameObject("ModConfigMenu_Filter_Count");
            filterCount.transform.SetParent(filterGo.transform, false);

            var filterCountTxt = filterCount.AddComponent<Text>();
            filterCountTxt.font = buttonTxtRef.font;
            filterCountTxt.fontSize = buttonTxtRef.fontSize;
            filterCountTxt.fontStyle = buttonTxtRef.fontStyle;
            filterCountTxt.alignment = TextAnchor.MiddleCenter;
            filterCountTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            filterCountTxt.text = "        ";

            var filterCountRt = filterCount.GetComponent<RectTransform>();
            filterCountRt.localPosition = new Vector3(filterRt.sizeDelta.x / 2 + filterCountTxt.preferredWidth / 2 + 30, 0, 0);

            filterGo.SetActive(false);
        }


        static void RenderPluginListDebounce()
        {
            var img = filterGo.GetComponent<Image>();
            if (coroutineRenderPluginListDelayed != null)
            {
                img.StopCoroutine(coroutineRenderPluginListDelayed);
            }
            coroutineRenderPluginListDelayed = img.StartCoroutine(RenderPluginListDelayed());
        }
        
        static IEnumerator RenderPluginListDelayed()
        {
            yield return new WaitForSeconds(0.25f);
            RenderPluginList();
        }

        static void RenderPluginList()
        {
            if (!enterMainMenu)
            {
                // after the first create, the offsets go way off for some reason, fix it here
                otherOptionsX = 1370;
            }
            for (int i = modScrollContent.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(modScrollContent.transform.GetChild(i).gameObject);
            }

            var filterParts = filterValue.Split(' ').Where(s => s.Length != 0);

            var j = 0f;
            var cnt = 0;
            foreach (var pi in pluginList)
            {
                if (filterValue != "")
                {
                    bool found = true;
                    foreach (var ft in filterParts)
                    {
                        if (ft.StartsWith("#"))
                        {
                            bool foundKey = false;
                            foreach (var cef in pi.Instance.Config.Keys)
                            {
                                if (cef.Key.Contains(ft.Substring(1), StringComparison.InvariantCultureIgnoreCase))
                                {
                                    foundKey = true;
                                    break;
                                }
                            }
                            if (!foundKey)
                            {
                                found = false;
                            }
                        }
                        else
                        if (!pi.Metadata.GUID.Contains(ft, StringComparison.InvariantCultureIgnoreCase) 
                            && !pi.Metadata.Name.Contains(ft, StringComparison.InvariantCultureIgnoreCase))
                        {
                            found = false;
                            break;
                        }
                    }
                    if (!found)
                    {
                        continue;
                    }
                }

                cnt++;

                // Create the mods header row with version
                // ---------------------------------------

                var amod = Instantiate(otherOptions);
                amod.name = pi.Metadata.GUID;
                Destroy(amod.GetComponentInChildren<LocalizedText>());
                var amodImg = amod.GetComponentInChildren<Image>();
                amodImg.enabled = true;

                var txt = amod.transform.Find("Label/Text").GetComponent<Text>();

                txt.supportRichText = true;
                txt.text = pi.Metadata.Name + " <color=#FFFF00>v" + pi.Metadata.Version + "</color>";

                amod.transform.SetParent(modScrollContent.transform, false);

                var rtLine = amod.GetComponent<RectTransform>();

                rtLine.localPosition = new Vector3(otherOptionsX, j, 0);

                // Create the open config file button
                // ----------------------------------

                var amodOpenCfg = new GameObject(amod.name + "-openconfig");
                amodOpenCfg.transform.SetParent(amod.transform, false);

                var amodOpenCfgImg = amodOpenCfg.AddComponent<Image>();
                amodOpenCfgImg.color = new Color(0.5f, 0, 0.5f);

                var amodOpenCfgTextGo = new GameObject(amod.name + "-openconfig-button");
                amodOpenCfgTextGo.transform.SetParent(amodOpenCfgImg.transform, false);

                var amodOpenCfgTxt = amodOpenCfgTextGo.AddComponent<Text>();
                amodOpenCfgTxt.text = " Open .cfg ";
                amodOpenCfgTxt.font = txt.font;
                amodOpenCfgTxt.fontSize = txt.fontSize;
                amodOpenCfgTxt.fontStyle = txt.fontStyle;
                amodOpenCfgTxt.color = Color.white;
                amodOpenCfgTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                amodOpenCfgTxt.alignment = TextAnchor.MiddleCenter;

                var amodOpenCfgRt = amodOpenCfg.GetComponent<RectTransform>();
                amodOpenCfgRt.localPosition = new Vector3(1200, -rtLine.sizeDelta.y / 2, 0);
                amodOpenCfgRt.sizeDelta = new Vector2(amodOpenCfgTxt.preferredWidth + 20, amodOpenCfgTxt.preferredHeight + 20);

                var amodOpenCfgBtn = amodOpenCfg.AddComponent<Button>();
                var amodOpenCfgRef = pi.Instance.Config;
                amodOpenCfgBtn.onClick.AddListener(new UnityAction(() =>
                {
                    Application.OpenURL("file:///" + amodOpenCfgRef.ConfigFilePath);
                }));


                // Create a sorted set of per mod options
                // --------------------------------------

                j -= rtLine.sizeDelta.y;

                List<string> sections = new(pi.Instance.Config.Keys.Select(e => e.Section).Distinct());
                sections.Sort(StringComparer.OrdinalIgnoreCase);

                if (sections.Count == 0)
                {
                    var amodCfg = Instantiate(otherOptions);
                    amodCfg.name = "NoConfig";
                    amodCfg.transform.SetParent(modScrollContent.transform, false);

                    amodCfg.GetComponentInChildren<Image>().enabled = false;

                    Destroy(amodCfg.GetComponentInChildren<LocalizedText>());

                    txt = amodCfg.transform.Find("Label/Text").GetComponent<Text>();

                    txt.supportRichText = true;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.fontStyle = FontStyle.BoldAndItalic;

                    var rt = amodCfg.GetComponent<RectTransform>();

                    rt.localPosition = new Vector3(otherOptionsX, j, 0);

                    txt.text = "( No configuration options declared )";

                    j -= rt.sizeDelta.y;
                }
                else
                {

                    foreach (var sec in sections)
                    {
                        foreach (var ce in pi.Instance.Config)
                        {
                            if (sec == ce.Key.Section)
                            {
                                var amodCfg = Instantiate(otherOptions);
                                amodCfg.name = ce.Key.Section + "-" + ce.Key.Key;
                                amodCfg.transform.SetParent(modScrollContent.transform, false);

                                amodCfg.GetComponentInChildren<Image>().enabled = false;

                                Destroy(amodCfg.GetComponentInChildren<LocalizedText>());

                                txt = amodCfg.transform.Find("Label/Text").GetComponent<Text>();

                                txt.supportRichText = true;
                                txt.horizontalOverflow = HorizontalWrapMode.Overflow;

                                var rt = amodCfg.GetComponent<RectTransform>();

                                rt.localPosition = new Vector3(otherOptionsX, j, 0);

                                txt.text = "[" + ce.Key.Section + "] " + ce.Key.Key;

                                if (ce.Value.SettingType == typeof(bool))
                                {

                                    var editorGo = new GameObject(ce.Key.Section + "-" + ce.Key.Key + "-editor");
                                    editorGo.transform.SetParent(txt.transform, false);

                                    editorGo.AddComponent<GraphicRaycaster>();

                                    var editorRt = editorGo.GetComponent<RectTransform>();

                                    var img = editorGo.AddComponent<Image>();
                                    var colorOff = new Color(0.1f, 0.1f, 0.1f);
                                    var colorOn = new Color(0f, 1f, 0f);
                                    img.color = ce.Value.GetSerializedValue() == "true" ? colorOn : colorOff;

                                    var bw = txt.fontSize + 20;
                                    editorRt.localPosition = new Vector2(bw / 2, editorRt.localPosition.y);
                                    editorRt.sizeDelta = new Vector2(bw, bw);

                                    var btnComp = editorGo.AddComponent<Button>();
                                    var ceb = ce.Value;
                                    var txt2 = txt;

                                    var pcfg = pi.Instance.Config;

                                    btnComp.onClick.AddListener(new UnityAction(() =>
                                    {
                                        ceb.SetSerializedValue(ce.Value.GetSerializedValue() == "true" ? "false" : "true");
                                        pcfg.Save();
                                        img.color = ce.Value.GetSerializedValue() == "true" ? colorOn : colorOff;
                                    }));
                                }
                                else
                                {
                                    var editorGo = new GameObject(ce.Key.Section + "-" + ce.Key.Key + "-editor");
                                    editorGo.transform.SetParent(txt.transform, false);

                                    editorGo.AddComponent<GraphicRaycaster>();

                                    var editorRt = editorGo.GetComponent<RectTransform>();

                                    var img = editorGo.AddComponent<Image>();
                                    img.color = new Color(0.1f, 0.1f, 0.1f);

                                    var editor = editorGo.AddComponent<InputField>();

                                    var txt3Go = new GameObject(editorGo.name + "-text");
                                    var txt3 = txt3Go.AddComponent<Text>();
                                    txt3.transform.SetParent(editorGo.transform, false);
                                    var rt3 = txt3.GetComponent<RectTransform>();
                                    rt3.localPosition = new Vector3(5, -5, 0);
                                    txt3.font = txt.font;
                                    txt3.fontSize = txt.fontSize;
                                    txt3.fontStyle = txt.fontStyle;
                                    txt3.horizontalOverflow = HorizontalWrapMode.Overflow;
                                    txt3.verticalOverflow = VerticalWrapMode.Overflow;

                                    editor.textComponent = txt3;
                                    editor.targetGraphic = img;
                                    editor.caretWidth = 3;

                                    editor.text = ce.Value.GetSerializedValue();
                                    var ew = 1300;
                                    editorRt.localPosition = new Vector2(ew / 2, editorRt.localPosition.y);
                                    editorRt.sizeDelta = new Vector2(ew, txt.fontSize + 20);
                                    rt3.sizeDelta = editorRt.sizeDelta;


                                    var ceb = ce.Value;

                                    var pcfg = pi.Instance.Config;
                                    editor.onValueChanged.AddListener(new UnityAction<string>(e =>
                                    {
                                        ceb.SetSerializedValue(e);
                                        pcfg.Save();
                                    }));

                                    editor.caretColor = Color.white;
                                    editor.enabled = false;
                                    editor.enabled = true;
                                }

                                j -= rt.sizeDelta.y;


                                var hover = amodCfg.gameObject.AddComponent<ModConfigEntryHover>();
                                hover.cfg = ce.Value;
                            }
                        }
                    }
                }
            }

            var contentRect = modScrollContent.GetComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Math.Abs(j));

            var cntStr = cnt.ToString();
            if (cnt != pluginList.Count)
            {
                cntStr += " / " + pluginList.Count;
            }

            var filterCountTxt = filterCount.GetComponent<Text>();
            filterCountTxt.text = cntStr;
            var filterRt = filterGo.GetComponent<RectTransform>();
            filterCount.GetComponent<RectTransform>().localPosition = new Vector3(filterRt.sizeDelta.x / 2 + filterCountTxt.preferredWidth / 2 + 30, 0, 0);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
                Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["ModConfigMenu_Button"] = "Modok";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["ModConfigMenu_Button"] = "Mods";
            }
        }

        class ModConfigEntryHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {

            GameObject tooltipGo;

            internal ConfigEntryBase cfg;

            public void OnPointerEnter(PointerEventData eventData)
            {
                tooltipGo = new GameObject(gameObject.name + "-tooltip");
                tooltipGo.transform.SetParent(modScroll.transform.parent, false);

                var img = tooltipGo.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.3f);

                var outl = tooltipGo.AddComponent<Outline>();
                outl.effectColor = new Color(0.8f, 0.8f, 0.8f);
                outl.effectDistance = new Vector2(4, 4);

                var tooltipTxt = new GameObject(gameObject.name + "-tooltip-text");
                tooltipTxt.transform.SetParent(tooltipGo.transform, false);

                var txtExample = gameObject.GetComponentInChildren<Text>();
                var txt = tooltipTxt.AddComponent<Text>();

                txt.supportRichText = true;
                txt.font = txtExample.font;
                txt.fontSize = txtExample.fontSize;
                txt.fontStyle = FontStyle.Normal;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.alignment = TextAnchor.MiddleCenter;

                txt.text = "<color=#FFFF00>" + cfg.Description.Description + "</color>\n\n"
                    + "Type: <color=#FF8080>" + cfg.SettingType + "</color>   |    Default: <color=#00FF00>" + cfg.DefaultValue + "</color>";
                
                var rtTxt = tooltipTxt.GetComponent<RectTransform>();

                rtTxt.localPosition = new Vector3(0, 0, 0);
                rtTxt.sizeDelta = new Vector3(txt.preferredWidth, txt.preferredHeight, 0);

                var pos2 = tooltipGo.transform.InverseTransformPoint(gameObject.transform.position);

                var rtImg = tooltipGo.GetComponent<RectTransform>();
                rtImg.localPosition = pos2 + new Vector3(-1350 + txt.preferredWidth / 2, -txt.preferredHeight - 40); // new Vector3(-1000, -1000, 0);
                rtImg.sizeDelta = rtTxt.sizeDelta + new Vector2(20, 20);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Destroy(tooltipGo);
            }
        }
    }
}
