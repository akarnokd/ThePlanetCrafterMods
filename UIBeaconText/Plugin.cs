// Copyright (c) 2022-2026, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using BepInEx.Logging;
using TMPro;

namespace UIBeaconText
{
    [BepInPlugin(modUiBeaconText, "(UI) Beacon Text", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiBeaconText = "akarnokd.theplanetcraftermods.uibeacontext";

        static ConfigEntry<int> fontSize;

        static ConfigEntry<int> displayMode;

        static ConfigEntry<bool> showDistanceOnTop;

        static ConfigEntry<bool> hideVanillaLabel;

        static ConfigEntry<string> displayModeToggle;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<string> fontName;

        static ConfigEntry<bool> hideVanillaHexagon;

        static ConfigEntry<int> combineLabels;

        static ManualLogSource logger;
        static Font font;
        static InputAction toggleAction;


        public void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fontSize = Config.Bind("General", "FontSize", 20, "The font size.");
            displayMode = Config.Bind("General", "DisplayMode", 3, "Display: 0 - no text no distance, 1 - distance only, 2 - text only, 3 - distance + text.");
            displayModeToggle = Config.Bind("General", "DisplayModeToggleKey", "B", "The toggle key for changing the display mode. Shift+<toggle key> to swap top-down. Ctrl+Shift+<toggle key> to toggle hexagon. Alt+<toggle key> to turn all beacons on or off.");
            showDistanceOnTop = Config.Bind("General", "ShowDistanceOnTop", true, "Show the distance above the beacon hexagon if true, below if false");
            hideVanillaLabel = Config.Bind("General", "HideVanillaLabel", true, "If true, the vanilla beacon text is hidden and replaced by this mod's label");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug logging? Chatty!");
            fontName = Config.Bind("General", "Font", "Arial.ttf", "The built-in font name, including its extesion.");
            hideVanillaHexagon = Config.Bind("General", "HideVanillaHexagon", false, "If true, the vanilla hexagon is hidden. Toggle via Ctrl+Shift+<toggle key>.");
            combineLabels = Config.Bind("General", "CombineLabels", 0, "0 - separate labels, 1 - combined labels above the hexagon, 2 - combined labels below the hexagon. Configure ordering via ShowDistanceOnTop");

            UpdateKeyBindings();

            font = Resources.GetBuiltinResource<Font>(fontName.Value);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        public void Update()
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

                var shiftPressed = Keyboard.current[Key.LeftShift].IsPressed() || Keyboard.current[Key.RightShift].IsPressed();

                var altPressed = Keyboard.current[Key.LeftAlt].IsPressed() || Keyboard.current[Key.RightAlt].IsPressed();

                var hh = Managers.GetManager<BaseHudHandler>();

                if (accessPressed && shiftPressed)
                {
                    hideVanillaHexagon.Value = !hideVanillaHexagon.Value;
                    hh?.DisplayCursorText("", 3f, "BeaconText: Hexagon " + (hideVanillaHexagon.Value ? "Hidden" : "Visible"));
                }
                else if (accessPressed)
                {
                    showDistanceOnTop.Value = !showDistanceOnTop.Value;
                    hh?.DisplayCursorText("", 3f, "BeaconText: Distance " + (showDistanceOnTop.Value ? "On Top" : "On Bottom"));
                }
                else if (altPressed)
                {
                    if (shiftPressed)
                    {
                        var c = combineLabels.Value + 1;
                        if (c == 3)
                        {
                            c = 0;
                        }
                        combineLabels.Value = c;
                        hh?.DisplayCursorText("", 3f, "BeaconText: Combining labels " + (c switch
                        {
                            0 => "OFF",
                            1 => "ON TOP",
                            2 => "ON BOTTOM",
                            _ => "???"
                        }));
                    }
                    else
                    {
                        MachineBeaconUpdater.HideBeacons = !MachineBeaconUpdater.HideBeacons;
                        hh?.DisplayCursorText("", 3f, "BeaconText: All beacons are now " + (MachineBeaconUpdater.HideBeacons ? "OFF" : "ON"));
                    }
                }
                else
                {
                    var m = displayMode.Value + 1;
                    if (m == 4)
                    {
                        m = 0;
                    }
                    displayMode.Value = m;


                    hh?.DisplayCursorText("", 3f, "BeaconText: Display Mode " + (
                        m switch
                        {
                            0 => "Off",
                            1 => "Distance only",
                            2 => "Title only",
                            3 => "Title + Distance",
                            _ => "???"
                        }
                    ));
                }
            }
        }

