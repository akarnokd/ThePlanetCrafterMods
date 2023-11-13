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
                var buttonMods = Instantiate(buttonGraphics);
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

                var modScroll = Instantiate(scrollViewControls);
                modScroll.name = "Scroll View Mods";
                modScroll.transform.SetParent(scrollViewControls.transform.parent, false);
                modScroll.SetActive(false);

                optionsPanel.tabsContent.Add(modScroll);
                var tabIndex = optionsPanel.tabsContent.Count - 1;

                bc.AddListener(() => optionsPanel.OnClickTab(tabIndex));

                var modScrollContent = modScroll.transform.Find("Viewport/Content").gameObject;

                var otherOptions = modScroll.transform.Find("Viewport/Content/Other Options");
                var otherOptionsX = otherOptions.GetComponent<RectTransform>().localPosition.x;

                for (int i = modScrollContent.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(modScrollContent.transform.GetChild(i).gameObject);
                }

                List<BepInEx.PluginInfo> pluginList = new(Chainloader.PluginInfos.Values);
                pluginList.Sort((a, b) =>
                {
                    return a.Metadata.Name.CompareTo(b.Metadata.Name);
                });

                var j = 0f;
                foreach (var pi in pluginList)
                {
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

                    j -= rtLine.sizeDelta.y;

                    List<string> sections = new(pi.Instance.Config.Keys.Select(e => e.Section).Distinct());
                    sections.Sort(StringComparer.OrdinalIgnoreCase);

                    foreach (var sec in sections)
                    {
                        foreach (var ce in pi.Instance.Config)
                        {
                            if (sec == ce.Key.Section)
                            {
                                var amodCfg = Instantiate(otherOptions);
                                amodCfg.name = ce.Key.Section + "-" + ce.Key.Key;

                                Destroy(amodCfg.GetComponentInChildren<LocalizedText>());

                                txt = amodCfg.transform.Find("Label/Text").GetComponent<Text>();

                                txt.supportRichText = true;
                                txt.horizontalOverflow = HorizontalWrapMode.Overflow;

                                amodCfg.transform.SetParent(modScrollContent.transform, false);

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

                            }
                        }
                    }
                }

                var contentRect = modScrollContent.GetComponent<RectTransform>();
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, Math.Abs(j));
            }
            else
            {
                logger.LogInfo("ButtonControls" + (buttonControls != null));
                logger.LogInfo("ButtonGraphics" + (buttonGraphics != null));
                logger.LogInfo("Scroll View Control" + (scrollViewControls != null));
                logger.LogInfo("OptionsPanel" + (optionsPanel != null));
            }

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

        class ButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            internal Action<PointerEventData> onEnter;

            internal Action<PointerEventData> onExit;

            public void OnPointerEnter(PointerEventData eventData)
            {
                onEnter?.Invoke(eventData);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                onExit?.Invoke(eventData);
            }
        }
    }
}
