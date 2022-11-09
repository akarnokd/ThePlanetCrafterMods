using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using BepInEx.Bootstrap;
using UnityEngine.InputSystem;
using System.Reflection;
using BepInEx.Logging;
using System.Linq;
using System.Collections;
using TMPro;

namespace UIBeaconText
{
    [BepInPlugin(modUiBeaconText, "(UI) Beacon Text", "1.0.0.0")]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiBeaconText = "akarnokd.theplanetcraftermods.uibeacontext";
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ConfigEntry<int> fontSize;

        static ManualLogSource logger;

        static TextMeshProUGUI mockupTMPro;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the slot index");
            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Found " + modFeatMultiplayerGuid + ", beacon text updated will sync too");
            }
            else
            {
                Logger.LogInfo("Not Found " + modFeatMultiplayerGuid);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineBeaconUpdater), "Start")]
        static void MachineBeaconUpdater_Start(MachineBeaconUpdater __instance, GameObject ___player, 
            GameObject ___canvas, float ___scaleFactor, float ___updateEverySec)
        {
            var woa = __instance.GetComponent<WorldObjectAssociated>();
            if (woa == null)
            {
                return;
            }

            var s = 0.005f;
            var offset = 0.15f;

            GameObject title = new GameObject("BeaconTitle");
            title.transform.SetParent(___canvas.transform);
            title.transform.localPosition = new Vector3(0, offset, 0);
            title.transform.localScale = new Vector3(s, s, s);

            var titleText = title.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.text = "Title";
            titleText.color = Color.white;
            titleText.fontSize = fontSize.Value;
            titleText.resizeTextForBestFit = false;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.alignment = TextAnchor.MiddleCenter;

            GameObject distance = new GameObject("BeaconDistance");
            distance.transform.SetParent(___canvas.transform);
            distance.transform.localPosition = new Vector3(0, -offset, 0);
            distance.transform.localScale = new Vector3(s, s, s);

            var distanceText = distance.AddComponent<Text>();
            distanceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            distanceText.text = "...";
            distanceText.color = Color.white;
            distanceText.fontSize = fontSize.Value;
            distanceText.resizeTextForBestFit = false;
            distanceText.verticalOverflow = VerticalWrapMode.Overflow;
            distanceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            distanceText.alignment = TextAnchor.MiddleCenter;

            logger.LogInfo("Finding the World Object of the beacon");
            var wo = woa.GetWorldObject();
            if (wo.GetText() == null || wo.GetText() == "...")
            {
                wo.SetText("");
            }

            logger.LogInfo("Adding default TMPro");
            if (mockupTMPro == null)
            {
                mockupTMPro = new GameObject("BeaconMockupTMPro").AddComponent<TextMeshProUGUI>();
            }

            logger.LogInfo("Finding Antena_01");
            var antena = __instance.gameObject.transform.Find("Container/Antena_01");

            logger.LogInfo("Adding WorldObjectText");
            var wot = antena.gameObject.AddComponent<WorldObjectText>();
            wot.textContainer = mockupTMPro; // Not sure why this field is even here, it is unused.
            wot.SetWorldObjectForText(wo);

            logger.LogInfo("Adding Action for text editor");
            var atwo = antena.gameObject.AddComponent<ActionTextWorldObject>();
            atwo.worldObjectText = wot;

            logger.LogInfo("Starting updater");
            __instance.StartCoroutine(TextUpdater(___player, ___updateEverySec, __instance.gameObject, wo, titleText, distanceText));
        }

        static IEnumerator TextUpdater(GameObject ___player, float ___updateEverySec, GameObject canvas, 
            WorldObject wo,
            Text titleText,
            Text distanceText)
        {
            for (; ; )
            {
                var dist = (int)Vector3.Distance(canvas.transform.position, ___player.transform.position);
                distanceText.text = dist + "m";
                titleText.text = wo.GetText();
                yield return new WaitForSeconds(___updateEverySec);
            }
        }

        private static T GetApi<T>(BepInEx.PluginInfo pi, string name)
        {
            var fi = AccessTools.Field(pi.Instance.GetType(), name);
            if (fi == null)
            {
                throw new NullReferenceException("Missing field " + pi.Instance.GetType() + "." + name);
            }
            return (T)fi.GetValue(null);
        }
    }
}
