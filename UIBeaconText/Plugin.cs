using BepInEx;
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
using UnityEngine.UIElements;

namespace UIBeaconText
{
    [BepInPlugin(modUiBeaconText, "(UI) Beacon Text", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiBeaconText = "akarnokd.theplanetcraftermods.uibeacontext";
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ConfigEntry<int> fontSize;

        static ConfigEntry<int> displayMode;

        static ConfigEntry<bool> showDistanceOnTop;

        static ConfigEntry<bool> hideVanillaLabel;

        static ConfigEntry<string> displayModeToggle;

        static ManualLogSource logger;

        static InputAction toggleAction;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size.");

            displayMode = Config.Bind("General", "DisplayMode", 1, "Display: 0 - no text no distance, 1 - distance only, 2 - text only, 3 - distance + text.");

            displayModeToggle = Config.Bind("General", "DisplayModeToggleKey", "B", "The toggle key for changing the display mode.");

            showDistanceOnTop = Config.Bind("General", "ShowDistanceOnTop", true, "Show the distance above the beacon hexagon if true, below if false");

            hideVanillaLabel = Config.Bind("General", "HideVanillaLabel", false, "If true, the vanilla beacon text is hidden and replaced by this mod's label");

            if (!displayModeToggle.Value.StartsWith("<Keyboard>/"))
            {
                displayModeToggle.Value = "<Keyboard>/" + displayModeToggle.Value;
            }
            toggleAction = new InputAction("DisplayModeToggleKey", binding: displayModeToggle.Value);
            toggleAction.Enable();

            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var _))
            {
                Logger.LogInfo("Found " + modFeatMultiplayerGuid + ", beacon text updates will sync too, probably");
            }
            else
            {
                Logger.LogInfo("Not Found " + modFeatMultiplayerGuid);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (wh.GetHasUiOpen())
            {
                return;
            }

            if (toggleAction.WasPressedThisFrame())
            {
                var accessPressed = Managers.GetManager<PlayersManager>()
                    ?.GetActivePlayerController()
                    ?.GetPlayerInputDispatcher()
                    ?.IsPressingAccessibilityKey() ?? false;

                if (accessPressed)
                {
                    showDistanceOnTop.Value = !showDistanceOnTop.Value;
                }
                else
                {
                    var m = displayMode.Value + 1;
                    if (m == 4)
                    {
                        m = 0;
                    }
                    displayMode.Value = m;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineBeaconUpdater), "Start")]
        static void MachineBeaconUpdater_Start(MachineBeaconUpdater __instance, GameObject ___player, 
            GameObject ___canvas, float ___updateEverySec)
        {
            var s = 0.005f;
            var offset = 0.15f;
            var rot = new Vector3(0, 180, 0);

            GameObject title = new GameObject("BeaconTitle");
            title.transform.SetParent(___canvas.transform);
            title.transform.localPosition = new Vector3(0, offset, 0);
            title.transform.localScale = new Vector3(s, s, s);
            title.transform.localEulerAngles = rot;

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
            distance.transform.localEulerAngles = rot;

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
            WorldObject wo = null;
            var woa = __instance.GetComponent<WorldObjectAssociated>();
            if (woa != null)
            {
                wo = woa.GetWorldObject();
                if (wo != null)
                {
                    if (wo.GetText() == null || wo.GetText() == "...")
                    {
                        wo.SetText("");
                    }
                }
            }

            if (hideVanillaLabel.Value)
            {
                var vanillaLabel = __instance.gameObject.transform.Find("Canvas/Image (1)");
                vanillaLabel?.gameObject.SetActive(false);
            }

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

                if (showDistanceOnTop.Value)
                {
                    titleText.text = dist + "m";
                    distanceText.text = wo?.GetText() ?? "";

                    titleText.gameObject.SetActive((displayMode.Value & 1) != 0);
                    distanceText.gameObject.SetActive((displayMode.Value & 2) != 0);
                }
                else
                {
                    distanceText.text = dist + "m";
                    titleText.text = wo?.GetText() ?? "";

                    titleText.gameObject.SetActive((displayMode.Value & 2) != 0);
                    distanceText.gameObject.SetActive((displayMode.Value & 1) != 0);
                }



                yield return new WaitForSeconds(___updateEverySec);
            }
        }
    }
}
