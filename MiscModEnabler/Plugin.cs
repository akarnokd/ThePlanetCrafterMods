using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.IO;
using System;
using System.Globalization;

namespace MiscModEnabler
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscmodenabler", "(Misc) Mod Enabler", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo("Checking for BepInEx.cfg HideManagerGameObject");

            var me = Assembly.GetExecutingAssembly();

            var dir = Path.GetDirectoryName(me.Location);

            int i = dir.ToLower(CultureInfo.InvariantCulture).IndexOf("bepinex");

            if (i >= 0)
            {
                var newdir = dir.Substring(0, i) + "bepinex\\config\\BepInEx.cfg";

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
                                Harmony.CreateAndPatchAll(typeof(Plugin));
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
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start(Intro __instance)
        {
            ShowDialog("Mods should now work properly.\n\nPlease restart the game.");
        }

        static void ShowDialog(string message)
        {
            var panel = new GameObject("MiscModEnabler_Notification");
            var canvas = panel.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var background = new GameObject("MiscModEnabler_Notification_Background");
            background.transform.SetParent(panel.transform, false);
            var img = background.AddComponent<Image>();
            img.color = new Color(0.5f, 0, 0, 0.95f);

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
            brect.sizeDelta = trect.sizeDelta + new Vector2(10, 10);
        }
    }
}
