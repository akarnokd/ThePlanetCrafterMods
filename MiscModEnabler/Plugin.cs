// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.IO;
using System;
using System.Globalization;
using TMPro;
using BepInEx.Bootstrap;
using Galaxy.Api;
using System.Net.Http.Headers;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MiscModEnabler
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscmodenabler", "(Misc) Mod Enabler", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static bool displayMessage;

        static Plugin self;

        public void Awake()
        {
            self = this;

            LibCommon.BepInExLoggerFix.ApplyFix();


            // Plugin startup logic
            Logger.LogInfo("Checking for BepInEx.cfg HideManagerGameObject");

            var me = Assembly.GetExecutingAssembly();

            var dir = Path.GetDirectoryName(me.Location);

            int i = dir.ToLower(CultureInfo.InvariantCulture).IndexOf("bepinex");

            if (i >= 0)
            {
                var newdir = dir[..i] + "bepinex\\config\\BepInEx.cfg";

                if (File.Exists(newdir))
                {
                    try
                    {
                        var lines = File.ReadAllLines(newdir);

                        var found = false;
                        var save = false;
                        for (int i1 = 0; i1 < lines.Length; i1++)
                        {
                            string line = lines[i1];
                            if (line.StartsWith("HideManagerGameObject"))
                            {
                                if (line.EndsWith("true"))
                                {
                                    Logger.LogInfo("    HideManagerGameObject = true, all is ok");
                                    found = true;
                                }
                                else
                                {
                                    Logger.LogInfo("    HideManagerGameObject = false, setting it to true");
                                    lines[i1] = "HideManagerGameObject = true";
                                    save = true;
                                }
                                break;
                            }
                        }
                        if (!found)
                        {
                            if (save)
                            {
                                File.WriteAllLines(newdir, lines);
                                displayMessage = true;
                            }
                            else
                            {
                                Logger.LogWarning("BepInEx.cfg is missing the default HideManagerGameObject. Please check your BepInEx version");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogInfo(ex);
                    }
                }
                else
                {
                    Logger.LogWarning("Could not locate BepInEx config file: " + newdir);
                }
            }
            else
            {
                Logger.LogWarning("Could not locate BepInEx directory: " + dir);
            }
            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Awake")]
        static void Intro_Awake(Intro __instance)
        {
            try
            {
                var logo = GameObject.Find("Canvases/CanvasBase/Logo");
                var logoRt = logo.GetComponent<RectTransform>();

                var subtitleTmp = logo.GetComponentInChildren<TextMeshProUGUI>(true);
                var subtitleRt = subtitleTmp.GetComponent<RectTransform>();

                var version = new GameObject("MiscModEnablerVersion");
                version.transform.SetParent(logo.transform, false);

                var txt = version.AddComponent<TextMeshProUGUI>();
                txt.color = Color.yellow;
                var str = "v" + Application.version;
                try
                {
                    if (SteamManager.Initialized)
                    {
                        str += " - Steam";
                        if (Steamworks.SteamApps.GetCurrentBetaName(out var text, 256))
                        {
                            str += " - " + text;
                        }
                    }
                } catch (Exception exc)
                {
                    self.Logger.LogError(exc);
                }

                str += " - Modded (" + Chainloader.PluginInfos.Count + " mods)";
                txt.text = str;
                txt.font = subtitleTmp.font;
                txt.fontSize = Math.Max(12, subtitleTmp.fontSize / 2);
                txt.textWrappingMode = TextWrappingModes.NoWrap;

                var rt = txt.rectTransform;
                rt.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

                var dw = Math.Max(0, (logoRt.sizeDelta.x - rt.sizeDelta.x) / 2);

                rt.localPosition = subtitleRt.localPosition + new Vector3(rt.sizeDelta.x / 2 + dw, -rt.sizeDelta.y, 0);
            }
            catch (Exception ex)
            {
                self.Logger.LogError(ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            if (displayMessage)
            {
                ShowDialog("Mods should now work properly.\n\nPlease restart the game.", true);
            }
            try
            {
                var modVersionLog = Path.Combine(Application.persistentDataPath, "lastgameversion.txt");
                if (File.Exists(modVersionLog))
                {
                    var log = File.ReadAllText(modVersionLog).Trim();
                    if (log != Application.version)
                    {
                        ShowDialog("The game just updated from <color=#FFCC00>v"
                            + log + "</color> to <color=#FFCC00>v" + Application.version + "</color> !"
                            + "\n"
                            + "\nIf you are experiencing problems,"
                            + "\nplease update your mods as soon as possible."
                            + "\n"
                            + "\n=[ Press ESC / Gamepad-B to continue ]="
                            , false);
                        File.WriteAllText(modVersionLog, Application.version);
                    }
                }
                else
                {
                    File.WriteAllText(modVersionLog, Application.version);
                }
            }
            catch (Exception ex)
            {
                self.Logger.LogError(ex);
            }
        }

        static void ShowDialog(string message, bool warning)
        {
            var panel = new GameObject("MiscModEnabler_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var background = new GameObject("MiscModEnabler_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            if (warning)
            {
                img.color = new Color(0.5f, 0, 0, 0.99f);
            }
            else
            {
                img.color = new Color(0, 0.4f, 0, 0.99f);
            }

            var text = new GameObject("MiscModEnabler_Notification_Text");
            text.transform.SetParent(background.transform, false);

            var txt = text.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.supportRichText = true;
            txt.text = message;
            txt.color = Color.white;
            txt.fontSize = 30;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleLeft;

            var trect = text.GetComponent<RectTransform>();
            trect.sizeDelta = new Vector2(txt.preferredWidth, txt.preferredHeight);

            var brect = background.GetComponent<RectTransform>();
            brect.sizeDelta = trect.sizeDelta + new Vector2(30, 30);

            panel.AddComponent<DialogCloser>();
        }

        class DialogCloser : MonoBehaviour
        {
            void Update()
            {
                var gamepad = Gamepad.current;
                if (Keyboard.current[Key.Escape].wasPressedThisFrame
                    || (gamepad != null && (gamepad[GamepadButton.B]?.wasPressedThisFrame ?? false)))
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