        const float defaultScale = 0.005f;
        const float defaultOffset = 0.15f;
        static readonly Vector3 defaultRotation = new(0, 0, 0);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineBeaconUpdater), "Start")]
        static void MachineBeaconUpdater_Start(
            MachineBeaconUpdater __instance, 
            GameObject ___canvas)
        {
            var vanillaLabel = ___canvas.GetComponentInChildren<TextMeshProUGUI>();


            var title = new GameObject("BeaconTitle");
            title.transform.SetParent(___canvas.transform, false);
            title.transform.localPosition = new Vector3(0, defaultOffset, 0);
            title.transform.localScale = new Vector3(defaultScale, defaultScale, defaultScale);
            title.transform.localEulerAngles = defaultRotation;

            var titleText = title.AddComponent<Text>();
            
            titleText.font = font;
            titleText.text = "...";
            titleText.color = Color.white;
            titleText.fontSize = fontSize.Value;
            titleText.resizeTextForBestFit = false;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.supportRichText = true;

            var distance = new GameObject("BeaconDistance");
            distance.transform.SetParent(___canvas.transform, false);
            distance.transform.localPosition = new Vector3(0, -defaultOffset, 0);
            distance.transform.localScale = new Vector3(defaultScale, defaultScale, defaultScale);
            distance.transform.localEulerAngles = defaultRotation;

            var distanceText = distance.AddComponent<Text>(); 
            distanceText.font = font;
            distanceText.text = "...";
            distanceText.color = Color.white;
            distanceText.fontSize = fontSize.Value;
            distanceText.resizeTextForBestFit = false;
            distanceText.verticalOverflow = VerticalWrapMode.Overflow;
            distanceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            distanceText.alignment = TextAnchor.MiddleCenter;
            distanceText.supportRichText = true;

            Log("Finding the World Object of the beacon");
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
            var tp = wo == null ? __instance.GetComponent<TextProxy>() : null;

            Log("Starting updater");

            var holder = ___canvas.AddComponent<BeaconTextHolder>();
            holder.titleText = titleText;
            holder.distanceText = distanceText;
            holder.beaconWorldObject = wo;
            holder.textProxy = tp;
            holder.vanillaLabel = vanillaLabel;
            holder.vanillaHexagon1 = ___canvas.transform.Find("Image")?.gameObject;
            holder.vanillaHexagon2 = ___canvas.transform.Find("Image (1)")?.gameObject;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineBeaconUpdater), "LateUpdate")]
        static void MachineBeaconUpdater_LateUpdate(
            MachineBeaconUpdater __instance, 
            GameObject ___canvas
        )
        {
            PlayersManager pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var player = pm.GetActivePlayerController();
            if (player == null)
            {
                return;
            }
            if (__instance == null)
            {
                return;
            }
            var holder = ___canvas.GetComponent<BeaconTextHolder>();

            var beaconPos = __instance.transform.position;
            var dist = (int)Vector3.Distance(beaconPos, player.transform.position);

            var titleText = holder.titleText;
            var distanceText = holder.distanceText;

            var textValue = "";
            if (holder.beaconWorldObject != null)
            {
                textValue = holder.beaconWorldObject.GetText();
            }
            else if (holder.textProxy != null)
            {
                textValue = holder.textProxy.GetText();
            }

            titleText.gameObject.transform.localPosition = new Vector3(0, defaultOffset, 0);
            distanceText.gameObject.transform.localPosition = new Vector3(0, -defaultOffset, 0);

            if (combineLabels.Value == 1)
            {
                if (showDistanceOnTop.Value)
                {
                    titleText.text = dist + "m\n" + textValue;
                }
                else
                {
                    titleText.text = textValue + "\n" + dist + "m";
                }

                titleText.gameObject.SetActive(displayMode.Value != 0);
                distanceText.gameObject.SetActive(false);
                titleText.gameObject.transform.localPosition = new Vector3(0, defaultOffset * 1.5f, 0);
                distanceText.gameObject.transform.localPosition = new Vector3(0, -defaultOffset * 1.5f, 0);
            }
            else
            if (combineLabels.Value == 2)
            {
                if (showDistanceOnTop.Value)
                {
                    distanceText.text = dist + "m\n" + textValue;
                }
                else
                {
                    distanceText.text = textValue + "\n" + dist + "m";
                }

                distanceText.gameObject.SetActive(displayMode.Value != 0);
                titleText.gameObject.SetActive(false);
                titleText.gameObject.transform.localPosition = new Vector3(0, defaultOffset * 1.5f, 0);
                distanceText.gameObject.transform.localPosition = new Vector3(0, -defaultOffset * 1.5f, 0);
            }
            else
            if (showDistanceOnTop.Value)
            {
                titleText.text = dist + "m";
                distanceText.text = textValue;

                titleText.gameObject.SetActive((displayMode.Value & 1) != 0);
                distanceText.gameObject.SetActive((displayMode.Value & 2) != 0);
            }
            else
            {
                distanceText.text = dist + "m";
                titleText.text = textValue;

                titleText.gameObject.SetActive((displayMode.Value & 2) != 0);
                distanceText.gameObject.SetActive((displayMode.Value & 1) != 0);
            }
            holder.vanillaLabel?.gameObject.SetActive(!hideVanillaLabel.Value);
            holder.vanillaHexagon1?.SetActive(!hideVanillaHexagon.Value);
            holder.vanillaHexagon2?.SetActive(!hideVanillaHexagon.Value);
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == "BeaconTitle" || go.name == "BeaconDistance")
                {
                    var txt = go.GetComponent<Text>();
                    if (txt != null)
                    {
                        txt.fontSize = fontSize.Value;
                    }
                }
            }
            UpdateKeyBindings();
        }

        static void UpdateKeyBindings()
        {
            if (!displayModeToggle.Value.StartsWith("<"))
            {
                displayModeToggle.Value = "<Keyboard>/" + displayModeToggle.Value;
            }
            toggleAction = new InputAction("DisplayModeToggleKey", binding: displayModeToggle.Value);
            toggleAction.Enable();
        }

        internal class BeaconTextHolder : MonoBehaviour
        {
            internal Text titleText;
            internal Text distanceText;
            internal WorldObject beaconWorldObject;
            internal TextProxy textProxy;
            internal TextMeshProUGUI vanillaLabel;
            internal GameObject vanillaHexagon1;
            internal GameObject vanillaHexagon2;
        }
    }
}
