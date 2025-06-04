// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using UnityEngine.UI;
using System;
using System.Reflection;
using UnityEngine.InputSystem;
using System.Text;
using LibCommon;

namespace UISortSaves
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uisortsaves", "(UI) Sort Saves On The Load Screen", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<int> sortMode;
        static ConfigEntry<int> fontSize;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            sortMode = Config.Bind("General", "SortMode", 1, "Sorting mode: 0=default, 1=newest, 2=oldest, 3=name ascending, 4=name descending");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static SaveFilesSelector instance;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.RefreshList))]
        static bool SaveFilesSelector_RefreshList(SaveFilesSelector __instance, List<GameObject> ____objectsInList)
        {
            instance = __instance;
            Text currentText = currentMode.GetComponent<Text>();
            int mode = sortMode.Value;
            if (mode > 0)
            {
                logger.LogInfo("RefreshList: " + mode);
                MethodInfo mi = AccessTools.Method(typeof(SaveFilesSelector), "AddSaveToList", [typeof(string)]);
                Func<string, GameObject> addSaveToList = AccessTools.MethodDelegate<Func<string, GameObject>>(mi, __instance);

                string[] files = Directory.GetFiles(Application.persistentDataPath, "*.json");
                foreach (GameObject obj in ____objectsInList)
                {
                    UnityEngine.Object.Destroy(obj);
                }
                ____objectsInList.Clear();

                // The vanilla AddSaveToList() adds items reversed, so sorting must be reversed
                switch (mode)
                {
                    case 1:
                        {
                            currentText.text = "Newest";
                            Array.Sort(files, (a, b) =>
                            {
                                return File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b));
                            });
                            break;
                        }
                    case 2:
                    {
                            currentText.text = "Oldest";
                            Array.Sort(files, (a, b) =>
                            {
                                return File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a));
                            });
                            break;
                    }
                    case 3:
                        {
                            currentText.text = "Name Asc";
                            Array.Sort(files, (a, b) =>
                            {
                                var fa = Path.GetFileName(a);
                                var fb = Path.GetFileName(b);

                                return NaturalStringComparer.Instance.Compare(fb, fa);
                            });
                            break;
                        }
                    case 4:
                        {
                            currentText.text = "Name Desc";
                            Array.Sort(files, (a, b) =>
                            {
                                var fa = Path.GetFileName(a);
                                var fb = Path.GetFileName(b);

                                return NaturalStringComparer.Instance.Compare(fa, fb);
                            });
                            break;
                        }
                }

                foreach (string file in files)
                {
                    logger.LogInfo(file);
                    string nameOfSave = __instance.GetNameOfSave(file);
                    if (!(nameOfSave == GameConfig.saveHiddenIdentifier))
                    {
                        GameObject item = addSaveToList(nameOfSave);
                        ____objectsInList.Add(item);
                    }
                }
                __instance.filesListContainer.GetComponentInChildren<ScrollRect>().normalizedPosition = new Vector2(500f, 500f);
                return false;
            }
            currentText.text = "Default";
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.SelectedSaveFile))]
        static void SaveFilesSelector_SelectedSaveFile()
        {
            instance = null;
            if (parent != null)
            {
                parent.SetActive(false);
            }
        }

        static GameObject parent;
        static GameObject prev;
        static GameObject next;
        static GameObject currentMode;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), "Start")]
        static void SaveFilesSelector_Start()
        {
            CreateOrUpdateButtons();
        }

        static int lastScreenWidth;
        static int lastScreenHeight;

        static void CreateOrUpdateButtons()
        {
            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            {
                Destroy(parent);
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
            }

            if (parent != null)
            {
                return;
            }


            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            parent = new GameObject("SaveFilesSelectorSorting");
            Canvas canvas = parent.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            int fs = fontSize.Value;
            int dx = -Screen.width / 2 + 30;
            int dy = Screen.height / 2 - 3 * fs;

            Text text;
            RectTransform rectTransform;

            // -------------------------

            prev = new GameObject("SaveListPrev");
            prev.transform.parent = parent.transform;

            text = prev.AddComponent<Text>();
            text.font = font;
            text.text = "[ <- ]";
            text.color = new Color(1f, 1f, 1f, 1f);
            text.fontSize = (int)fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = true;

            rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx + fs * 3, dy + fs);
            rectTransform.sizeDelta = new Vector2(fs * 6, fs + 5);

            // -------------------------

            currentMode = new GameObject();
            currentMode.transform.parent = parent.transform;

            text = currentMode.AddComponent<Text>();
            text.font = font;
            text.text = "";
            text.color = new Color(1f, 1f, 1f, 1f);
            text.fontSize = (int)fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;

            rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx + 150 + fs * 7, dy + fs);
            rectTransform.sizeDelta = new Vector2(300, fs + 5);

            switch (sortMode.Value)
            {
                case 1:
                    {
                        text.text = "Newest";
                        break;
                    }
                case 2:
                    {
                        text.text = "Oldest";
                        break;
                    }
                case 3:
                    {
                        text.text = "Name Asc";
                        break;
                    }
                case 4:
                    {
                        text.text = "Name Desc";
                        break;
                    }
                default:
                    {
                        text.text = "Default";
                        break;
                    }
            }

            // -------------------------

            next = new GameObject("SaveListNext");
            next.transform.parent = parent.transform;

            text = next.AddComponent<Text>();
            text.font = font;
            text.text = "[ -> ]";
            text.color = new Color(1f, 1f, 1f, 1f);
            text.fontSize = (int)fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = true;

            rectTransform = text.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector2(dx + 300 + fs * 3, dy + fs);
            rectTransform.sizeDelta = new Vector2(fs * 6, fs + 5);

            // -------------------------

            parent.SetActive(false);
        }

        void Update()
        {

            if (instance != null)
            {
                CreateOrUpdateButtons();

                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    var mouse = Mouse.current.position.ReadValue();
                    if (IsWithin(prev, mouse))
                    {
                        OnPrevNext(instance, false);
                    }
                    if (IsWithin(next, mouse))
                    {
                        OnPrevNext(instance, true);
                    }
                }
                if (Keyboard.current[Key.LeftArrow].wasPressedThisFrame)
                {
                    OnPrevNext(instance, false);
                }
                if (Keyboard.current[Key.RightArrow].wasPressedThisFrame)
                {
                    OnPrevNext(instance, true);
                }

                if (!instance.filesListContainer.transform.parent.gameObject.activeSelf)
                {
                    parent.SetActive(false);
                }
            }
        }

        static bool IsWithin(GameObject go, Vector2 mouse)
        {
            RectTransform rect = go.GetComponent<Text>().GetComponent<RectTransform>();

            var lp = rect.localPosition;
            lp.x += Screen.width / 2 - rect.sizeDelta.x / 2;
            lp.y += Screen.height / 2 - rect.sizeDelta.y / 2;

            return mouse.x >= lp.x && mouse.y >= lp.y 
                && mouse.x <= lp.x + rect.sizeDelta.x && mouse.y <= lp.y + rect.sizeDelta.y;
        }

        static void OnPrevNext(SaveFilesSelector __instance, bool isNext)
        {
            logger.LogInfo((isNext ? "Next clicked" : "Prev clicked") + ": " + sortMode.Value);
            if (isNext)
            {
                sortMode.Value = Math.Min(4, sortMode.Value + 1);
            } 
            else
            {
                sortMode.Value = Math.Max(0, sortMode.Value - 1);
            }
            __instance.RefreshList();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Intro), nameof(Intro.ShowSaveFilesList))]
        static void Intro_ShowSaveFilesList()
        {
            CreateOrUpdateButtons();
            parent.AsNullable()?.SetActive(true);
        }

    }

    /// <summary>
    /// From https://stackoverflow.com/a/58328837/61158
    /// </summary>
    public class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer Instance { get; } = new NaturalStringComparer();

        public int Compare(string x, string y)
        {
            var sx = FindSections(x);
            var sy = FindSections(y);

            for (int i = 0; i < Math.Min(sx.Count, sy.Count); i++)
            {
                var se1 = sx[i];
                var se2 = sy[i];

                if (se1 is string s1 && se2 is string s2)
                {
                    int c = s1.CompareTo(s2);
                    if (c != 0)
                    {
                        return c;
                    }
                }
                else if (se1 is string && se2 is int)
                {
                    return 1;
                }
                else if (se1 is int && se2 is string)
                {
                    return -1;
                }
                else if (se1 is int i1 && se2 is int i2)
                {
                    int c = i1.CompareTo(i2);
                    if (c != 0)
                    {
                        return c;
                    }
                }
            }
            if (sx.Count > sy.Count)
            {
                return 1;
            }
            if (sx.Count < sy.Count)
            {
                return -1;
            }
            return 0;
        }

        public List<object> FindSections(string s)
        {
            List<object> sections = [];
            if (s.Length > 0)
            {
                StringBuilder sb = new();
                sb.Append(s[0]);
                bool digitLast = char.IsDigit(s[0]);
                for (int i = 0; i < s.Length; i++)
                {
                    bool digit = char.IsDigit(s[i]);
                    if (digitLast && !digit)
                    {
                        sections.Add(int.Parse(sb.ToString()));
                        sb.Clear();
                    }
                    else if (!digitLast && digit)
                    {
                        sections.Add(sb.ToString());
                        sb.Clear();
                    }

                    sb.Append(s[i]);

                    digitLast = digit;
                }
                if (digitLast)
                {
                    sections.Add(int.Parse(sb.ToString()));
                }
                else
                {
                    sections.Add(sb.ToString());
                }
            }
            return sections;
        }
    }
}
