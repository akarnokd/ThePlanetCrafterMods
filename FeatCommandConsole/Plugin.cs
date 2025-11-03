// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FeatCommandConsole
{
    // Credits to Aedenthorn's Spawn Object mod, used it as a guide to create an in-game interactive window
    // because so far, I only did overlays or modified existing windows
    // https://github.com/aedenthorn/PlanetCrafterMods/blob/master/SpawnObject/BepInExPlugin.cs

    [BepInPlugin(modFeatCommandConsole, "(Feat) Command Console", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatCommandConsole = "akarnokd.theplanetcraftermods.featcommandconsole";
        const string modCheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;
        static ConfigEntry<string> toggleKey;
        static ConfigEntry<string> toggleKeyController;
        static ConfigEntry<int> consoleTop;
        static ConfigEntry<int> consoleLeft;
        static ConfigEntry<int> consoleRight;
        static ConfigEntry<int> consoleBottom;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<string> fontName;
        static ConfigEntry<float> transparency;
        static ConfigEntry<string> onLoadCommand;
        static ConfigEntry<bool> unstuckDirectional;
        static ConfigEntry<float> unstuckDistance;

        static GameObject canvas;
        static GameObject background;
        static GameObject separator;
        static GameObject inputField;
        static TMP_InputField inputFieldText;
        static readonly List<GameObject> outputFieldLines = [];
        static readonly List<string> consoleText = [];
        static TMP_FontAsset fontAsset;
        static int fontMargin = 5;

        static int scrollOffset;
        static int commandHistoryIndex;
        static readonly List<string> commandHistory = [];

        static InputAction toggleAction;
        static InputAction toggleActionController;

        static readonly Dictionary<string, CommandRegistryEntry> commandRegistry = [];

        static Dictionary<string, Vector3> savedTeleportLocations;

        const int similarLimit = 3;

        static IEnumerator autorefillCoroutine;

        static readonly float defaultTradePlatformDelay = 6;
        static float tradePlatformDelay = defaultTradePlatformDelay;

        static readonly float defaultOutsideGrowerDelay = 1;
        static float outsideGrowerDelay = defaultOutsideGrowerDelay;

        static bool suppressCommandConsoleKey;

        static Plugin me;

        static Func<Inventory, int> GetInventoryCapacity;

        static Func<Inventory, string, bool> InventoryCanAdd;

        static int worldLoadCount;

        static float noClipBaseSpeed;

        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
        // API
        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

        public static IDisposable RegisterCommand(string name, string description, Func<List<string>, List<string>> action)
        {
            if (commandRegistry.TryGetValue(name, out var cre))
            {
                if (cre.standard)
                {
                    throw new InvalidOperationException("Redefining standard commands is not allowed: " + name);
                }
            }
            commandRegistry[name] = new CommandRegistryEntry
            {
                description = description,
                method = (list =>
                {
                    consoleText.AddRange(action(list));
                }),
                standard = false
            };
            return new RemoveCommandRegistry(name);
        }

        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            me = this;

            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable the detailed logging of this mod");
            toggleKey = Config.Bind("General", "ToggleKey", "<Keyboard>/enter", "Key to open the console");
            toggleKeyController = Config.Bind("General", "ToggleKeyController", "<Gamepad>/rightStickPress", "Controller action to open the console");

            consoleTop = Config.Bind("General", "ConsoleTop", 200, "Console window's position relative to the top of the screen.");
            consoleLeft = Config.Bind("General", "ConsoleLeft", 300, "Console window's position relative to the left of the screen.");
            consoleRight = Config.Bind("General", "ConsoleRight", 200, "Console window's position relative to the right of the screen.");
            consoleBottom = Config.Bind("General", "ConsoleBottom", 200, "Console window's position relative to the bottom of the screen.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size in the console");
            fontName = Config.Bind("General", "FontName", "arial.ttf", "The font name in the console");
            transparency = Config.Bind("General", "Transparency", 0.98f, "How transparent the console background should be (0..1).");
            onLoadCommand = Config.Bind("General", "OnLoadCommand", "", "The command to execute after the player loads into a world. Use a # prefix to 'comment out' the command.");

            unstuckDirectional = Config.Bind("General", "UnstuckDirectional", false, "If true, pressing F4 will teleport up, Shift+F4 will teleport down, Ctrl+F4 will teleport forward, each the distance in UnstuckDistance amount.");
            unstuckDistance = Config.Bind("General", "UnstuckDistance", 2.5f, "The distance to teleport when using F4, Shift+F4 and Ctrl+F4");

            UpdateKeyBindings();

            Log("   Get resource");
            Font osFont = null;

            var fn = fontName.Value.ToLower(CultureInfo.InvariantCulture);

            foreach (var fp in Font.GetPathsToOSFonts())
            {
                if (fp.ToLower(CultureInfo.InvariantCulture).Contains(fn))
                {
                    osFont = new Font(fp);
                    Log("      Found font at " + fp);
                    break;
                }
            }

            Log("   Set asset");
            try
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
            } 
            catch (Exception)
            {
                Log("Setting custom font failed, using default game font");
            }

            CreateWelcomeText();

            Log("Setting up command registry");
            foreach (MethodInfo mi in typeof(Plugin).GetMethods())
            {
                var ca = mi.GetCustomAttribute<Command>();
                if (ca != null)
                {
                    commandRegistry[ca.Name] = new CommandRegistryEntry
                    {
                        description = ca.Description,
                        method = (list => mi.Invoke(this, [list])),
                        standard = true
                    };

                    Log("  " + ca.Name + " - " + ca.Description);
                }
            }

            InventoryCanAdd = (inv, gid) => !inv.IsFull();

            if (Chainloader.PluginInfos.TryGetValue(modCheatInventoryStackingGuid, out var info))
            {
                Logger.LogInfo("Mod " + modCheatInventoryStackingGuid + " found, using it's services.");

                var modType = info.Instance.GetType();

                var apiIsFullStackedInventory = (Func<Inventory, string, bool>)AccessTools.Field(modType, "apiIsFullStackedInventory").GetValue(null);
                // we need to logically invert it as we need it as "can-do"
                InventoryCanAdd = (inv, gid) => !apiIsFullStackedInventory(inv, gid);

                GetInventoryCapacity = (Func<Inventory, int>)AccessTools.Field(modType, "apiGetCapacityInventory").GetValue(null);
            }
            else
            {
                Logger.LogInfo("Mod " + modCheatInventoryStackingGuid + " not found.");
            }

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(h, modFeatCommandConsole, _ => PlanetLoader_HandleDataAfterLoad());
            LibCommon.GameVersionCheck.Patch(h, "(Feat) Command Console - v" + PluginInfo.PLUGIN_VERSION);
        }

        static void Log(object o)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(o);
            }
        }

        static void UpdateKeyBindings()
        {
            if (!toggleKey.Value.Contains("<"))
            {
                toggleKey.Value = "<Keyboard>/" + toggleKey.Value;
            }
            toggleAction = new InputAction(name: "Open console", binding: toggleKey.Value);
            toggleAction.Enable();

            if (!toggleKeyController.Value.Contains("<"))
            {
                toggleKeyController.Value = "<Gamepad>/" + toggleKey.Value;
            }
            toggleActionController = new InputAction(name: "Open console via controller", binding: toggleKeyController.Value);
            toggleActionController.Enable();
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
        }

        static void CreateWelcomeText()
        {
            var ver = typeof(Plugin).GetCustomAttribute<BepInPlugin>().Version;
            consoleText.Add("Welcome to <b>Command Console</b> version <color=#00FF00>" + ver + "</color>.");
            consoleText.Add("<margin=1em>Type in <b><color=#FFFF00>/help</color></b> to list the available commands.");
            consoleText.Add("<margin=1em><i>Use the <b><color=#FFFFFF>Up/Down Arrow/D-Pad</color></b> to cycle command history.</i>");
            consoleText.Add("<margin=1em><i>Use the <b><color=#FFFFFF>Mouse Wheel or Left Stick Up/Down</color></b> to scroll up/down the output.</i>");
            consoleText.Add("<margin=1em><i>Use the <b><color=#FFFFFF>ESC or Gamepad B</color></b> close the dialog.</i>");
            consoleText.Add("<margin=1em><i>Start typing <color=#FFFF00>/</color> and press <b><color=#FFFFFF>TAB or Gamepad Y</color></b> to see commands starting with those letters.</i>");
            consoleText.Add("");
        }

        void DestroyConsoleGUI()
        {
            Log("DestroyConsoleGUI");

            Destroy(background);
            Destroy(separator);
            Destroy(inputField);
            foreach (var go in outputFieldLines)
            {
                Destroy(go);
            }
            outputFieldLines.Clear();
            Destroy(canvas);

            background = null;
            inputField = null;
            inputFieldText = null;
            canvas = null;
            separator = null;
        }

        void Update()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (!wh.GetHasUiOpen() && background != null)
            {
                Log("No UI should be open, closing GUI");
                DestroyConsoleGUI();
                return;
            }

            if (!modEnabled.Value)
            {
                return;
            }
            if (wh.GetHasUiOpen() && background != null)
            {
                if (Keyboard.current[Key.Escape].wasPressedThisFrame
                    || (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null && Gamepad.current.bButton.wasPressedThisFrame))
                {
                    Log("Escape pressed. Closing GUI");
                    DestroyConsoleGUI();
                    wh.CloseAllWindows();
                }
                else if (Keyboard.current[Key.Enter].wasPressedThisFrame || Keyboard.current[Key.NumpadEnter].wasPressedThisFrame)
                {
                    ExecuteConsoleCommand(inputFieldText?.text);
                    /*
                    if (!Keyboard.current[Key.LeftShift].isPressed && !Keyboard.current[Key.RightShift].isPressed)
                    {
                        log("Command executed, Closing GUI");
                        DestroyConsoleGUI();
                        wh.CloseAllWindows();
                    }
                    */
                }
                if (Mouse.current.leftButton.wasPressedThisFrame 
                    || (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null && Gamepad.current.aButton.wasPressedThisFrame))
                {
                    inputFieldText.ActivateInputField();
                }
                var ms = Mouse.current.scroll.ReadValue();
                if (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null)
                {
                    if (Gamepad.current.leftStick.up.wasPressedThisFrame)
                    {
                        ms = new(0, 1);
                    }
                    else if (Gamepad.current.leftStick.down.wasPressedThisFrame)
                    {
                        ms = new(0, -1);
                    }
                }
                if (ms.y != 0)
                {
                    Log(" Scrolling " + ms.y);
                    int delta = 1;
                    if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
                    {
                        delta = 3;
                    }
                    if (ms.y > 0)
                    {
                        scrollOffset = Math.Min(consoleText.Count - 1, scrollOffset + delta);
                    }
                    else
                    {
                        scrollOffset = Math.Max(0, scrollOffset - delta);
                    }
                    CreateOutputLines();
                }
                bool prevHistoryPressed = Keyboard.current[Key.UpArrow].wasPressedThisFrame
                    || (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null 
                    && Gamepad.current.dpad.up.wasPressedThisFrame);
                if (prevHistoryPressed)
                {
                    Log("UpArrow, commandHistoryIndex = " + commandHistoryIndex + ", commandHistory.Count = " + commandHistory.Count);
                    if (commandHistoryIndex < commandHistory.Count)
                    {
                        commandHistoryIndex++;
                        inputFieldText.text = commandHistory[^commandHistoryIndex];
                        inputFieldText.ActivateInputField();
                        inputFieldText.caretPosition = inputFieldText.text.Length;
                        inputFieldText.stringPosition = inputFieldText.text.Length;
                    }
                }
                bool nextHistoryPressed = Keyboard.current[Key.DownArrow].wasPressedThisFrame
                    || (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null 
                    && Gamepad.current.dpad.down.wasPressedThisFrame);
                if (nextHistoryPressed)
                {
                    Log("DownArrow, commandHistoryIndex = " + commandHistoryIndex + ", commandHistory.Count = " + commandHistory.Count);
                    commandHistoryIndex = Math.Max(0, commandHistoryIndex - 1);
                    if (commandHistoryIndex > 0)
                    {
                        inputFieldText.text = commandHistory[^commandHistoryIndex];
                    }
                    else
                    {
                        inputFieldText.text = "";
                    }
                    inputFieldText.ActivateInputField();
                    inputFieldText.caretPosition = inputFieldText.text.Length;
                    inputFieldText.stringPosition = inputFieldText.text.Length;
                }
                bool suggestionPressed = Keyboard.current[Key.Tab].wasPressedThisFrame
                    || (GamepadConfig.Instance.GetIsUsingController() && Gamepad.current != null && Gamepad.current.yButton.wasPressedThisFrame);
                if (suggestionPressed)
                {
                    List<string> list = [];
                    foreach (var k in commandRegistry.Keys)
                    {
                        if (k.StartsWith(inputFieldText.text))
                        {
                            list.Add(k);
                        }
                    }
                    if (list.Count != 0)
                    {
                        if (list.Count == 1)
                        {
                            inputFieldText.text = list[0];
                        }
                        else
                        {
                            list.Sort();
                            Colorize(list, "#FFFF00");
                            foreach (var k in JoinPerLine(list, 10))
                            {
                                AddLine("<margin=2em>" + k);
                            }
                            CreateOutputLines();
                        }
                    }
                    inputFieldText.ActivateInputField();
                    inputFieldText.caretPosition = inputFieldText.text.Length;
                }
                return;
            }

            bool toogleActionWasPressed = toggleAction.WasPressedThisFrame()
                || (GamepadConfig.Instance.GetIsUsingController() && toggleActionController.WasPressedThisFrame());

            if (wh.GetHasUiOpen() && toogleActionWasPressed && background == null)
            {
                return;
            }
            if (!toogleActionWasPressed || background != null)
            {
                return;
            }
            if (suppressCommandConsoleKey)
            {
                return;
            }

            Log("GetHasUiOpen: " + wh.GetHasUiOpen() + ", Background null? " + (background == null));

            canvas = new GameObject("CommandConsoleCanvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;

            Log("Creating the background");

            RecreateBackground(wh);
            Log("Done");
        }

        void RecreateBackground(WindowsHandler wh)
        {
            int panelWidth = Screen.width - consoleLeft.Value - consoleRight.Value;
            int panelHeight = Screen.height - consoleTop.Value - consoleBottom.Value;

            int panelX = -Screen.width / 2 + consoleLeft.Value + panelWidth / 2;
            int panelY = Screen.height / 2 - consoleTop.Value - panelHeight / 2;

            RectTransform rect;

            Destroy(background);
            background = new GameObject("CommandConsoleBackground");
            background.transform.parent = canvas.transform;
            var img = background.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, transparency.Value);
            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(panelX, panelY, 0); // new Vector3(-Screen.width / 2 + consoleLeft.Value, Screen.height / 2 - consoleTop.Value, 0);
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);

            separator = new GameObject("CommandConsoleSeparator");
            separator.transform.parent = background.transform;
            img = separator.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -panelHeight / 2 + (fontMargin + fontSize.Value), 0);
            rect.sizeDelta = new Vector2(panelWidth - 10, 2);

            // ---------------------------------------------

            Log("Creating the text field");
            inputField = new GameObject("CommandConsoleInput");
            inputField.transform.parent = background.transform;

            Log("   Create TMP_InputField");
            inputFieldText = inputField.AddComponent<TMP_InputField>();
            Log("   Create TextMeshProUGUI");
            var txt = inputField.AddComponent<TextMeshProUGUI>();
            inputFieldText.textComponent = txt;

            Log("   Set asset");
            inputFieldText.fontAsset = fontAsset;
            Log("   Set set pointSize");
            inputFieldText.pointSize = fontSize.Value;
            Log("   Set text");
            //inputFieldText.text = "example...";
            inputFieldText.caretColor = Color.white;
            inputFieldText.selectionColor = Color.gray;
            inputFieldText.onFocusSelectAll = false;

            Log("   Set position");
            rect = inputField.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -panelHeight / 2 + (fontMargin + fontSize.Value) / 2, 0);
            rect.sizeDelta = new Vector2(panelWidth - 10, fontMargin + fontSize.Value);

            CreateOutputLines();

            Log("Patch in the custom text window");
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "_openedUi") = DataConfig.UiType.TextInput;

            Log("Activating the field");
            inputFieldText.enabled = false;
            inputFieldText.enabled = true;

            inputFieldText.Select();
            inputFieldText.ActivateInputField();
        }

        void CreateOutputLines()
        {
            if (background == null)
            {
                return;
            }
            int panelWidth = Screen.width - consoleLeft.Value - consoleRight.Value;
            int panelHeight = Screen.height - consoleTop.Value - consoleBottom.Value;

            // Clear previous lines
            foreach (var go in outputFieldLines)
            {
                Destroy(go);
            }
            outputFieldLines.Clear();

            Log("Set output lines");
            int outputY = -panelHeight / 2 + (fontMargin + fontSize.Value) * 3 / 2;

            int j = 0;
            for (int i = consoleText.Count - scrollOffset - 1; i >= 0; i--)
            {
                string line = consoleText[i];
                if (outputY > panelHeight / 2)
                {
                    break;
                }

                var outputField = new GameObject("CommandConsoleOutputLine-" + j);
                outputField.transform.parent = background.transform;

                var outputFieldText = outputField.AddComponent<TextMeshProUGUI>();
                outputFieldText.font = fontAsset;
                outputFieldText.fontSize = fontSize.Value;
                outputFieldText.richText = true;
                outputFieldText.textWrappingMode = TextWrappingModes.NoWrap;
                outputFieldText.text = line;

                var rect = outputFieldText.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(0, outputY, 0);
                rect.sizeDelta = new Vector2(panelWidth - 10, fontMargin + fontSize.Value);

                outputFieldLines.Add(outputField);

                j++;
                outputY += (fontMargin + fontSize.Value);
            }
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            var onLoadCommandText = onLoadCommand.Value;
            if (++worldLoadCount == 1
                && !string.IsNullOrWhiteSpace(onLoadCommandText) 
                && !onLoadCommandText.StartsWith("#"))
            {
                AddLine("<color=#FFFF00>Executing OnLoadCommand");
                me.ExecuteConsoleCommand(onLoadCommandText);
            }
        }

        void ExecuteConsoleCommand(string text)
        {
            Log("Debug executing command: " + text);
            text = text.Trim();
            if (text.Length == 0)
            {
                return;
            }
            consoleText.Add("<color=#FFFF00><noparse>" + text.Replace("</noparse>", "") + "</noparse></color>");
            commandHistory.Add(text);
            commandHistoryIndex = 0;

            var commands = ParseConsoleCommand(text);
            if (commands.Count != 0 && commands[0].StartsWith("/"))
            {
                if (commandRegistry.TryGetValue(commands[0], out var action))
                {
                    try
                    {
                        action.method(commands);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex);
                        foreach (var el in ex.ToString().Split('\n'))
                        {
                            AddLine("<margin=1em><color=#FF8080>" + el);
                        }
                    }
                }
                else
                {
                    AddLine("<color=#FF0000>Unknown command</color>");
                }

            }
            else
            {
                ChatHandler.Instance?.SendChatMessage(text);
            }

            scrollOffset = 0;
            CreateOutputLines();

            if (background == null)
            {
                return;
            }

            inputFieldText.text = "";
            inputFieldText.Select();
            inputFieldText.ActivateInputField();
        }

        List<string> ParseConsoleCommand(string text)
        {
            return [.. Regex.Split(text, "\\s+")];
        }

        static void AddLine(string line)
        {
            consoleText.Add(line);
        }

        // oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
        // command methods
        // oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo

        [Command("/help", "Displays the list of commands or their own descriptions.")]
        public void Help(List<string> args)
        {
            if (args.Count == 1)
            {
                HelpListCommands();
            }
            else
            {
                if (args[1] == "*")
                {
                    AddLine("<margin=1em>Available commands:");
                    var list = new List<string>();
                    foreach (var kv in commandRegistry)
                    {
                        list.Add(kv.Key);
                    }
                    list.Sort();

                    foreach (var cmd in list)
                    {
                        var reg = commandRegistry[cmd];
                        AddLine("<margin=2em><color=#FFFF00>" + cmd + "</color> - " + reg.description);
                    }
                }
                else
                {
                    commandRegistry.TryGetValue(args[1], out var cmd);
                    if (cmd == null)
                    {
                        commandRegistry.TryGetValue("/" + args[1], out cmd);
                    }
                    if (cmd != null)
                    {
                        Log("Help for " + args[1] + " - " + cmd.description);
                        AddLine("<margin=1em>" + cmd.description);
                    }
                    else
                    {
                        AddLine("<margin=1em><color=#FFFF00>Unknown command");
                    }
                }
            }
        }

        void HelpListCommands()
        {
            AddLine("<margin=1em>Type <b><color=#FFFF00>/help [command]</color></b> to get specific command info.");
            AddLine("<margin=1em>Type <b><color=#FFFF00>/help *</color></b> to list all commands with their description.");
            AddLine("<margin=1em>Available commands:");
            var list = new List<string>();
            foreach (var kv in commandRegistry)
            {
                list.Add(kv.Key);
            }
            list.Sort();
            Colorize(list, "#FFFF00");
            Bolden(list);
            foreach (var line in JoinPerLine(list, 10))
            {
                AddLine("<margin=2em>" + line);
            }
        }

        [Command("/clear", "Clears the console history.")]
        public void Clear(List<string> _)
        {
            consoleText.Clear();
        }

        [Command("/spawn", "Spawns an item and adds them to the player inventory.")]
        public void Spawn(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Spawn item(s) or list items that can be possibly spawn");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/spawn list [name-prefix]</color> - list the item ids that can be spawn");
                AddLine("<margin=2em><color=#FFFF00>/spawn basic [amount]</color> - Spawn some food, water, oxygen and beginner materials");
                AddLine("<margin=2em><color=#FFFF00>/spawn advanced [amount]</color> - Spawn the best equipment");
                AddLine("<margin=2em><color=#FFFF00>/spawn itemid [amount]</color> - spawn the given item by the given amount");
            } else
            {
                if (args[1] == "list")
                {
                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> possibleSpawns = [];
                    foreach (var g in GroupsHandler.GetAllGroups())
                    {
                        if (g is GroupItem gi && gi.GetId().ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            possibleSpawns.Add(gi.GetId());
                        }
                    }
                    possibleSpawns.Sort();
                    Colorize(possibleSpawns, "#00FF00");
                    foreach (var line in JoinPerLine(possibleSpawns, 5))
                    {
                        AddLine("<margin=1em>" + line);
                    }
                }
                else if (args[1] == "basic")
                {
                    string[] resources =
                    [
                        "astrofood", "WaterBottle1", "OxygenCapsule1", "Iron", "Cobalt", "Titanium", "Magnesium", "Silicon", "Aluminium"
                    ];
                    int amount = 10;
                    if (args.Count > 2)
                    {
                        amount = int.Parse(args[2]);
                    }

                    SpawnInItems(amount, resources);
                }
                else if (args[1] == "advanced")
                {
                    string[] resources =
                    [
                        "MultiToolLight3", "MultiToolDeconstruct3", "Backpack7", 
                        "Jetpack4", "BootsSpeed3", "MultiBuild", "MultiToolMineSpeed4",
                        "EquipmentIncrease4", "OxygenTank5", "HudCompass"
                    ];
                    int amount = 1;
                    if (args.Count > 2)
                    {
                        amount = int.Parse(args[2]);
                    }

                    SpawnInItems(amount, resources);
                }
                else
                {
                    int count = 1;
                    if (args.Count > 2)
                    {
                        try
                        {
                            count = int.Parse(args[2]);
                        }
                        catch (Exception)
                        {

                        }
                    }

                    var gid = args[1].ToLowerInvariant();
                    SpaceCraft.Group g = FindGroup(gid);

                    if (g == null)
                    {
                        DidYouMean(gid, false, true);
                    }
                    else if (g is not GroupItem)
                    {
                        AddLine("<margin=1em><color=#FF0000>This item can't be spawned.");
                        if (g is GroupConstructible)
                        {
                            AddLine("<margin=1em>Use <color=#FFFF00>/build " + g.id + "</color> instead.");
                        }
                    }
                    else if (g.id == "DNASequence")
                    {
                        AddLine("<margin=1em><color=#FF0000>This item can't be spawned with /spawn.");
                        AddLine("<margin=1em>Use <color=#FFFF00>/spawn-dna</color> for this type of item.");
                    }
                    else
                    {
                        var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                        var inv = pm.GetPlayerBackpack().GetInventory();
                        int added = 0;
                        for (int i = 0; i < count; i++)
                        {
                            if (TryAddToInventory(inv, g))
                            {
                                added++;
                            }
                        }

                        if (added == count)
                        {
                            AddLine("<margin=1em>Items added");
                        }
                        else if (added > 0)
                        {
                            AddLine("<margin=1em>Some items added (" + added + "). Inventory full.");
                        }
                        else
                        {
                            AddLine("<margin=1em>Inventory full.");
                        }
                    }
                }
            }
        }

        bool TryAddToInventory(Inventory inventory, SpaceCraft.Group gr)
        {
            if (InventoryCanAdd(inventory, gr.id))
            {
                InventoriesHandler.Instance.AddItemToInventory(gr, inventory, (success, id) =>
                {
                    if (!success && id != 0)
                    {
                        WorldObjectsHandler.Instance.DestroyWorldObject(id);
                    }
                });
                return true;
            }
            return false;
        }

        void SpawnInItems(int amount, string[] resources)
        {
            int added = 0;
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var inv = pm.GetPlayerBackpack().GetInventory();

            for (int i = 0; i < amount; i++)
            {
                foreach (var gid in resources)
                {
                    var gr = GroupsHandler.GetGroupViaId(gid);
                    if (gr != null)
                    {
                        if (TryAddToInventory(inv, gr))
                        {
                            added++;
                        }
                    }
                    else
                    {
                        AddLine("<margin=1em><color=red>Unknown item " + gid);
                    }
                }
            }

            int count = amount * resources.Length;
            if (added == count)
            {
                AddLine("<margin=1em>Items added");
            }
            else if (added > 0)
            {
                AddLine("<margin=1em>Some items added (" + added + "). Inventory full.");
            }
            else
            {
                AddLine("<margin=1em>Inventory full.");
            }
        }

        [Command("/tp", "Teleport to a user-named location or an x, y, z position.")]
        public void Teleport(List<string> args)
        {
            if (args.Count != 2 && args.Count != 4)
            {
                AddLine("<margin=1em>Teleport to a user-named location or an x, y, z position");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tp location-name</color> - teleport to location-name");
                AddLine("<margin=2em><color=#FFFF00>/tp x y z</color> - teleport to a specific coordinate");
                AddLine("<margin=2em><color=#FFFF00>/tp x:y:z</color> - teleport to a specific coordinate described by the colon format");
                AddLine("<margin=1em>See also <color=#FFFF00>/tp-create</color>, <color=#FFFF00>/tp-list</color>, <color=#FFFF00>/tp-remove</color>.");
            }
            else
            if (args.Count == 2)
            {
                var m = Regex.Match(args[1], "([-+]?[0-9]*\\.?[0-9]*):([-+]?[0-9]*\\.?[0-9]*):([-+]?[0-9]*\\.?[0-9]*)");
                if (m.Success)
                {
                    var x = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    var y = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    var z = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);

                    var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                    pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);

                    AddLine("<margin=1em>Teleported to: ( "
                        + m.Groups[1].Value
                        + ", " + m.Groups[2].Value
                        + ", " + m.Groups[3].Value
                        + " )"
                    );
                }
                else
                if (TryGetSavedTeleportLocation(args[1], out var pos))
                {
                    var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                    pm.SetPlayerPlacement(pos, pm.transform.rotation);

                    AddLine("<margin=1em>Teleported to: <color=#00FF00>" + args[1] + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown location.");
                    AddLine("<margin=1em>Use <b><color=#FFFF00>/tp-list</color></b> to get all known named locations.");
                }
            }
            else
            {
                var x = float.Parse(args[1], CultureInfo.InvariantCulture);
                var y = float.Parse(args[2], CultureInfo.InvariantCulture);
                var z = float.Parse(args[3], CultureInfo.InvariantCulture);

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);

                AddLine("<margin=1em>Teleported to: ( "
                    + args[1]
                    + ", " + args[2]
                    + ", " + args[3]
                    + " )"
                );
            }
        }

        [Command("/tpr", "Teleport relative to the current location.")]
        public void TeleportRelative(List<string> args)
        {
            if (args.Count != 2 && args.Count != 4)
            {
                AddLine("<margin=1em>Teleport relative to the current location");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tpr x y z</color> - teleport with the specified deltas");
                AddLine("<margin=2em><color=#FFFF00>/tpr x:y:z</color> - teleport with the specified deltas described by the colon format");
            }
            else
            if (args.Count == 2)
            {
                var m = Regex.Match(args[1], "([-+]?[0-9]*\\.?[0-9]*):([-+]?[0-9]*\\.?[0-9]*):([-+]?[0-9]*\\.?[0-9]*)");
                if (m.Success)
                {

                    var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                    var x = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + pm.transform.position.x;
                    var y = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + pm.transform.position.y;
                    var z = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) + pm.transform.position.z;

                    pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);

                    AddLine("<margin=1em>Teleported to: ( "
                        + x.ToString(CultureInfo.InvariantCulture)
                        + ", " + y.ToString(CultureInfo.InvariantCulture)
                        + ", " + z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Invalid relative offset(s).");
                }
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                var x = float.Parse(args[1], CultureInfo.InvariantCulture) + pm.transform.position.x;
                var y = float.Parse(args[2], CultureInfo.InvariantCulture) + pm.transform.position.y;
                var z = float.Parse(args[3], CultureInfo.InvariantCulture) + pm.transform.position.z;

                pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);

                AddLine("<margin=1em>Teleported to: ( "
                    + x.ToString(CultureInfo.InvariantCulture)
                    + ", " + y.ToString(CultureInfo.InvariantCulture)
                    + ", " + z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
            }
        }

        [Command("/tpf", "Teleport forward or backward where the camera is facing.")]
        public void TeleportForward(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Teleport forward or backward where the camera is facing");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tpf distance</color> - teleport by a positive or negative distance");
            }
            else
            if (args.Count == 2)
            {
                var dist = float.Parse(args[1], CultureInfo.InvariantCulture);
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                pm.SetPlayerPlacement(pm.transform.position + Camera.main.transform.forward * dist, pm.transform.rotation);

                AddLine("<margin=1em>Teleported to: ( "
                    + pm.transform.position.x.ToString(CultureInfo.InvariantCulture)
                    + ", " + pm.transform.position.y.ToString(CultureInfo.InvariantCulture)
                    + ", " + pm.transform.position.z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
            }
        }


        [Command("/tpp", "Teleport to another planet.")]
        public void TeleportPlanet(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Teleport to another planet");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tpp planetName</color> - teleport to the named planet, case insensitive");
                AddLine("<margin=2em><color=#FFFF00>/tpp list</color> - list planet names");
            }
            else
            {
                var availablePlanets = PlanetNetworkLoader.Instance.GetAvailablePlanets(false, true);
                bool found = false;
                if (args[1] != "list")
                {
                    foreach (var ap in availablePlanets)
                    {
                        if (ap.id.Equals(args[1], StringComparison.InvariantCultureIgnoreCase))
                        {
                            PlanetNetworkLoader.Instance.SwitchToPlanet(ap);
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    if (availablePlanets.Count != 0)
                    {
                        AddLine("<margin=1em>Available planets");
                        foreach (var ap in availablePlanets)
                        {
                            AddLine("<margin=2em><#00FF00>" + ap.id);
                        }
                    }
                    else
                    {
                        AddLine("<margin=1em>No available planets");
                    }
                }
            }
        }

        [Command("/tp-list", "List all known user-named teleport locations; can specify name prefix.")]
        public void TeleportList(List<string> args)
        {
            EnsureTeleportLocations();
            if (savedTeleportLocations.Count != 0)
            {
                List<string> tpNames = [];
                string prefix = "";
                if (args.Count >= 2)
                {
                    prefix = args[1].ToLower(CultureInfo.InvariantCulture);
                }
                foreach (var n in savedTeleportLocations.Keys)
                {
                    if (n.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                    {
                        tpNames.Add(n);
                    }
                }
                tpNames.Sort();
                foreach (var tpName in tpNames)
                {
                    var pos = savedTeleportLocations[tpName];
                    AddLine("<margin=1em><color=#00FF00>" + tpName + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
            }
            else
            {
                AddLine("<margin=1em>No user-named teleport locations known");
                AddLine("<margin=1em>Use <b><color=#FFFF00>/tp-create</color></b> to create one for the current location.");
            }
        }

        [Command("/tp-create", "Save the current player location as a named location.")]
        public void TeleportCreate(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Save the current player location as a named location");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tp-create location-name</color> - save the current location with the given name");
            } else
            {
                EnsureTeleportLocations();
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                var pos = pm.transform.position;
                savedTeleportLocations[args[1]] = pos;
                PersistTeleportLocations();

                AddLine("<margin=1em>Teleport location created: <color=#00FF00>" + args[1] + "</color> at ( "
                    + pos.x.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
            }
        }

        [Command("/tp-remove", "Remove a user-named teleport location.")]
        public void TeleportRemove(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Remove the specified user-named teleport location");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tp-remove location-name</color> - save the current location with the given name");
                AddLine("<margin=1em>See also <color=#FFFF00>/tp-list</color>.");
            }
            else
            {
                EnsureTeleportLocations();

                var tpName = args[1];
                if (savedTeleportLocations.TryGetValue(tpName, out var pos))
                {
                    savedTeleportLocations.Remove(tpName);
                    PersistTeleportLocations();
                    AddLine("<margin=1em>Teleport location removed: <color=#00FF00>" + tpName + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown location");
                }
            }
        }

        static bool TryGetSavedTeleportLocation(string name, out Vector3 pos)
        {
            EnsureTeleportLocations();
            return savedTeleportLocations.TryGetValue(name, out pos);
        }

        static void EnsureTeleportLocations()
        {
            if (savedTeleportLocations == null)
            {
                savedTeleportLocations = [];

                string filename = string.Format("{0}/{1}.json", Application.persistentDataPath, "CommandConsole_Locations.txt");

                if (!File.Exists(filename))
                {
                    filename = string.Format("{0}/{1}", Application.persistentDataPath, "CommandConsole_Locations.txt");
                }

                if (File.Exists(filename))
                {
                    foreach (var line in File.ReadAllLines(filename))
                    {
                        string[] sep = line.Split(';');
                        try
                        {
                            var p = new Vector3(
                                float.Parse(sep[1], CultureInfo.InvariantCulture),
                                float.Parse(sep[2], CultureInfo.InvariantCulture),
                                float.Parse(sep[3], CultureInfo.InvariantCulture)
                            );
                            savedTeleportLocations[sep[0]] = p;
                        }
                        catch (Exception ex)
                        {
                            Log(ex);
                        }
                    }
                }
            }
        }

        static void PersistTeleportLocations()
        {
            if (savedTeleportLocations != null)
            {
                string filename = string.Format("{0}/{1}", Application.persistentDataPath, "CommandConsole_Locations.txt");

                List<string> lines = [];

                foreach (var kv in savedTeleportLocations)
                {
                    var v = kv.Value;
                    lines.Add(kv.Key
                        + ";" + v.x.ToString(CultureInfo.InvariantCulture)
                        + ";" + v.y.ToString(CultureInfo.InvariantCulture)
                        + ";" + v.z.ToString(CultureInfo.InvariantCulture)
                    );
                }

                File.WriteAllLines(filename, lines);
            }
        }

        [Command("/list-stages", "List the terraformation stages along with their Ti amounts.")]
        public void ListStages(List<string> _)
        {
            var pd = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData();
            foreach (var stage in pd.GetPlanetTerraformationStages())
            {
                AddLine("<margin=1em><color=#FFFFFF>" + stage.GetTerraId()
                    + "</color> <color=#00FF00>\"" + Readable.GetTerraformStageName(stage)
                    + "\"</color> @ <b>"
                    + string.Format("{0:#,##0}", stage.GetStageStartValue()) + "</b> " + stage.GetWorldUnitType());
            }
        }

        [Command("/add-ti", "Adds the specified amount to the Ti value.")]
        public void AddTi(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Ti value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-ti amount</color> - Ti += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Terraformation, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Terraformation updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-heat", "Adds the specified amount to the Heat value.")]
        public void AddHeat(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Heat value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-heat amount</color> - Heat += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Heat, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Heat updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-oxygen", "Adds the specified amount to the Oxygen value.")]
        public void AddOxygen(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Oxygen value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-oxygen amount</color> - Oxygen += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Oxygen, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Oxygen updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }
        
        [Command("/add-pressure", "Adds the specified amount to the Pressure value.")]
        public void AddPressure(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Pressure value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-pressure amount</color> - Pressure += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Pressure, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Pressure updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-biomass", "Adds the specified amount to the Biomass value.")]
        public void AddBiomass(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Biomass value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-biomass amount</color> - Biomass += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Biomass, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Biomass updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-plants", "Adds the specified amount to the Plants value.")]
        public void AddPlants(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Plants value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-plants amount</color> - Plants += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Plants, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Plants updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-insects", "Adds the specified amount to the Insects value.")]
        public void AddInsects(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Insects value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-insects amount</color> - Insects += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Insects, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Insects updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-animals", "Adds the specified amount to the Animals value.")]
        public void AddAnimals(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Animals value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-animals amount</color> - Animals += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                AddLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color> or <color=#FFFF00>/add-insects</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Animals, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Animals updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        float AddToWorldUnit(DataConfig.WorldUnitType wut, float amount)
        {
            var result = 0.0f;
            var worldUnitCurrentTotalValue = AccessTools.Field(typeof(WorldUnit), "_currentTotalValue");
            var worldUnitsPositioningWorldUnitsHandler = AccessTools.Field(typeof(WorldUnitPositioning), "worldUnitsHandler");
            var worldUnitsPositioningHasMadeFirstInit = AccessTools.Field(typeof(WorldUnitPositioning), "hasMadeFirstInit");

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            foreach (WorldUnit wu in wuh.GetAllPlanetUnits())
            {
                if (wu.GetUnitType() == wut)
                {
                    result = (float)worldUnitCurrentTotalValue.GetValue(wu) + amount;
                    worldUnitCurrentTotalValue.SetValue(wu, result);
                }
                if (wu.GetUnitType() == DataConfig.WorldUnitType.Terraformation && wut != DataConfig.WorldUnitType.Terraformation)
                {
                    worldUnitCurrentTotalValue.SetValue(wu, (float)worldUnitCurrentTotalValue.GetValue(wu) + amount);
                }
                if (wu.GetUnitType() == DataConfig.WorldUnitType.Biomass
                    && (wut == DataConfig.WorldUnitType.Plants
                    || wut == DataConfig.WorldUnitType.Insects
                    || wut == DataConfig.WorldUnitType.Animals))
                {
                    worldUnitCurrentTotalValue.SetValue(wu, (float)worldUnitCurrentTotalValue.GetValue(wu) + amount);
                }
            }
            /*
            var go = FindObjectOfType<AlertUnlockables>();
            if (go != null)
            {
                AccessTools.Field(typeof(AlertUnlockables), "hasInited").SetValue(go, false);
            }
            */

            var allWaterVolumes = FindObjectsByType<WaterVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            //LogInfo("allWaterVolumes.Count = " + allWaterVolumes.Count);
            foreach (var go1 in allWaterVolumes)
            {
                var wup = go1.GetComponent<WorldUnitPositioning>();

                //LogInfo("WorldUnitPositioning-Before: " + wup.transform.position);
                if (wup != null)
                {
                    worldUnitsPositioningWorldUnitsHandler.SetValue(wup, wuh);
                    worldUnitsPositioningHasMadeFirstInit.SetValue(wup, false);
                    wup.UpdateEvolutionPositioning();
                }
                //LogInfo("WorldUnitPositioning-After: " + wup.transform.position);
            }
            return result;
        }

        [Command("/list-microchip-tiers", "Lists all unlock tiers and unlock items of the microchips.")]
        public void ListMicrochipTiers(List<string> _)
        {
            UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

            List<List<GroupData>> tiers =
            [
                unlock.unlockingData.tier1GroupToUnlock,
                unlock.unlockingData.tier2GroupToUnlock,
                unlock.unlockingData.tier3GroupToUnlock,
                unlock.unlockingData.tier4GroupToUnlock,
                unlock.unlockingData.tier5GroupToUnlock,
                unlock.unlockingData.tier6GroupToUnlock,
                unlock.unlockingData.tier7GroupToUnlock,
                unlock.unlockingData.tier8GroupToUnlock,
                unlock.unlockingData.tier9GroupToUnlock,
                unlock.unlockingData.tier10GroupToUnlock,
            ];

            for (int i = 0; i < tiers.Count; i++)
            {
                List<GroupData> gd = tiers[i];
                AddLine("<margin=1em><b>Tier #" + (i + 1));

                if (gd.Count != 0)
                {
                    foreach (GroupData g in gd)
                    {
                        var sb = new StringBuilder();
                        sb.Append(" <color=#FFFFFF>").Append(g.id).Append("</color> ")
                            .Append("<color=#00FF00>\"")
                            .Append(Readable.GetGroupName(GroupsHandler.GetGroupViaId(g.id)))
                            .Append("\"")
                            ;
                        AddLine("<margin=2em>" + sb.ToString());
                    }
                }
                else
                {
                    AddLine("<margin=2em>None");
                }
            }

        }

        [Command("/list-microchip-unlocks", "List the identifiers of microchip unlocks; can specify prefix.")]
        public void ListMicrochipUnlocks(List<string> args)
        {
            UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

            List<List<GroupData>> tiers =
            [
                unlock.unlockingData.tier1GroupToUnlock,
                unlock.unlockingData.tier2GroupToUnlock,
                unlock.unlockingData.tier3GroupToUnlock,
                unlock.unlockingData.tier4GroupToUnlock,
                unlock.unlockingData.tier5GroupToUnlock,
                unlock.unlockingData.tier6GroupToUnlock,
                unlock.unlockingData.tier7GroupToUnlock,
                unlock.unlockingData.tier8GroupToUnlock,
                unlock.unlockingData.tier9GroupToUnlock,
                unlock.unlockingData.tier10GroupToUnlock,
            ];

            string prefix = "";
            if (args.Count >= 2)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }

            List<string> list = [];

            for (int i = 0; i < tiers.Count; i++)
            {
                List<GroupData> gd = tiers[i];

                if (gd.Count != 0)
                {
                    foreach (GroupData g in gd)
                    {
                        if (g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            list.Add(g.id);
                        }
                    }
                }
            }

            if (list.Count == 0)
            {
                AddLine("<margin=1em>No microchip unlocks found.");
            }
            else
            {
                list.Sort();
                Colorize(list, "#00FF00");
                foreach (var line in JoinPerLine(list, 5))
                {
                    AddLine("<margin=2em>" + line);
                }
            }
        }

        [Command("/unlock-microchip", "Unlocks a specific microchip techology.")]
        public void UnlockMicrochip(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Unlocks a specific microchip techology");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/unlock-microchip list [prefix]</color> - lists technologies not unlocked yet");
                AddLine("<margin=2em><color=#FFFF00>/unlock-microchip identifier</color> - Unlocks the given technology");
            }
            else
            {
                if (args[1] == "list")
                {
                    UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                    List<GroupData> tiers =
                    [
                        .. unlock.unlockingData.tier1GroupToUnlock,
                        .. unlock.unlockingData.tier2GroupToUnlock,
                        .. unlock.unlockingData.tier3GroupToUnlock,
                        .. unlock.unlockingData.tier4GroupToUnlock,
                        .. unlock.unlockingData.tier5GroupToUnlock,
                        .. unlock.unlockingData.tier6GroupToUnlock,
                        .. unlock.unlockingData.tier7GroupToUnlock,
                        .. unlock.unlockingData.tier8GroupToUnlock,
                        .. unlock.unlockingData.tier9GroupToUnlock,
                        .. unlock.unlockingData.tier10GroupToUnlock,
                    ];

                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> list = [];
                    foreach (var gd in tiers)
                    {
                        var g = GroupsHandler.GetGroupViaId(gd.id);
                        if (!UnlockedGroupsHandler.Instance.IsGloballyUnlocked(g) && g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            list.Add(g.id);
                        }
                    }
                    if (list.Count != 0)
                    {
                        list.Sort();
                        Colorize(list, "#00FF00");
                        foreach (var line in JoinPerLine(list, 5))
                        {
                            AddLine("<margin=2em>" + line);
                        }
                    }
                }
                else
                {
                    var gr = FindGroup(args[1].ToLowerInvariant());
                    if (gr != null)
                    {
                        AddLine("<margin=1em>Unlocked: <color=#FFFFFF>" + gr.id + "</color> <color=#00FF00>\"" + Readable.GetGroupName(gr) + "\"");
                        UnlockedGroupsHandler.Instance.UnlockGroupGlobally(gr);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        DidYouMean(args[1].ToLowerInvariant(), true, true);
                    }
                }
            }
        }

        [Command("/unlock", "Unlocks a specific techology.")]
        public void Unlock(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Unlocks a specific techology");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/unlock list [prefix]</color> - lists technologies not unlocked yet");
                AddLine("<margin=2em><color=#FFFF00>/unlock identifier</color> - Unlocks the given technology");
            }
            else
            {
                if (args[1] == "list")
                {
                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> list = [];
                    foreach (var gd in GroupsHandler.GetAllGroups())
                    {
                        var g = GroupsHandler.GetGroupViaId(gd.id);
                        if (!UnlockedGroupsHandler.Instance.IsGloballyUnlocked(g) && g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            list.Add(g.id);
                        }
                    }
                    if (list.Count != 0)
                    {
                        list.Sort();
                        Colorize(list, "#00FF00");
                        foreach (var line in JoinPerLine(list, 5))
                        {
                            AddLine("<margin=2em>" + line);
                        }
                    }
                }
                else
                {
                    var gr = FindGroup(args[1].ToLowerInvariant());
                    if (gr != null)
                    {
                        AddLine("<margin=1em>Unlocked: <color=#FFFFFF>" + gr.id + "</color> <color=#00FF00>\"" + Readable.GetGroupName(gr) + "\"");
                        UnlockedGroupsHandler.Instance.UnlockGroupGlobally(gr);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        DidYouMean(args[1].ToLowerInvariant(), true, true);
                    }
                }
            }
        }

        [Command("/list-tech", "Lists all technology identifiers; can filter via prefix.")]
        public void ListTech(List<string> args)
        {
            var prefix = "";
            if (args.Count > 1)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }
            List<string> list = [];
            foreach (var gd in GroupsHandler.GetAllGroups())
            {
                var g = GroupsHandler.GetGroupViaId(gd.id);
                if (g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                {
                    list.Add(g.id);
                }
            }
            if (list.Count != 0)
            {
                list.Sort();
                Colorize(list, "#00FF00");
                foreach (var line in JoinPerLine(list, 5))
                {
                    AddLine("<margin=2em>" + line);
                }
            }
        }

        SpaceCraft.Group FindGroup(string gid)
        {
            List<SpaceCraft.Group> groupByName = [];
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                var gci = gr.GetId().ToLower(CultureInfo.InvariantCulture);
                if (gci == gid && !gci.StartsWith("spacemultiplier"))
                {
                    return gr;
                }
                var nameLocalized = Localization.GetLocalizedString(GameConfig.localizationGroupNameId + gr.GetId()) ?? "";
                if (nameLocalized.Contains(gid, StringComparison.InvariantCultureIgnoreCase))
                {
                    groupByName.Add(gr);
                }

            }
            if (groupByName.Count == 1)
            {
                return groupByName[0];
            }
            return null;
        }

        void DidYouMean(string gid, bool isStructure, bool isItem)
        {
            List<string> similar = FindSimilar(gid, GroupsHandler.GetAllGroups()
                .Where(g => { 
                    if (isStructure && g is GroupConstructible && !g.id.StartsWith("SpaceMultiplier"))
                    {
                        return true;
                    }
                    return isItem && g is GroupItem;
                })
                .Select(g => g.id));
            if (similar.Count != 0)
            {
                similar.Sort();
                Colorize(similar, "#00FF00");

                if (isStructure && !isItem)
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown structure.</color> Did you mean?");
                }
                else
                if (isItem && !isStructure)
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown item.</color> Did you mean?");
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown item or structure.</color> Did you mean?");
                }
                foreach (var line in JoinPerLine(similar, 5))
                {
                    AddLine("<margin=2em>" + line);
                }
            }
            else
            {
                if (isStructure && !isItem)
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown structure.</color>");
                }
                else if (isItem && !isStructure)
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown item.</color>");
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Unknown item or structure.</color>");
                }
            }

        }

        [Command("/tech-info", "Shows detailed information about a technology.")]
        public void TechInfo(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Shows detailed information about a technology");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tech-info identifier</color> - show detailed info");
                AddLine("<margin=1em>See also <color=#FFFF00>/list-tech</color> for all identifiers");
            }
            else
            {
                var gr = FindGroup(args[1].ToLowerInvariant());
                if (gr == null)
                {
                    DidYouMean(args[1].ToLowerInvariant(), true, true);
                }
                else
                {
                    AddLine("<margin=1em><b>ID:</b> <color=#00FF00>" + gr.id);
                    AddLine("<margin=1em><b>Name:</b> <color=#00FF00>" + Readable.GetGroupName(gr));
                    AddLine("<margin=1em><b>Description:</b> <color=#00FF00>" + Readable.GetGroupDescription(gr));
                    var unlockInfo = gr.GetUnlockingInfos();
                    AddLine("<margin=1em><b>Is Unlocked:</b>");
                    AddLine("<margin=2em><b>Globally:</b> <color=#00FF00>" + gr.GetIsGloballyUnlocked());
                    AddLine("<margin=2em><b>Blueprint:</b> <color=#00FF00>" + unlockInfo.GetIsUnlockedViaBlueprint());
                    AddLine("<margin=2em><b>Progress:</b> <color=#00FF00>" + unlockInfo.GetIsUnlocked());
                    AddLine("<margin=2em><b>At:</b> <color=#00FF00>" + string.Format("{0:#,##0}", unlockInfo.GetUnlockingValue()) + " " + unlockInfo.GetWorldUnit());
                    if (gr is GroupItem gi)
                    {
                        AddLine("<margin=1em><b>Class:</b> <color=#00FF00>Item");
                        if (gi.GetUsableType() != DataConfig.UsableType.Null)
                        {
                            AddLine("<margin=2em><b>Usable:</b> " + gi.GetUsableType());
                        }
                        if (gi.GetEquipableType() != DataConfig.EquipableType.Null)
                        {
                            AddLine("<margin=2em><b>Equipable:</b> " + gi.GetEquipableType());
                        }
                        if (gi.GetItemCategory() != DataConfig.ItemCategory.Null)
                        {
                            AddLine("<margin=2em><b>Category:</b> <color=#00FF00>" + gi.GetItemCategory());
                        }
                        if (gi.GetItemSubCategory() != DataConfig.ItemSubCategory.Null)
                        {
                            AddLine("<margin=2em><b>Subcategory:</b> <color=#00FF00>" + gi.GetItemSubCategory());
                        }
                        AddLine("<margin=2em><b>Value:</b> <color=#00FF00>" + gi.GetGroupValue());
                        List<string> list = [];
                        foreach (var e in Enum.GetValues(typeof(DataConfig.CraftableIn)))
                        {
                            if (gi.CanBeCraftedIn((DataConfig.CraftableIn)e))
                            {
                                list.Add(((DataConfig.CraftableIn)e).ToString());
                            }
                        }
                        if (list.Count != 0)
                        {
                            Colorize(list, "#00FF00");
                            AddLine("<margin=2em><b>Craftable in:</b> " + String.Join(", ", list));
                        }

                        list = [];
                        foreach (var e in Enum.GetValues(typeof(DataConfig.WorldUnitType)))
                        {
                            var v = gi.GetGroupUnitMultiplier((DataConfig.WorldUnitType)e);
                            if (v != 0)
                            {
                                list.Add(v + " " + ((DataConfig.WorldUnitType)e).ToString());
                            }
                        }
                        if (list.Count != 0)
                        {
                            Colorize(list, "#00FF00");
                            AddLine("<margin=2em><b>Unit:</b> " + string.Join(", ", list));
                        }

                        var ggi = gi.GetGrowableGroup();
                        if (ggi != null)
                        {
                            AddLine("<margin=2em><b>Grows:</b> <color=#00FF00>" + ggi.GetId() + " \"" + Readable.GetGroupName(ggi) + "\"");
                        }
                        var ulg = gi.GetUnlocksGroup();
                        if (ulg != null)
                        {
                            AddLine("<margin=2em><b>Unlocks group:</b> <color=#00FF00>" + ulg.GetId() + " \"" + Readable.GetGroupName(ulg) + "\"");
                        }

                        EffectOnPlayer eff = gi.GetEffectOnPlayer();
                        if (eff != null)
                        {
                            AddLine("<margin=2em><b>Effect on player:</b> <color=#00FF00>" + eff.effectOnPlayer + " (" + eff.durationInSeconds + " seconds");
                        }
                        AddLine("<margin=2em><b>Chance to spawn:</b> <color=#00FF00>" + gi.GetChanceToSpawn());
                        AddLine("<margin=2em><b>Destroyable:</b> <color=#00FF00>" + !gi.GetCantBeDestroyed());
                        AddLine("<margin=2em><b>Logistics display type:</b> <color=#00FF00>" + gi.GetLogisticDisplayType());
                        AddLine("<margin=2em><b>Recycleable:</b> <color=#00FF00>" + !gi.GetCantBeRecycled());
                        AddLine("<margin=2em><b>World pickup by drone:</b> <color=#00FF00>" + gi.GetCanBePickedUpFromWorldByDrones());
                    }
                    else if (gr is GroupConstructible gc)
                    {
                        AddLine("<margin=1em><b>Class:</b> <color=#00FF00>Building");
                        AddLine("<margin=1em><b>Category:</b> <color=#00FF00>" + gc.GetGroupCategory());
                        if (gc.GetWorldUnitMultiplied() != DataConfig.WorldUnitType.Null)
                        {
                            AddLine("<margin=1em><b>Unit multiplied:</b> <color=#00FF00>" + gc.GetWorldUnitMultiplied());
                        }
                        List<string> list = [];
                        foreach (var e in Enum.GetValues(typeof(DataConfig.WorldUnitType)))
                        {
                            var v = gc.GetGroupUnitGeneration((DataConfig.WorldUnitType)e);
                            if (v != 0)
                            {
                                list.Add(v + " " + ((DataConfig.WorldUnitType)e).ToString());
                            }
                        }
                        if (list.Count != 0)
                        {
                            Colorize(list, "#00FF00");
                            AddLine("<margin=1em><b>Unit generation:</b> " + string.Join(", ", list));
                        }
                        var ng = gc.GetNextTierGroup();
                        if (ng != null)
                        {
                            AddLine("<margin=1em><b>Next tier group:</b> <color=#00FF00>" + ng.id + " \"" + Readable.GetGroupName(ng) + "\""); 
                        }
                        var tsrs = gc.GetTerraStageRequirements();
                        if (tsrs != null && tsrs.Length != 0)
                        {
                            foreach (var tsr in tsrs)
                            {
                                AddLine("<margin=1em><b>Terrastage requirement:</b> <color=#00FF00>" + string.Format("{0:#,##0}", tsr.GetStageStartValue()) + " " + tsr.GetWorldUnitType());
                            }
                        }
                        var notallowed = gc.GetNotAllowedPlanetsRequirement();
                        if (notallowed != null && notallowed.Count != 0)
                        {
                            AddLine("<margin=1em><b>Not allowed planets requirement:</b> <color=#00FF00>" 
                                + string.Join(", ", notallowed.Select(v => v.id)));
                        }
                    } 
                    else
                    {
                        AddLine("<margin=1em><b>Class:</b> Unknown");
                    }

                    AddLine("<margin=2em><b>Hide in crafter:</b> <color=#00FF00>" + gr.GetHideInCrafter()); ;
                    AddLine("<margin=2em><b>Trade category:</b> <color=#00FF00>" + gr.GetTradeCategory());
                    AddLine("<margin=2em><b>Trade value:</b> <color=#00FF00>" + gr.GetTradeValue());
                    AddLine("<margin=2em><b>Loot recipe on deconstruct:</b> <color=#00FF00>" + gr.GetLootRecipeOnDeconstruct());
                    AddLine("<margin=2em><b>Interplanetary logistics type:</b> <color=#00FF00>" + gr.GetLogisticInterplanetaryType());
                    AddLine("<margin=2em><b>Primary inventory size:</b> <color=#00FF00>" + gr.GetInventorySize());
                    var sis = gr.GetSecondaryInventoriesSize();
                    if (sis != null && sis.Count != 0) {
                        AddLine("<margin=2em><b>Secondary inventory size:</b> <color=#00FF00>" + string.Join(", ", sis));
                    }

                    var grd = gr.GetGroupData();
                    AddLine("<margin=2em><b>Planet usage type:</b> <color=#00FF00>" + grd.planetUsageType);
                    if (grd.unlockInPlanets != null && grd.unlockInPlanets.Count != 0) {
                        AddLine("<margin=2em><b>Unlock in planets:</b> <color=#00FF00>" + string.Join(", ", grd.unlockInPlanets.Select(p => p.id)));
                    } 
                    else
                    {
                        AddLine("<margin=2em><b>Unlock in planets:</b> <color=#00FF00>None");
                    }

                    var recipe = gr.GetRecipe();
                    if (recipe != null)
                    {
                        var ingr = recipe.GetIngredientsGroupInRecipe();
                        if (ingr.Count != 0)
                        {
                            AddLine("<margin=1em><b>Recipe:</b>");
                            foreach (var rg in ingr)
                            {
                                AddLine("<margin=2em><color=#00FF00>" + rg.id + " \"" + Readable.GetGroupName(rg) + "\"");
                            }
                        }
                        else
                        {
                            AddLine("<margin=1em><b>Recipe:</b> None");
                        }
                    }
                }
            }
        }

        [Command("/copy-to-clipboard", "Copies the console history to the system clipboard.")]
        public void CopyToClipboard(List<string> _)
        {
            GUIUtility.systemCopyBuffer = string.Join("\n", consoleText);
        }

        [Command("/ctc", "Copies the console history to the system clipboard without formatting.")]
        public void CopyToClipboard2(List<string> _)
        {
            var str = string.Join("\n", consoleText);
            str = str.Replace("<margin=1em>", "    ");
            str = str.Replace("<margin=2em>", "        ");
            str = str.Replace("<margin=3em>", "            ");
            str = str.Replace("<margin=4em>", "                ");
            str = str.Replace("<margin=5em>", "                    ");
            GUIUtility.systemCopyBuffer = Regex.Replace(str, "<\\/?.*?>", "");
        }

        [Command("/refill", "Refills the Health, Water and Oxygen meters.")]
        public void Refill(List<string> _)
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var gh = pm.GetGaugesHandler();
            gh.AddHealth(100);
            gh.AddWater(100);
            gh.AddOxygen(1000);
            gh.RemoveToxic(10000);
            AddLine("<margin=1em>Health, Water and Oxygen refilled");
        }

        [Command("/auto-refill", "Automatically refills the Health, Water and Oxygen. Re-issue command to stop.")]
        public void AutoRefill(List<string> _)
        {
            if (autorefillCoroutine != null)
            {
                AddLine("<margin=1em>Auto Refill stopped");
                StopCoroutine(autorefillCoroutine);
                autorefillCoroutine = null;
            }
            else
            {
                AddLine("<margin=1em>Auto Refill started");
                autorefillCoroutine = AutoRefillCoroutine();
                StartCoroutine(autorefillCoroutine);
            }
        }

        IEnumerator AutoRefillCoroutine()
        {
            for (; ; ) {
                var pc = Managers.GetManager<PlayersManager>();
                if (pc != null)
                {
                    var pm = pc.GetActivePlayerController();
                    if (pm != null)
                    {
                        var gh = pm.GetGaugesHandler();
                        if (gh != null)
                        {
                            gh.AddHealth(100);
                            gh.AddWater(100);
                            gh.AddOxygen(1000);
                            gh.RemoveToxic(10000);

                            yield return new WaitForSeconds(1);

                            continue;
                        }
                    }
                }
                autorefillCoroutine = null;
                break;
            }
        }

        [Command("/add-health", "Adds a specific Health amount to the player.")]
        public void AddHealth(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds a specific Health amount to the player");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-health amount</color> - Health += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-water</color>, <color=#FFFF00>/add-air</color> or <color=#FFFF00>/remove-toxic</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddHealth(int.Parse(args[1]));
            }
        }

        [Command("/add-water", "Adds a specific Water amount to the player.")]
        public void AddWater(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds a specific Water amount to the player");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-water amount</color> - Water += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-health</color>, <color=#FFFF00>/add-air</color> or <color=#FFFF00>/remove-toxic</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddWater(int.Parse(args[1]));
            }
        }

        [Command("/add-air", "Adds a specific Air amount to the player.")]
        public void AddAir(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds a specific Air amount to the player");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-air amount</color> - Water += amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-health</color>, <color=#FFFF00>/add-water</color> or <color=#FFFF00>/remove-toxic</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddOxygen(int.Parse(args[1]));
            }
        }

        [Command("/remove-toxic", "Removes a specific amount of toxicity from the player.")]
        public void RemoveToxic(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Removes a specific amount of toxicity from the player");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/remove-toxic amount</color> - Toxicity -= amount");
                AddLine("<margin=1em>See also <color=#FFFF00>/add-health</color>, <color=#FFFF00>/add-water</color> or <color=#FFFF00>/add-air</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.RemoveToxic(int.Parse(args[1]));
            }
        }

        [Command("/add-purity", "Adds a specific purity amount to the current planet.")]
        public void AddPurity(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds a specific purity amount to the current planet");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-purity amount</color> - Purity += amount");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Purification, float.Parse(args[1], CultureInfo.InvariantCulture));
                AddLine("<margin=1em>Purification updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }


        [Command("/die", "Kills the player.")]
        public void Die(List<string> _)
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var sm = pm.GetPlayerStatus();
            DyingConsequencesHandler.HandleDyingConsequences(pm, GroupsHandler.GetGroupViaId(sm.canisterGroup.id));
            sm.DieAndRespawn();
            AddLine("<margin=1em>Player died and respawned.");
            //Managers.GetManager<WindowsHandler>().CloseAllWindows();
        }

        [Command("/list-larvae", "Show information about larvae sequencing; can use prefix filter.")]
        public void LarvaeSequenceInfo(List<string> args)
        {
            var prefix = "";
            if (args.Count > 1)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }

            Dictionary<string, List<string>> larvaeToSequenceInto = [];
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                if (gr is GroupItem gi && gi.CanBeCraftedIn(DataConfig.CraftableIn.CraftIncubatorT1))
                {
                    var recipe = gi.GetRecipe().GetIngredientsGroupInRecipe();
                    foreach (var rgi in recipe)
                    {
                        var rgid = rgi.GetId();
                        if (rgid.StartsWith("LarvaeBase") && rgid.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            if (!larvaeToSequenceInto.TryGetValue(rgid, out var list))
                            {
                                list = [];
                                larvaeToSequenceInto[rgid] = list;
                            }
                            list.Add(gi.GetId());
                        }
                    }
                }
            }

            foreach (var larvaeAndOutcome in larvaeToSequenceInto)
            {
                var larve = GroupsHandler.GetGroupViaId(larvaeAndOutcome.Key);
                var outcomes = larvaeAndOutcome.Value;

                AddLine("<margin=1em><b><color=#00FFFF>" + larve.id + " \"" + Readable.GetGroupName(larve) + "\"");
                var ull = larve.GetUnlockingInfos();
                if (ull.GetIsUnlocked())
                {
                    AddLine("<margin=2em><b>Unlocked:</b> true");
                }
                else
                {
                    if (ull.GetWorldUnit() != DataConfig.WorldUnitType.Null)
                    {
                        AddLine("<margin=2em><b>Unlocked:</b> false");
                        AddLine("<margin=4em><b>Unlocked at:</b> " + string.Format("{0:#,##0}", ull.GetUnlockingValue()) + " " + ull.GetWorldUnit());
                    }
                    else
                    {
                        AddLine("<margin=2em><b>Unlocked globally:</b> " + larve.GetIsGloballyUnlocked());
                    }
                }
                AddLine("<margin=2em><b>Outcomes</b>");

                foreach (var outcome in outcomes)
                {
                    if (GroupsHandler.GetGroupViaId(outcome) is GroupItem og)
                    {
                        var chance = og.GetChanceToSpawn();
                        if (chance == 0)
                        {
                            chance = 100;
                        }
                        var ul = og.GetUnlockingInfos();
                        if (ul.GetIsUnlocked())
                        {
                            AddLine("<margin=3em><color=#00FF00>" + og.id + " \"" + Readable.GetGroupName(og) + "\"</color> = <b>" + chance + " %</b>");
                        }
                        else
                        {
                            AddLine("<margin=3em><color=#FF0000>[Not unlocked]</color> <color=#00FF00>" + og.id + " \"" + Readable.GetGroupName(og) + "\"</color> = <b>" + chance + " %</b>");
                        }
                        if (ul.GetWorldUnit() != DataConfig.WorldUnitType.Null)
                        {
                            AddLine("<margin=4em><b>Unlocked at:</b> " + string.Format("{0:#,##0}", ul.GetUnlockingValue()) + " " + ul.GetWorldUnit());
                        }
                        else
                        {
                            AddLine("<margin=4em><b>Unlocked globally:</b> " + og.GetIsGloballyUnlocked());
                        }
                    }
                }
            }
        }

        [Command("/list-loot", "List chest loot information.")]
        public void ListLoot(List<string> _)
        {
            var stagesLH = Managers.GetManager<InventoryLootHandler>();
            var stages = stagesLH.lootTerraStages;

            logger.LogInfo("Found " + stages.Count + " stages");
            stages.Sort((a, b) =>
            {
                var v1 = a.terraStage.GetStageStartValue();
                var v2 = b.terraStage.GetStageStartValue();
                return v1 < v2 ? -1 : (v1 > v2 ? 1 : 0);
            });
            foreach (InventoryLootStage ils in stages)
            {
                AddLine("<margin=1em><b><color=#00FFFF>" + ils.terraStage.GetTerraId() + " \"" + Readable.GetTerraformStageName(ils.terraStage) + "\"</color></b> at "
                    + string.Format("{0:#,##0}", ils.terraStage.GetStageStartValue()) + " Ti");

                string[] titles = ["Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare"];
                List<List<GroupData>> gs =
                [
                    ils.commonItems ?? [], ils.unCommonItems ?? [], ils.rareItems ?? [], ils.veryRareItems ?? [], ils.ultraRareItems ?? []
                ];
                var boostAmount = (int)AccessTools.Field(typeof(InventoryLootStage), "defaultBoostedMultiplier").GetValue(ils);

                List<float> chances =
                [
                    100,
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceUnCommon").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceRare").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceVeryRare").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceUltraRare").GetValue(ils),
                ];

                for (int i = 0; i < titles.Length; i++)
                {
                    AddLine("<margin=2em><b>" + titles[i] + "</b> (Chance: " + chances[i] + " %, Boost multiplier: " + ((boostAmount + 2) / 3) + ")");
                    foreach (GroupData g in gs[i])
                    {
                        AddLine("<margin=3em><color=#00FF00>" + g.id + " \"" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(g.id)) + "\"");
                    }
                }
            }
        }

        [Command("/list-larvae-zones", "Lists the larvae zones and the larvae that can spawn there.")]
        public void ListLarvaeZones(List<string> _)
        {
            var sectors = FindObjectsByType<Sector>(FindObjectsSortMode.None);
            Logger.LogInfo("Sector count: " + sectors.Length);
            foreach (Sector sector in sectors)
            {
                if (sector == null || sector.gameObject == null)
                {
                    continue;
                }
                string name = sector.gameObject.name;
                Logger.LogInfo("Sector: " + name);

                SceneManager.LoadScene(name, LoadSceneMode.Additive);
            }

            foreach (LarvaeZone lz in FindObjectsByType<LarvaeZone>(FindObjectsSortMode.None))
            {
                var pool = lz.GetLarvaesToAddToPool();
                var bounds = lz.GetComponent<Collider>().bounds;
                var center = bounds.center;
                var extents = bounds.extents;

                var captureLarvaeZoneCurrentSector = "";
                foreach (SectorEnter sector in FindObjectsByType<SectorEnter>(FindObjectsSortMode.None))
                {
                    if (sector.collider != null)
                    {
                        var sbounds = sector.collider.bounds;
                        if (sbounds.Intersects(bounds))
                        {
                            captureLarvaeZoneCurrentSector += sector.sector.gameObject.name + " ";
                        }
                    }
                }

                if (captureLarvaeZoneCurrentSector.Length == 0)
                {
                    captureLarvaeZoneCurrentSector = "<color=#FF0000>Unable to determine";
                }

                AddLine("<margin=1em>Sector <color=#00FFFF>" + captureLarvaeZoneCurrentSector);

                AddLine("<margin=2em>Position = " + string.Join(", ", (int)center.x, (int)center.y, (int)center.z)
                    + ", Extents = " + string.Join(", ", (int)extents.x, (int)extents.y, (int)extents.z));

                if (pool != null && pool.Count != 0)
                {
                    foreach (var lp in pool)
                    {
                        AddLine("<margin=3em><color=#00FF00>" + lp.id
                            + " \"" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(lp.id)) + "\"</color>, Chance = " + lp.chanceToSpawn + "%");
                    }
                }
                else
                {
                    AddLine("<margin=3em>No larvae spawn info.");
                }
            }

            AddLine("<color=#FF8080>Warning! You may want to reload this save to avoid game issues.");
        }

        [Command("/build", "Build a structure. The ingredients are automatically added to the inventory first.")]
        public void Build(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Build a structure. The ingredients are automatically added to the inventory first.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/build list [name-prefix]</color> - list the item ids that can be built");
                AddLine("<margin=2em><color=#FFFF00>/build itemid [count]</color> - get the ingredients and start building it by showing the ghost");
            }
            else
            {
                if (args[1] == "list")
                {
                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> possibleStructures = [];
                    foreach (var g in GroupsHandler.GetAllGroups())
                    {
                        if (g is GroupConstructible gc)
                        {
                            var gci = gc.GetId().ToLower(CultureInfo.InvariantCulture);
                            if (gci.StartsWith(prefix) && !gci.StartsWith("SpaceMultiplier"))
                            {
                                possibleStructures.Add(gc.GetId());
                            }
                        }
                    }
                    possibleStructures.Sort();
                    Colorize(possibleStructures, "#00FF00");
                    foreach (var line in JoinPerLine(possibleStructures, 5))
                    {
                        AddLine("<margin=1em>" + line);
                    }
                }
                else
                {
                    var gid = args[1].ToLowerInvariant();
                    SpaceCraft.Group g = FindGroup(gid);
                    if (g == null)
                    {
                        DidYouMean(gid, true, false);
                    }
                    else if (g is not GroupConstructible)
                    {
                        AddLine("<color=#FF0000>This item can't be built.");
                        if (g is GroupItem)
                        {
                            if (g.id == "DNASequence")
                            {
                                AddLine("Use <color=#FFFF00>/spawn-dna</color> for this type of item.");
                            }
                            else
                            {
                                AddLine("Use <color=#FFFF00>/spawn " + g.id + "</color> instead.");
                            }
                        }
                    }
                    else
                    {
                        var player = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                        var inv = player.GetPlayerBackpack().GetInventory();
                        var gc = (GroupConstructible)g;

                        var recipe = gc.GetRecipe().GetIngredientsGroupInRecipe();

                        int n = 1;
                        if (args.Count > 2)
                        {
                            n = int.Parse(args[2]);
                        }
                        var full = false;
                        for (int k = 0; k < n; k++)
                        {
                            foreach (var ri in recipe)
                            {
                                if (!TryAddToInventory(inv, ri))
                                {
                                    full = true; 
                                    break;
                                }
                            }
                            if (full)
                            {
                                break;
                            }
                        }

                        if (full)
                        {
                            AddLine("<color=#FF0000>Inventory full.");
                        } 
                        else 
                        {
                            DestroyConsoleGUI();
                            var wh = Managers.GetManager<WindowsHandler>();
                            wh.CloseAllWindows();

                            PlayerBuilder pb = player.GetPlayerBuilder();

                            player.GetMultitool().SetState(DataConfig.MultiToolState.Build);

                            if (pb.GetIsGhostExisting())
                            {
                                Log("Cancelling previous ghost");
                                pb.InputOnCancelAction();
                            }

                            if (NetworkManager.Singleton.IsServer)
                            {
                                Log("Activating ghost for " + gc.GetId());
                                pb.SetNewGhost(gc);
                            }
                            else
                            {
                                Log("Activating delayed ghost for " + gc.GetId());
                                pb.StartCoroutine(SetNewGhostDelayed(pb, gc));
                            }
                        }
                    }
                }
            }
        }

        static IEnumerator SetNewGhostDelayed(PlayerBuilder pb, GroupConstructible gc)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (pb != null)
            {
                pb.SetNewGhost(gc);
            }
        }

        [Command("/raise", "Raises player-placed objects in a radius (cylindrically).")]
        public void Raise(List<string> args)
        {
            if (args.Count != 3)
            {
                AddLine("<margin=1em>Raises player-placed objects in a radius (cylindrically).");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/raise radius amount</color> - raise all items within the given radius by the given amount");
            }
            else
            {
                var radius = Math.Abs(float.Parse(args[1], CultureInfo.InvariantCulture));
                var amount = float.Parse(args[2], CultureInfo.InvariantCulture);

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var pos = pm.transform.position;
                var posXY = new Vector2(pos.x, pos.z);

                int i = 0;
                foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects().Values)
                {
                    if (!WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()) && wo.GetIsPlaced())
                    {
                        var wp = wo.GetPosition();
                        var xy = new Vector2(wp.x, wp.z);
                        
                        if (Vector2.Distance(xy, posXY) <= radius)
                        {
                            wo.SetPositionAndRotation(new Vector3(wp.x, wp.y + amount, wp.z), wo.GetRotation());
                            var wog = wo.GetGameObject();
                            if (wog != null)
                            {
                                wog.transform.position = wo.GetPosition();
                            }
                            i++;
                        }
                    }
                }
                AddLine("<margin=1em>" + i + " objects affected.");
            }
        }

        [Command("/console-set-left", "Sets the Command Console's window's left position on the screen.")]
        public void ConsoleLeft(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Sets the Command Console's left position on the screen.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/console-set-left value</color> - Set the window's left position");
            }
            else
            {
                consoleLeft.Value = int.Parse(args[1]);
            }
            AddLine("<margin=1em>Current Left: " + consoleLeft.Value);
            RecreateBackground(Managers.GetManager<WindowsHandler>());
        }

        [Command("/console-set-right", "Sets the Command Console's window's right position on the screen.")]
        public void ConsoleRight(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Sets the Command Console's right position on the screen.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/console-set-right value</color> - Set the window's right position");
            }
            else
            {
                consoleRight.Value = int.Parse(args[1]);
            }
            AddLine("<margin=1em>Current Right: " + consoleRight.Value);
            RecreateBackground(Managers.GetManager<WindowsHandler>());
        }

        [Command("/meteor", "Triggers or lists the available meteor events.")]
        public void Meteor(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Triggers or lists the available meteor events.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/meteor list</color> - Lists the queued and all meteor events");
                AddLine("<margin=2em><color=#FFFF00>/meteor clear</color> - Clears all queued meteor events");
                AddLine("<margin=2em><color=#FFFF00>/meteor eventId</color> - Triggers the meteor events by its case-insensitive name");
                AddLine("<margin=2em><color=#FFFF00>/meteor eventNumber</color> - Triggers the meteor events by its number");
            }
            else 
            {
                var mh = Managers.GetManager<MeteoHandler>();
                var list = (List<MeteoEventData>)AccessTools.Field(typeof(MeteoHandler), "_meteoEvents").GetValue(mh);
                var queue = (List<(MeteoEventData, Vector3?)>)AccessTools.Field(typeof(MeteoHandler), "_meteoEventQueue").GetValue(mh);
                var currIndex = (NetworkVariable<int>)AccessTools.Field(typeof(MeteoHandler), "_selectedDataMeteoEventIndex").GetValue(mh);
                if (args[1] == "list")
                {
                    AddLine("<margin=1em>Current meteor event:");
                    if (currIndex.Value != -1)
                    {
                        CreateMeteorEventLines(0, list[currIndex.Value]);
                    }
                    else
                    {
                        AddLine("<margin=2em>None.");
                    }

                    AddLine("<margin=1em>Queued meteor events:");
                    if (queue.Count == 0)
                    {
                        AddLine("<margin=2em>None.");
                    }
                    else
                    {
                        for (int i = 0; i < queue.Count; i++)
                        {
                            CreateMeteorEventLines(i, queue[i].Item1);
                        }
                    }
                    AddLine("<margin=1em>All meteor events:");
                    if (list.Count == 0)
                    {
                        AddLine("<margin=2em>None.");
                    }
                    else
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            CreateMeteorEventLines(i, list[i]);
                        }
                    }
                }
                else
                if (args[1] == "clear")
                {
                    AddLine("<margin=1em>Queued meteor events cleared [" + queue.Count + "].");
                    queue.Clear();
                }
                else
                {
                    try
                    {
                        int n = int.Parse(args[1]);
                        if (n < 0 && n >= list.Count)
                        {
                            AddLine("<margin=1em><color=#FF0000>Meteor event index out of range.");
                        }
                        else
                        {
                            mh.QueueMeteoEvent(list[n]);
                            AddLine("<margin=1em>Meteor event <color=#00FF00>" + list[n].name + "</color> queued.");
                            if (list[n].asteroidEventData != null)
                            {
                                AddLine("<margin=3em>Resources: " + GetAsteroidSpawn(list[n].asteroidEventData));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        var found = false;
                        var name = args[1].ToLowerInvariant();
                        foreach (var me in list)
                        {
                            if (me.name.ToLowerInvariant() == name)
                            {
                                mh.QueueMeteoEvent(me);
                                AddLine("<margin=1em>Meteor event <color=#00FF00>" + me.name + "</color> queued.");
                                if (me.asteroidEventData != null)
                                {
                                    AddLine("<margin=3em>Resources: " + GetAsteroidSpawn(me.asteroidEventData));
                                }
                                found = true;
                            }
                        }

                        if (!found)
                        {
                            AddLine("<margin=1em><color=#FF0000>Meteor event not found.");

                            var candidates = new List<string>();

                            foreach (var me in list)
                            {
                                if (me.name.ToLowerInvariant().Contains(name))
                                {
                                    candidates.Add(me.name);
                                }
                            }
                            if (candidates.Count > 0)
                            {
                                AddLine("<margin=1em><color=#FF0000>Unknown meteor event.</color> Did you mean?");
                                foreach (var line in JoinPerLine(candidates, 5))
                                {
                                    AddLine("<margin=2em>" + line);
                                }

                            }
                        }
                    }
                }
            }
        }

        [Command("/list-items-nearby", "Lists the world object ids and their types within a radius.")]
        public void ItemsNearby(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Lists the world object ids and their types within a radius.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/list-items-nearby radius [typefilter]</color> - List the items with group type name containing the optional typefilter");
            }
            else
            {
                string filter = "";
                if (args.Count > 2)
                {
                    filter = args[2].ToLowerInvariant();
                }
                float radius = float.Parse(args[1], CultureInfo.InvariantCulture);

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var pp = pm.transform.position;

                List<WorldObject> found = [];
                foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects().Values)
                {
                    if (wo.GetIsPlaced() && Vector3.Distance(pp, wo.GetPosition()) < radius)
                    {
                        if (wo.GetGroup().id.ToLowerInvariant().Contains(filter))
                        {
                            found.Add(wo);
                        }
                    }
                }
                found.Sort((a, b) =>
                {
                    var p1 = a.GetPosition();
                    var d1 = Vector3.Distance(pp, p1);

                    var p2 = b.GetPosition();
                    var d2 = Vector3.Distance(pp, p2);

                    return d1.CompareTo(d2);
                });

                AddLine("<margin=1em>Found " + found.Count + " world objects");
                var i = 0;
                foreach (var wo in found) {
                    AddLine("<margin=2em>"
                        + string.Format("{0:00}.  ", i)
                        + wo.GetId() + " - " 
                        + wo.GetGroup().GetId() 
                        + " <color=#00FF00>\"" + Readable.GetGroupName(wo.GetGroup()) 
                        + "\"</color>  @ " + wo.GetPosition() + " (" + string.Format("{0:0.#}", Vector3.Distance(wo.GetPosition(), pp)) + ")");

                    i++;
                }
            }
        }

        [Command("/delete-item", "Deletes a world object specified by its unique id.")]
        public void DeleteItem(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Deletes a world object specified by its unique id.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/delete-item id</color> - Deletes a world object (and its game object) by the given id");
            }
            else
            {
                int id = int.Parse(args[1]);
                bool found = false;
                if (WorldObjectsHandler.Instance.GetWorldObjectViaId(id) != null)
                {
                    WorldObjectsHandler.Instance.DestroyWorldObject(id, true);
                    found = true;
                }

                if (found)
                {
                    AddLine("<margin=1em>World object deleted.");
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>World object not found.");
                }
            }
        }

        [Command("/move-item", "Moves an item to the specified absolute position.")]
        public void MoveItem(List<string> args)
        {
            if (args.Count != 5)
            {
                AddLine("<margin=1em>Moves an item to the specified absolute position.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/move-item id x y z</color> - Moves a world object identified by its id to the position x, y, z");
            }
            else
            {
                int id = int.Parse(args[1]);
                float x = float.Parse(args[2], CultureInfo.InvariantCulture);
                float y = float.Parse(args[3], CultureInfo.InvariantCulture);
                float z = float.Parse(args[4], CultureInfo.InvariantCulture);

                bool found = false;
                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                if (wo != null && wo.GetIsPlaced())
                {
                    wo.SetPositionAndRotation(new Vector3(x, y, z), wo.GetRotation());

                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        go.transform.position = wo.GetPosition();

                        SyncPosition(go.GetComponent<GroupNetworkContainer>());
                    }
                    found = true;
                }
                if (found)
                {
                    AddLine("<margin=1em>World object moved.");
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>World object not found.");
                }
            }
        }

        /// <summary>
        /// We need to piggyback to the GroupNetworkContainer._transformSync,
        /// used when dropping items, to sync the gameobject's transform for us.
        /// </summary>
        /// <param name="gnc"></param>
        void SyncPosition(GroupNetworkContainer gnc)
        {
            if (gnc != null)
            {
                var _syncTransform = (NetworkVariable<bool>)AccessTools.Field(typeof(GroupNetworkContainer), "_transformSync").GetValue(gnc);
                if (_syncTransform != null)
                {
                    _syncTransform.Value = true;
                    gnc.StartCoroutine(SyncPositionDelay(_syncTransform));
                }
            }
        }
        IEnumerator SyncPositionDelay(NetworkVariable<bool> _syncTransform)
        {
            yield return null;
            _syncTransform.Value = false;
        }

        [Command("/move-item-relative", "Moves an item by the specified relative amount.")]
        public void MoveItemRelative(List<string> args)
        {
            if (args.Count != 5)
            {
                AddLine("<margin=1em>Moves an item by the specified relative amount.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/move-item-relative id x y z</color> - Moves a world object identified by its id relative by x, y, z");
            }
            else
            {
                int id = int.Parse(args[1]);
                float x = float.Parse(args[2], CultureInfo.InvariantCulture);
                float y = float.Parse(args[3], CultureInfo.InvariantCulture);
                float z = float.Parse(args[4], CultureInfo.InvariantCulture);

                bool found = false;
                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                if (wo != null && wo.GetIsPlaced())
                {
                    wo.SetPositionAndRotation(wo.GetPosition() + new Vector3(x, y, z), wo.GetRotation());

                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        go.transform.position = wo.GetPosition();

                        SyncPosition(go.GetComponent<GroupNetworkContainer>());
                    }
                    found = true;
                }
                if (found)
                {
                    AddLine("<margin=1em>World object moved.");
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>World object not found.");
                }
            }
        }

        static string MeteoEventDataToString(int idx, MeteoEventData med)
        {
            return string.Format("{0:00}. <color=#00FF00>{1}</color> [{2:#,##0} <= TI <= {3:#,##0}]", 
                idx, 
                med.name,
                med.startTerraformStage?.GetStageStartValue() ?? 0f,
                med.stopTerraformStage?.GetStageStartValue() ?? float.PositiveInfinity);
        }

        static string GetAsteroidSpawn(AsteroidEventData asteroidEventData)
        {
            var list = asteroidEventData.asteroidGameObject?.GetComponent<Asteroid>()?.groupsSelected;

            if (list != null && list.Count != 0)
            {
                return "<color=#00FF00>" + string.Join("</color>, <color=#00FF00>", list.Select(g => g.id).Distinct()) + "</color>";
            }
            return "No resources";
        }

        void CreateMeteorEventLines(int idx, MeteoEventData med)
        {
            AddLine("<margin=2em>" + MeteoEventDataToString(idx, med));
            if (med.asteroidEventData != null)
            {
                AddLine("<margin=3em>Resources: " + GetAsteroidSpawn(med.asteroidEventData));
            }
        }

        [Command("/add-token", "Adds the specified amount to the Trade Token value.")]
        public void AddToken(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Adds the specified amount to the Trade Token value");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/add-token amount</color> - Trade Token += amount");
            }
            else
            {
                var n = float.Parse(args[1], CultureInfo.InvariantCulture);
                TokensHandler.Instance.GainTokens((int)n);
                n = TokensHandler.Instance.GetTokensNumber();
                AddLine("<margin=1em>Trade Tokens updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", n));
            }
        }

        [Command("/set-trade-rocket-delay", "Sets the trading rockets' progress delay in seconds.")]
        public void SetTradeDelay(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Sets the trading rocket's progress delay in seconds. Total rocket time is 100 x this amount.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/set-trade-rocket-delay seconds</color> - Set the progress delay in seconds (fractions allowed)");
                AddLine("<margin=1em>Current trading rocket progress delay: <color=#00FF00>" + string.Format("{0:#,##0.00} s", tradePlatformDelay));
            }
            else
            {
                tradePlatformDelay = float.Parse(args[1], CultureInfo.InvariantCulture);
                
                AddLine("<margin=1em>Trading rocket progress delay updated. Now at <color=#00FF00>" + string.Format("{0:#,##0.00} s", tradePlatformDelay));

                FieldInfo ___updateGrowthEvery = AccessTools.Field(typeof(MachineRocketBackAndForth), "updateGrowthEvery");

                foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
                {
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        var platform = go.GetComponent<MachineRocketBackAndForth>();
                        if (platform != null)
                        {
                            ___updateGrowthEvery.SetValue(platform, tradePlatformDelay);
                        }
                    }
                }
            }
        }

        [Command("/list-golden-containers", "Lists all loaded-in golden containers.")]
        public void ListGoldenContainers(List<string> args)
        {
            int range = 0;
            if (args.Count > 1)
            {
                range = int.Parse(args[1]);
            }

            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var player = pm.transform.position;

            int i = 0;
            var found = new List<WorldObjectFromScene>();
            foreach (var wos in FindObjectsByType<WorldObjectFromScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var gd = wos.GetGroupData();
                if (gd.id == "GoldenContainer")
                {
                    if (i == 0)
                    {
                        AddLine("<margin=1em>Golden Containers found:");
                    }
                    var p = wos.transform.position;
                    var d = Vector3.Distance(player, p);
                    if (range == 0 || d <= range)
                    {
                        found.Add(wos);
                    }
                    i++;
                }
            }
            found.Sort((a, b) =>
            {
                var p1 = a.transform.position;
                var d1 = Vector3.Distance(player, p1);

                var p2 = b.transform.position;
                var d2 = Vector3.Distance(player, p2);

                return d1.CompareTo(d2);
            });

            var j = 0;
            foreach (var wos in found)
            {
                var p = wos.transform.position;
                var d = Vector3.Distance(player, p);
                AddLine(string.Format("<margin=2em>{0:00} @ {1}, Range: {2}, Id: {3}, [{4}]", j, p, (int)d, wos.GetUniqueId(), wos.gameObject.activeSelf));
                j++;
            }

            if (i == 0)
            {
                AddLine("<margin=1em>No containers found.");
            }
        }

        [Command("/list-starform-containers", "Lists all loaded-in Starform containers.")]
        public void ListStarformContainers(List<string> args)
        {
            int range = 0;
            if (args.Count > 1)
            {
                range = int.Parse(args[1]);
            }

            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var player = pm.transform.position;

            var found = new List<WorldObjectFromScene>();
            int i = 0;
            foreach (var wos in FindObjectsByType<WorldObjectFromScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (wos.name.Contains("WorldContainerStarform"))
                {
                    if (i == 0)
                    {
                        AddLine("<margin=1em>Starform Containers found:");
                    }
                    var p = wos.transform.position;
                    var d = Vector3.Distance(player, p);
                    if (range == 0 || d <= range)
                    {
                        found.Add(wos);
                    }
                    i++;
                }
            }

            found.Sort((a, b) =>
            {
                var p1 = a.transform.position;
                var d1 = Vector3.Distance(player, p1);

                var p2 = b.transform.position;
                var d2 = Vector3.Distance(player, p2);

                return d1.CompareTo(d2);
            });

            var j = 0;
            foreach (var wos in found)
            {
                var p = wos.transform.position;
                var d = Vector3.Distance(player, p);
                AddLine(string.Format("<margin=2em>{0:00} @ {1}, Range: {2}, Id: {3}, [{4}]", j, p, (int)d, wos.GetUniqueId(), wos.gameObject.activeSelf));
                j++;
            }

            if (i == 0)
            {
                AddLine("<margin=1em>No containers found.");
            }
        }

        [Command("/list-golden-crams", "Lists all loaded-in golden crams.")]
        public void ListGoldenCrams(List<string> args)
        {
            int range = 0;
            if (args.Count > 1)
            {
                range = int.Parse(args[1]);
            }

            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var player = pm.transform.position;

            int i = 0;
            var found = new List<WorldObjectFromScene>();
            foreach (var wos in FindObjectsByType<WorldObjectFromScene>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var gd = wos.GetGroupData();
                if (gd.id == "ContainerGoldenAqualis")
                {
                    if (i == 0)
                    {
                        AddLine("<margin=1em>Golden Crams found:");
                    }
                    var p = wos.transform.position;
                    var d = Vector3.Distance(player, p);
                    if (range == 0 || d <= range)
                    {
                        found.Add(wos);
                    }
                    i++;
                }
            }
            found.Sort((a, b) =>
            {
                var p1 = a.transform.position;
                var d1 = Vector3.Distance(player, p1);

                var p2 = b.transform.position;
                var d2 = Vector3.Distance(player, p2);

                return d1.CompareTo(d2);
            });

            var j = 0;
            foreach (var wos in found)
            {
                var p = wos.transform.position;
                var d = Vector3.Distance(player, p);
                AddLine(string.Format("<margin=2em>{0:00} @ {1}, Range: {2}, Id: {3}, [{4}]", j, p, (int)d, wos.GetUniqueId(), wos.gameObject.activeSelf));
                j++;
            }

            if (i == 0)
            {
                AddLine("<margin=1em>No containers found.");
            }
        }

        [Command("/load-all-sectors", "Loads all sectors of the world.")]
        public void LoadAllSectors(List<string> _)
        {
            var sectors = FindObjectsByType<Sector>(FindObjectsSortMode.None);
            AddLine("Sector count: " + sectors.Length);
            foreach (Sector sector in sectors)
            {
                if (sector == null || sector.gameObject == null)
                {
                    continue;
                }
                string name = sector.gameObject.name;
                AddLine("<margin=1em>Sector <color=#00FFFF>" + name);

                SceneManager.LoadScene(name, LoadSceneMode.Additive);
            }

            AddLine("<color=#FF8080>Warning! You may want to reload this save to avoid game issues.");
        }

        [Command("/save-stats", "Display save statistics.")]
        public void SaveStats(List<string> _)
        {
            int totalWorldObjects = 0;
            int sceneWorldObjects = 0;
            int placedSceneItems = 0;
            int sceneWorldObjectsInventory = 0;

            int playerWorldObjects = 0;
            int playerStructures = 0;
            int playerPlacedItems = 0;
            int playerWorldObjectsInventory = 0;

            int drones = 0;
            int dronesActive = 0;
            int dronesCarrying = 0;

            foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects().Values)
            {
                totalWorldObjects++;
                if (WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()))
                {
                    sceneWorldObjects++;
                    if (wo.GetIsPlaced())
                    {
                        placedSceneItems++;
                    }
                    if (wo.GetLinkedInventoryId() != 0)
                    {
                        sceneWorldObjectsInventory++;
                    }
                }
                else
                {
                    playerWorldObjects++;
                    if (wo.GetGroup() is GroupConstructible)
                    {
                        playerStructures++;
                    }
                    if (wo.GetGroup() is GroupItem && wo.GetIsPlaced())
                    {
                        playerPlacedItems++;
                    }
                    if (wo.GetLinkedInventoryId() != 0)
                    {
                        playerWorldObjectsInventory++;
                    }
                }

                var gr = wo.GetGroup();
                if (gr is GroupItem && gr.id.StartsWith("Drone"))
                {
                    drones++;
                    if (wo.GetIsPlaced())
                    {
                        dronesActive++;
                    }
                    var inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());
                    if (inv != null)
                    {
                        dronesCarrying += inv.GetInsideWorldObjects().Count;
                    }
                }
            }

            int totalInventories = 0;
            int sceneInventories = 0;
            int playerInventories = 0;
            int totalItemsInInventory = 0;
            int totalInventoryCapacity = 0;

            int supplyItemCount = 0;
            int supplyCapacity = 0;
            int demandItemCount = 0;
            int demandCapacity = 0;
            int supplyInventoryCount = 0;
            int demandInventoryCount = 0;
            int logisticsInventoryCount = 0;
            int reservedDemandSlots = 0;

            foreach (var inv in InventoriesHandler.Instance.GetAllInventories().Values)
            {
                totalInventories++;
                if (inv.GetId() >= 100_000_000)
                {
                    sceneInventories++;
                }
                else
                {
                    playerInventories++;
                    totalItemsInInventory += inv.GetInsideWorldObjects().Count;
                }
                if (GetInventoryCapacity != null)
                {
                    totalInventoryCapacity += GetInventoryCapacity(inv);
                }
                else
                {
                    totalInventoryCapacity += inv.GetSize();
                }

                var le = inv.GetLogisticEntity();
                if (le != null)
                {
                    bool logisticsRelated = false;
                    {
                        var sg = le.GetSupplyGroups();
                        if (sg != null && sg.Count != 0)
                        {
                            supplyInventoryCount++;
                            logisticsRelated = true;

                            if (GetInventoryCapacity != null)
                            {
                                supplyCapacity += GetInventoryCapacity(inv);
                            }
                            else
                            {
                                supplyCapacity += inv.GetSize();
                            }

                            var hash = new HashSet<string>(sg.Select(v => v.id));

                            foreach (var item in inv.GetInsideWorldObjects())
                            {
                                if (hash.Contains(item.GetGroup().id))
                                {
                                    supplyItemCount++;
                                }
                            }
                        }
                    }
                    {
                        var gd = le.GetDemandGroups();
                        if (gd != null && gd.Count != 0)
                        {
                            demandInventoryCount++;
                            logisticsRelated = true;

                            if (GetInventoryCapacity != null)
                            {
                                demandCapacity += GetInventoryCapacity(inv);
                            }
                            else
                            {
                                demandCapacity += inv.GetSize();
                            }

                            var hash = new HashSet<string>(gd.Select(v => v.id));

                            foreach (var item in inv.GetInsideWorldObjects())
                            {
                                if (hash.Contains(item.GetGroup().id))
                                {
                                    demandItemCount++;
                                }
                            }
                        }
                    }
                    if (logisticsRelated)
                    {
                        logisticsInventoryCount++;
                        reservedDemandSlots += le.waitingDemandSlots;
                    }
                }
            }

            var lm = Managers.GetManager<LogisticManager>();
            var alltasks = lm.GetAllCurrentTasks();
            var logisticsTaskCount = alltasks.Count;
            var logisticsTaskUnattributed = 0;
            var logisticsTaskToSupply = 0;
            var logisticsTaskToDemand = 0;
            // var logisticsTaskDone = 0;
            foreach (var lt in alltasks)
            {
                switch (lt.Value.GetTaskState())
                {
                    case LogisticData.TaskState.NotAttributed: {
                            logisticsTaskUnattributed++;
                            break;
                        }
                    case LogisticData.TaskState.ToSupply:
                        {
                            logisticsTaskToSupply++;
                            break;
                        }
                    case LogisticData.TaskState.ToDemand:
                        {
                            logisticsTaskToDemand++;
                            break;
                        }
                        /*
                    case LogisticData.TaskState.Done:
                        {
                            logisticsTaskDone++;
                            break;
                        }
                        */
                }
            }

            AddLine(string.Format("<margin=1em>Total Objects: <color=#00FF00>{0:#,##0}</color>", totalWorldObjects));
            AddLine(string.Format("<margin=1em>   Scene Objects: <color=#00FF00>{0:#,##0}</color>", sceneWorldObjects));
            AddLine(string.Format("<margin=1em>      Placed Items: <color=#00FF00>{0:#,##0}</color>", placedSceneItems));
            AddLine(string.Format("<margin=1em>      Have inventory: <color=#00FF00>{0:#,##0}</color>", sceneWorldObjectsInventory));
            AddLine(string.Format("<margin=1em>   Player Objects: <color=#00FF00>{0:#,##0}</color>", playerWorldObjects));
            AddLine(string.Format("<margin=1em>      Structures: <color=#00FF00>{0:#,##0}</color>", playerStructures));
            AddLine(string.Format("<margin=1em>      Placed Items: <color=#00FF00>{0:#,##0}</color>", playerPlacedItems));
            AddLine(string.Format("<margin=1em>      Have inventory: <color=#00FF00>{0:#,##0}</color>", playerWorldObjectsInventory));
            AddLine(string.Format("<margin=1em>Total Inventories: <color=#00FF00>{0:#,##0}</color>", totalInventories));
            AddLine(string.Format("<margin=1em>   Scene Inventories: <color=#00FF00>{0:#,##0}</color>", sceneInventories));
            AddLine(string.Format("<margin=1em>   Player Inventories: <color=#00FF00>{0:#,##0}</color>", playerInventories));
            AddLine(string.Format("<margin=1em>      Items inside: <color=#00FF00>{0:#,##0}</color>", totalItemsInInventory));
            AddLine(string.Format("<margin=1em>      Capacity: <color=#00FF00>{0:#,##0}</color>", totalInventoryCapacity));
            if (totalInventoryCapacity > 0)
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>{0:#,##0.##} %</color>", totalItemsInInventory * 100d / totalInventoryCapacity));
            }
            else
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>Logistics: <color=#00FF00>{0:#,##0}</color> inventories", logisticsInventoryCount));
            AddLine(string.Format("<margin=1em>   Supply: <color=#00FF00>{0:#,##0}</color>", supplyInventoryCount));
            AddLine(string.Format("<margin=1em>      Items: <color=#00FF00>{0:#,##0}</color>", supplyItemCount));
            AddLine(string.Format("<margin=1em>      Capacity: <color=#00FF00>{0:#,##0}</color>", supplyCapacity));
            AddLine(string.Format("<margin=1em>      Free: <color=#00FF00>{0:#,##0}</color>", supplyCapacity - supplyItemCount));
            if (supplyCapacity > 0)
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>{0:#,##0.##} %</color>", supplyItemCount * 100d / supplyCapacity));
            }
            else
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>   Demand: <color=#00FF00>{0:#,##0}</color>", demandInventoryCount));
            AddLine(string.Format("<margin=1em>      Items: <color=#00FF00>{0:#,##0}</color>", demandItemCount));
            AddLine(string.Format("<margin=1em>      Capacity: <color=#00FF00>{0:#,##0}</color>", demandCapacity));
            AddLine(string.Format("<margin=1em>      Free: <color=#00FF00>{0:#,##0}</color>", demandCapacity - demandItemCount));
            if (demandCapacity > 0)
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>{0:#,##0.##} %</color>", demandItemCount * 100d / demandCapacity));
            }
            else
            {
                AddLine(string.Format("<margin=1em>      Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>      Reserved: <color=#00FF00>{0:#,##0}</color>", reservedDemandSlots));
            AddLine(string.Format("<margin=1em>   Drones: <color=#00FF00>{0:#,##0}</color>", drones));
            AddLine(string.Format("<margin=1em>      Active: <color=#00FF00>{0:#,##0}</color>", dronesActive));
            if (drones > 0)
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>{0:#,##0.##} %</color>", dronesActive * 100d / drones));
            }
            else
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>      Items Carried: <color=#00FF00>{0:#,##0}</color>", dronesCarrying));
            if (dronesActive > 0)
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>{0:#,##0.##} %</color>", dronesCarrying * 100d / dronesActive));
            }
            else
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>   Tasks: <color=#00FF00>{0:#,##0}</color>", logisticsTaskCount));
            AddLine(string.Format("<margin=1em>      Not attributed: <color=#00FF00>{0:#,##0}</color>", logisticsTaskUnattributed));
            if (logisticsTaskCount > 0)
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>{0:#,##0} %</color>", logisticsTaskUnattributed * 100d / logisticsTaskCount));
            }
            else
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>      To Supply: <color=#00FF00>{0:#,##0}</color>", logisticsTaskToSupply));
            if (logisticsTaskCount > 0)
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>{0:#,##0} %</color>", logisticsTaskToSupply * 100d / logisticsTaskCount));
            }
            else
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>N/A</color>"));
            }
            AddLine(string.Format("<margin=1em>      To Demand: <color=#00FF00>{0:#,##0}</color>", logisticsTaskToDemand));
            if (logisticsTaskCount > 0)
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>{0:#,##0} %</color>", logisticsTaskToDemand * 100d / logisticsTaskCount));
            }
            else
            {
                AddLine(string.Format("<margin=1em>         Utilization: <color=#00FF00>N/A</color>"));
            }
        }

        [Command("/set-outside-grower-delay", "Sets the outside growers' progress delay in seconds.")]
        public void SetOutsideGrowerDelay(List<string> args)
        {
            if (args.Count != 2)
            {
                AddLine("<margin=1em>Sets the outside growers' progress delay in seconds.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/set-outside-grower-delay seconds</color> - Set the progress delay in seconds (fractions allowed)");
                AddLine("<margin=1em>Current outside growers' delay: <color=#00FF00>" + string.Format("{0:#,##0.00} s", outsideGrowerDelay));
            }
            else
            {
                outsideGrowerDelay = float.Parse(args[1], CultureInfo.InvariantCulture);

                AddLine("<margin=1em>Outside growers' delay progress delay updated. Now at <color=#00FF00>" + string.Format("{0:#,##0.00} s", outsideGrowerDelay));

                FieldInfo ___updeteInterval = AccessTools.Field(typeof(MachineGrowerVegetationStatic), "growthUpdateInterval");

                foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
                {
                    var go = wo.GetGameObject();
                    if (go != null)
                    {
                        var platform = go.GetComponent<MachineGrowerVegetationStatic>();
                        if (platform != null)
                        {
                            ___updeteInterval.SetValue(platform, outsideGrowerDelay);
                        }
                    }
                }
            }
        }

        [Command("/logistics-item-stats", "Display statistics about a particular item type in the logistics system.")]
        public void LogisticItemStats(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Display statistics about a particular item type in the logistics system.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/logistics-item-stats item-id [planet]</color> - Display the statistics for the item");
            }
            else
            {
                var planetHash = Managers.GetManager<PlanetLoader>().GetCurrentPlanetData().GetPlanetHash();

                if (args.Count >= 3)
                {
                    planetHash = args[2].GetStableHashCode();
                }
                var gr = FindGroup(args[1].ToLowerInvariant());
                if (gr == null)
                {
                    DidYouMean(args[1].ToLowerInvariant(), false, true);
                }
                else
                {
                    var supplyInventoryCount = 0;
                    var supplyItemCount = 0;
                    var supplyCapacity = 0;
                    var supplyFree = 0;
                    var demandInventoryCount = 0;
                    var demandItemCount = 0;
                    var demandCapacity = 0;
                    var demandFree = 0;
                    var demandReserved = 0;

                    foreach (var inv in InventoriesHandler.Instance.GetAllInventories().Values)
                    {
                        var le = inv.GetLogisticEntity();
                        if (le != null && le.GetWorldObject() != null && le.GetWorldObject().GetPlanetHash() == planetHash)
                        {
                            bool isRelated = false;
                            {
                                var sup = le.GetSupplyGroups();
                                if (sup != null 
                                    && sup.Any(g => g == gr))
                                {
                                    supplyInventoryCount++;
                                    isRelated = true;

                                    supplyItemCount += inv.GetInsideWorldObjects()
                                        .Where(g => g.GetGroup() == gr).Count();

                                    var cap = 0;
                                    if (GetInventoryCapacity != null)
                                    {
                                        cap = GetInventoryCapacity(inv);
                                    }
                                    else
                                    {
                                        cap = inv.GetSize();
                                    }
                                    supplyFree += Math.Max(0, cap - inv.GetInsideWorldObjects().Count);
                                    supplyCapacity += cap;
                                }
                            }
                            {
                                var dem = le.GetDemandGroups();
                                if (dem != null 
                                    && dem.Any(g => g == gr))
                                {
                                    isRelated = true;
                                    demandInventoryCount++;
                                    demandItemCount += inv.GetInsideWorldObjects()
                                        .Where(g => g.GetGroup() == gr).Count();

                                    var cap = 0;
                                    if (GetInventoryCapacity != null)
                                    {
                                        cap = GetInventoryCapacity(inv);
                                    }
                                    else
                                    {
                                        cap = inv.GetSize();
                                    }
                                    demandFree += Math.Max(0, cap - inv.GetInsideWorldObjects().Count);
                                    demandCapacity += cap;
                                }
                            }

                            if (isRelated)
                            {
                                demandReserved += le.waitingDemandSlots;
                            }
                        }
                    }

                    var lm = Managers.GetManager<LogisticManager>();
                    var alltasks = lm.GetAllCurrentTasks();

                    var tasks = alltasks.Count;
                    var unassigned = 0;
                    var tosupply = 0;
                    var todemand = 0;

                    foreach (var lt in alltasks)
                    {
                        var wo = lt.Value.GetWorldObjectToMove();
                        if (wo != null && lt.Value.GetPlanetHash() == planetHash)
                        {
                            if (wo.GetGroup() == gr)
                            {
                                switch (lt.Value.GetTaskState())
                                {
                                    case LogisticData.TaskState.NotAttributed:
                                        {
                                            unassigned++;
                                            break;
                                        }
                                    case LogisticData.TaskState.ToSupply:
                                        {
                                            tosupply++;
                                            break;
                                        }
                                    case LogisticData.TaskState.ToDemand:
                                        {
                                            todemand++;
                                            break;
                                        }

                                }
                            }
                        }
                    }
                    var grtask = unassigned + tosupply + todemand;

                    AddLine("<margin=1em><b>ID:</b> <color=#00FF00>" + gr.id);
                    AddLine("<margin=1em><b>Name:</b> <color=#00FF00>" + Readable.GetGroupName(gr));
                    AddLine("<margin=1em><b>Description:</b> <color=#00FF00>" + Readable.GetGroupDescription(gr));
                    AddLine("<margin=1em><b>Logistics Info:</b>");
                    AddLine(string.Format("<margin=1em>   Supply: {0:#,##0} inventories", supplyInventoryCount));
                    AddLine(string.Format("<margin=1em>      Items: {0:#,##0}", supplyItemCount));
                    AddLine(string.Format("<margin=1em>      Capacity: {0:#,##0}", supplyCapacity));
                    AddLine(string.Format("<margin=1em>      Free: {0:#,##0}", supplyFree));
                    if (supplyCapacity > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Utilization: {0:#,##0.##} %", supplyItemCount * 100d / supplyCapacity));
                    }
                    AddLine(string.Format("<margin=1em>   Demand: {0:#,##0} inventories", demandInventoryCount));
                    AddLine(string.Format("<margin=1em>      Items: {0:#,##0}", demandItemCount));
                    AddLine(string.Format("<margin=1em>      Capacity: {0:#,##0}", demandCapacity));
                    AddLine(string.Format("<margin=1em>      Free: {0:#,##0}", demandFree));
                    if (demandCapacity > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Utilization: {0:#,##0.##} %", demandItemCount * 100d / demandCapacity));
                    }
                    AddLine(string.Format("<margin=1em>      Reserved: {0:#,##0}", demandReserved));
                    AddLine(string.Format("<margin=1em>   Tasks: {0:#,##0} total", tasks));
                    AddLine(string.Format("<margin=1em>      Items: {0:#,##0}", grtask));
                    if (tasks > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Usage: {0:#,##0.##} %", grtask * 100d / tasks));
                    }
                    AddLine(string.Format("<margin=1em>      Unassigned: {0:#,##0}", unassigned));
                    if (grtask > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Usage: {0:#,##0.##} %", unassigned * 100d / grtask));
                    }
                    AddLine(string.Format("<margin=1em>      To Supply: {0:#,##0}", tosupply));
                    if (grtask > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Usage: {0:#,##0.##} %", tosupply * 100d / grtask));
                    }
                    AddLine(string.Format("<margin=1em>      To Demand: {0:#,##0}", todemand));
                    if (grtask > 0)
                    {
                        AddLine(string.Format("<margin=1em>         Usage: {0:#,##0.##} %", todemand * 100d / grtask));
                    }

                }
            }
        }

        [Command("/list", "Lists all currently connected players (vanilla command).")]
        public void List(List<string> _)
        {
            for (int i = 0; i < PlayersDataManager.Instance.GetPlayerDataCount(); i++)
            {
                var pd = PlayersDataManager.Instance.GetPlayerDataAtIndex(i);
                AddLine("<margin=1em>" + pd.id + " - " + pd.name);
            }
        }

        [Command("/listAll", "Lists all players ever joined the current world (vanilla command).")]
        public void ListAll(List<string> _)
        {
            if (PlayersDataManager.Instance.IsServer) {
                var pm = Managers.GetManager<PlayersManager>();

                foreach (var pd in pm.GetAllTimeListOfPlayers())
                {
                    AddLine("<margin=1em>" + pd.Key + " - " + pd.Value);
                }

            }
            else {
                AddLine("<margin=1em><color=#FF0000>" + Localization.GetLocalizedString("ChatConsole_PermissionError") + "</color>");
            }
        }

        [Command("/kick", "Kick a specific player identified by its number or name (vanilla command).")]
        public void Kick(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Kick a specific player identified by its number or name (vanilla command).");
                AddLine("<margin=1em>Host only command.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/kick id|name</color> - Kick a player identified by its id or name");
                AddLine("<margin=1em>See also: <color=#FFFF00>/list</color>");
            }
            else
            {
                if (PlayersDataManager.Instance.IsServer)
                {

                    for (int i = 0; i < PlayersDataManager.Instance.GetPlayerDataCount(); i++)
                    {
                        var pd = PlayersDataManager.Instance.GetPlayerDataAtIndex(i);

                        if (pd.id != PlayersDataManager.Instance.OwnerClientId)
                        {
                            if (pd.id.ToString().Equals(args[1], StringComparison.InvariantCultureIgnoreCase)
                                || pd.name.ToString().Equals(args[1], StringComparison.InvariantCultureIgnoreCase)
                            )
                            {
                                AddLine("<margin=1em>Kicked " + pd.id + " - " + pd.name);
                                NetworkManager.Singleton.DisconnectClient(pd.id);
                            }
                        }
                    }
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>" + Localization.GetLocalizedString("ChatConsole_PermissionError") + "</color>");
                }
            }
        }

        [Command("/remove", "Kick a specific player - identified by its number or name - and remove all their data from the world (vanilla command).")]
        public void Remove(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Kick a specific player - identified by its number or name - and remove all their data from the world (vanilla command)");
                AddLine("<margin=1em>Host only command.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/remove id|name</color> - Kick a player and remove all their data");
                AddLine("<margin=1em>See also: <color=#FFFF00>/list</color>");
            }
            else
            {
                if (PlayersDataManager.Instance.IsServer)
                {
                    var save = Managers.GetManager<SavedDataHandler>().GetLastLoadedPlayerData();

                    for (int i = 0; i < PlayersDataManager.Instance.GetPlayerDataCount(); i++)
                    {
                        var pd = PlayersDataManager.Instance.GetPlayerDataAtIndex(i);

                        if (pd.id != PlayersDataManager.Instance.OwnerClientId)
                        {
                            if (pd.id.ToString().Equals(args[1], StringComparison.InvariantCultureIgnoreCase)
                                || pd.name.ToString().Equals(args[1], StringComparison.InvariantCultureIgnoreCase)
                            )
                            {
                                AddLine("<margin=1em>Kicked " + pd.id + " - " + pd.name);
                                NetworkManager.Singleton.DisconnectClient(pd.id);

                                for (int j = save.Count - 1; j >= 0; j--)
                                {
                                    var se = save[j];
                                    if (se.name.Equals(pd.name.ToString(), StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        save.RemoveAt(j);
                                        AddLine("<margin=2em>Save data also removed.");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>" + Localization.GetLocalizedString("ChatConsole_PermissionError") + "</color>");
                }
            }
        }

        [Command("/list-mods", "Lists all or a filtered set of mods.")]
        public void ListMods(List<string> args)
        {
            string filterText = string.Empty;
            if (args.Count > 1)
            {
                filterText = args[1];
            }
            var mods = Chainloader.PluginInfos.Values
                .Where(m => m.Metadata.GUID.Contains(filterText, StringComparison.InvariantCultureIgnoreCase))
                .OrderBy(m => m.Metadata.GUID);

            foreach (var mod in mods)
            {
                AddLine("<margin=1em><color=#FFFFFF>" + mod.Metadata.GUID + "</color> - <color=#FFFF00>" + mod.Metadata.Version + "</color>");
                AddLine("<margin=2em><color=#FFFFFF>" + mod.Metadata.Name + "</color>");
            }
        }

        [Command("/get-portal-time", "Get the time remaining for the currently opened portal.")]
        public void GetInstanceTime(List<string> _)
        {
            var wih = Managers.GetManager<WorldInstanceHandler>();
            if (wih != null)
            {
                var inst = wih.GetOpenedWorldInstanceData();
                if (inst != null)
                {
                    var curr = inst.GetTimeLeft();
                    AddLine("<margin=1em>Portal remaining time <color=#00FF00>"
                        + curr + "s (" + string.Format("{0}:{1:00}", curr / 60, curr % 60) + ")</color>"
                    );
                }
                else
                {
                    AddLine("<margin=1em>No portal is open at the moment.");
                }
            }
            else
            {
                AddLine("<margin=1em><color=#FF0000>Unable to query the current portal.</color>");
            }
        }

        [Command("/set-portal-time", "Set the time remaining for the currently opened portal.")]
        public void SetPortalTime(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Set the time remaining for the currently opened portal.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/set-portal-time seconds</color> - set the time remaining to this new value");
                AddLine("<margin=1em>See also: <color=#FFFF00>/get-portal-time</color>");
            }
            else
            {
                var wih = Managers.GetManager<WorldInstanceHandler>();
                if (wih != null)
                {
                    var inst = wih.GetOpenedWorldInstanceData();
                    if (inst != null)
                    {
                        var curr = inst.GetTimeLeft();
                        var next = int.Parse(args[1]);
                        inst.SetTimeLeft(next);
                        AddLine("<margin=1em>Portal remaining time <color=#00FF00>" 
                            + curr + "s (" + string.Format("{0}:{1:00}", curr / 60, curr % 60) + ")</color> -> <color=#00FF00>"
                            + next + "s (" + string.Format("{0}:{1:00}", next / 60, curr % 60) + ")</color>"
                        );
                    }
                    else
                    {
                        AddLine("<margin=1em>No portal is open at the moment.");
                    }
                }
                else
                {
                    AddLine("<margin=1em><color=#FF0000>Unable to query the current portal.</color>");
                }
            }
        }

        [Command("/ingredients-for", "Spawn in the ingredients to craft/build an item.")]
        public void IngredientsFor(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Spawn in the ingredients to craft/build an item.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/ingredients-for itemid [count]</color> - get the ingredients x count for the given item id");
            }
            else
            {
                {
                    var gid = args[1].ToLowerInvariant();
                    SpaceCraft.Group g = FindGroup(gid);
                    if (g == null)
                    {
                        DidYouMean(gid, true, true);
                    }
                    else
                    {
                        var player = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                        var inv = player.GetPlayerBackpack().GetInventory();

                        var recipe = g.GetRecipe().GetIngredientsGroupInRecipe();

                        int n = 1;
                        if (args.Count > 2)
                        {
                            n = int.Parse(args[2]);
                        }
                        var full = false;
                        foreach (var ri in recipe)
                        {
                            var c = 0;
                            for (int k = 0; k < n; k++)
                            {
                                if (!TryAddToInventory(inv, ri))
                                {
                                    full = true;
                                    break;
                                }
                                c++;
                            }
                            AddLine("<margin=2em>" + c + " x <color=#FFFFFF>" + ri.id + "</color> <color=#00FF00>\"" + Readable.GetGroupName(ri) + "\"");
                            if (full)
                            {
                                break;
                            }
                        }

                        if (full)
                        {
                            AddLine("<color=#FF0000>Inventory full.");
                        }
                        else
                        {
                            if (recipe.Count != 0)
                            {
                                AddLine("<margin=1em>Items added");
                            }
                            else
                            {
                                AddLine("<margin=1em>This item does not have a recipe.");
                            }
                        }
                    }
                }
            }
        }

        [Command("/get-color", "Returns the color of a given world object.")]
        public void GetColor(List<string> args)
        {
            if (args.Count == 1)
            {
                AddLine("<margin=1em>Returns the color of a given world object.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/get-color id</color> - get the color of a given world object");
                AddLine("<margin=1em>See also: <color=#FFFF00>/set-color</color>");
            }
            else
            {
                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(int.Parse(args[1]));
                if (wo != null)
                {
                    var color = wo.GetColor();
                    AddLine("<margin=1em>" + args[1] + ": <color=#00FF00>" + wo.GetGroup().id + " \"" + Readable.GetGroupName(wo.GetGroup()) + "\"");
                    AddLine("<margin=2em>Color: " + FormatColor(color));

                }
                else
                {
                    AddLine("<color=#FF0000>Unknown world object.");
                }
            }
        }

        [Command("/set-color", "Sets the color of a given world object.")]
        public void SetColor(List<string> args)
        {
            if (args.Count <= 2)
            {
                AddLine("<margin=1em>Returns the color of a given world object.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/set-color id #00000000</color> - set the color to an ARGB hex value");
                AddLine("<margin=2em><color=#FFFF00>/set-color id 0 0 0 0</color> - set the color to an ARGB with values between 0-255");
                AddLine("<margin=2em><color=#FFFF00>/set-color id 0.0 0.0 0.0 0.0</color> - set the color to an ARGB with values between 0.0-1.0");
                AddLine("<margin=1em>See also: <color=#FFFF00>/get-color</color>");
            }
            else
            {
                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(int.Parse(args[1]));
                if (wo != null)
                {
                    var color = wo.GetColor();
                    AddLine("<margin=1em>" + args[1] + ": <color=#00FF00>" + wo.GetGroup().id + " \"" + Readable.GetGroupName(wo.GetGroup()) + "\"");
                    AddLine("<margin=2em>Previous color: " + FormatColor(color));

                    if (args.Count == 3 && args[2].Length >= 8)
                    {
                        var colorStr = args[2];
                        if (colorStr.StartsWith('#'))
                        {
                            colorStr = colorStr[..^1];
                        }
                        var a = int.Parse(colorStr[..2], NumberStyles.HexNumber) / 255f;
                        var r = int.Parse(colorStr.Substring(2, 2), NumberStyles.HexNumber) / 255f;
                        var g = int.Parse(colorStr.Substring(4, 2), NumberStyles.HexNumber) / 255f;
                        var b = int.Parse(colorStr.Substring(6, 2), NumberStyles.HexNumber) / 255f;

                        color = new Color(r, g, b, a);
                        UpdateWoColor();
                    }
                    else if (args.Count == 6)
                    {
                        if (args[2].Contains('.'))
                        {
                            var a = float.Parse(args[2], CultureInfo.InvariantCulture);
                            var r = float.Parse(args[3], CultureInfo.InvariantCulture);
                            var g = float.Parse(args[4], CultureInfo.InvariantCulture);
                            var b = float.Parse(args[5], CultureInfo.InvariantCulture);

                            color = new Color(r, g, b, a);
                            UpdateWoColor();
                        }
                        else
                        {
                            var a = int.Parse(args[2]) / 255f;
                            var r = int.Parse(args[3]) / 255f;
                            var g = int.Parse(args[4]) / 255f;
                            var b = int.Parse(args[5]) / 255f;

                            color = new Color(r, g, b, a);
                            UpdateWoColor();
                        }
                    }
                    else
                    {
                        AddLine("<color=#FF0000>Wrong color parameter(s).");
                    }

                    void UpdateWoColor()
                    {
                        wo.SetColor(color);
                        var go = wo.GetGameObject();
                        if (go != null)
                        {
                            var cp = go.GetComponent<ColorProxy>();
                            if (cp != null)
                            {
                                cp.SetColor(color);
                            }
                        }
                        AddLine("<margin=2em>New color: " + FormatColor(color));
                    }
                }
                else
                {
                    AddLine("<color=#FF0000>Unknown world object.");
                }
            }
        }

        string FormatColor(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2} ---- {0}, {1}, {2}, {3} ---- {4:0.0###}, {5:0.0###}, {6:0.0###}, {7:0.0###}",
                                (int)(color.a * 255), (int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255),
                                color.a, color.r, color.g, color.b
                                );
        }

        [Command("/pick", "Returns the first world object id in the line of sight of the player.")]
        public void Pick(List<string> args)
        {
            DoPick(args, false);
        }

        [Command("/pick-all", "Returns all the world object ids in the line of sight of the player.")]
        public void PickAll(List<string> args)
        {
            DoPick(args, true);
        }

        void DoPick(List<string> args, bool all)
        {
            var range = 30f;
            if (args.Count >= 2)
            {
                range = float.Parse(args[1], CultureInfo.InvariantCulture);
            }

            HashSet<int> seen = [];

            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            if (pm != null)
            {
                var ac = pm.GetAimController();
                if (ac != null)
                {
                    var pt = ac.GetAimRay();

                    var hits = Physics.RaycastAll(pt, range, ~LayerMask.GetMask(GameConfig.commonIgnoredAndWater));

                    foreach (var hit in hits)
                    {
                        var go = hit.transform.gameObject;

                        var woa = go.GetComponentInParent<WorldObjectAssociated>()
                            ?? GetComponentInChildren<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            var wo = woa.GetWorldObject();
                            if (wo != null)
                            {
                                if (seen.Add(wo.GetId()))
                                {
                                    AddLine("<margin=1em>" + wo.GetId() + ": <color=#00FF00>"
                                        + wo.GetGroup().id + " \"" + Readable.GetGroupName(wo.GetGroup())
                                        + "\" @ " + string.Format("{0:#.##} m", hit.distance));
                                    if (!all)
                                    {
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (seen.Count == 0)
            {
                AddLine("<color=#FF0000>No world object found.");
            }
        }

        [Command("/locateme", "Displays the player's x, y, z position.")]
        public void LocateMe(List<string> _)
        {
            var pos = Managers.GetManager<PlayersManager>().GetActivePlayerController().transform.position;

            AddLine("<margin=1em>Player at ( "
                    + pos.x.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
        }

        [Command("/spawn-gt", "Spawns genetic trait(s) with a specific type and value.")]
        public void SpawnTrait(List<string> args)
        {
            if (!(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
            {
                AddLine("<margin=1em>This command can't be executed on the client.");
                return;
            }
            if (args.Count < 3)
            {
                AddLine("<margin=1em>Spawns genetic trait(s) with a specific type and value.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/spawn-gt type value [count]</color> - spawn a genetic trait with the type(int), value(int) and optional count of times");
                AddLine("<margin=3em>Type codes:");
                AddLine("<margin=4em>1 - Species, 2 - ColorA, 3 - ColorB, 4 - PatternColor");
                AddLine("<margin=4em>5 - Pattern, 6 - Variant, 7 - Bioluminescence, 8 - Size");
                AddLine("<margin=3em>For the color types, use #RRGGBB hex to specify the value.");
            }
            else
            {
                int type = int.Parse(args[1]);
                int value = 0;
                if (args[2].StartsWith("#"))
                {
                    value = int.Parse(args[2][1..], NumberStyles.HexNumber);
                }
                else
                {
                    value = int.Parse(args[2]);
                }
                int count = 1;
                if (args.Count >= 4)
                {
                    count = int.Parse(args[3]);
                }

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var inv = pm.GetPlayerBackpack().GetInventory();
                var gr = GroupsHandler.GetGroupViaId("GeneticTrait");
                var n = 0;
                for (int i = 0; i < count; i++) {
                    var sb = new StringBuilder(32);
                    sb.Append(gr.id).Append('_');
                    GeneticsGrouping.AppendTraitInfo(type, value, sb);
                    var gid = sb.ToString();
                    if (InventoryCanAdd(inv, gid))
                    {
                        InventoriesHandler.Instance.AddItemToInventory(gr, inv, (success, id) =>
                        {
                            if (!success && id != 0)
                            {
                                WorldObjectsHandler.Instance.DestroyWorldObject(id);
                            }
                            else
                            {
                                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                                SetGeneticTraitInfo(wo, type, value);
                            }
                        });
                        n++;
                    }
                }

                if (n > 0)
                {
                    AddLine("<margin=1em>Genetic Trait: " + type + " - " + ((DataConfig.GeneticTraitType)type) + " (" + value + ") x " + n + " added.");
                }
                if (n != count)
                {
                    AddLine("<margin=1em>Inventory full.");
                }
            }
        }

        [Command("/spawn-dna", "Spawns a DNA sequence with a set of specific traits in slots 1-8.")]
        public void SpawnDNA(List<string> args)
        {
            if (!(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
            {
                AddLine("<margin=1em>This command can't be executed on the client.");
                return;
            }
            if (args.Count < 9)
            {
                AddLine("<margin=1em>Spawns a DNA sequence with a set of specific traits in slots 1-8.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/spawn-dna trait1 .. trait8 [count]</color> - spawn a DNA sequence with the set of traits");
                AddLine("<margin=3em>Traits at slots:");
                AddLine("<margin=4em>1 - Species, 2 - ColorA, 3 - ColorB, 4 - PatternColor");
                AddLine("<margin=4em>5 - Pattern, 6 - Variant, 7 - Bioluminescence, 8 - Size");
                AddLine("<margin=4em>Specify an underscore _ to ignore a trait slot.");
                AddLine("<margin=4em>/spawn-dna 1 1 1 _ _ _ _ _");
                AddLine("<margin=3em>For the color types, use #RRGGBB hex to specify the value.");
            }
            else
            {
                int[] traitSlots = new int[9];
                var count = 1;
                for (int i = 1; i < 9; i++)
                {
                    var s = args[i];
                    if (s != "_")
                    {
                        if (i >= 2 && i <= 4 && s.StartsWith("#"))
                        {
                            traitSlots[i] = int.Parse(s[1..], NumberStyles.HexNumber);
                        }
                        else
                        {
                            traitSlots[i] = int.Parse(s);
                        }
                    }
                    else
                    {
                        traitSlots[i] = -1;
                    }
                }
                if (args.Count >= 10)
                {
                    count = int.Parse(args[9]);
                }

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var playerInventory = pm.GetPlayerBackpack().GetInventory();
                var gr = GroupsHandler.GetGroupViaId("DNASequence");
                var grTrait = GroupsHandler.GetGroupViaId("GeneticTrait");
                var n = 0;

                var gidSb = new StringBuilder(64);
                gidSb.Append("DNASequence");
                for (int k = 1; k < traitSlots.Length; k++)
                {
                    var s = traitSlots[k];
                    gidSb.Append('_');
                    GeneticsGrouping.AppendTraitInfo(k, s, gidSb);
                }
                var gid = gidSb.ToString();

                for (int j = 0; j < count; j++)
                {

                    if (InventoryCanAdd(playerInventory, gid))
                    {
                        var woDna = WorldObjectsHandler.Instance.CreateNewWorldObject(gr, 0, null, true);
                        // we need it to have a position, otherwise AddWorldObjectToInventory() will do nothing
                        woDna.SetPositionAndRotation(new Vector3(0.1f, 0.1f, 0.1f), Quaternion.identity);
                        
                        InventoriesHandler.Instance.CreateNewInventory(8, 0, woDna.GetId(), null, null, woInv =>
                        {
                            for (int k = 1; k < traitSlots.Length; k++)
                            {
                                var type = k;
                                var value = traitSlots[k];
                                if (value >= 0)
                                {
                                    InventoriesHandler.Instance.AddItemToInventory(grTrait, woInv, (success, id) =>
                                    {
                                        if (!success && id != 0)
                                        {
                                            WorldObjectsHandler.Instance.DestroyWorldObject(id);
                                        }
                                        else
                                        {
                                            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                                            SetGeneticTraitInfo(wo, type, value);
                                        }
                                    });
                                }
                            }
                        });

                        InventoriesHandler.Instance.AddWorldObjectToInventory(woDna, playerInventory, false, success =>
                        {
                            Log("SpawnDNA - " + woDna.GetId() + " " + success);
                            if (!success)
                            {
                                WorldObjectsHandler.Instance.DestroyWorldObject(woDna);
                            }
                        });

                        n++;
                    }
                }


                if (n > 0)
                {
                    AddLine("<margin=1em>" + n + " DNA Sequence(s) added.");
                }
                if (n != count)
                {
                    AddLine("<margin=1em>Inventory full.");
                }
            }
        }

        static void SetGeneticTraitInfo(WorldObject wo, int type, int value)
        {
            wo.SetGeneticTraitType((DataConfig.GeneticTraitType)type);
            wo.SetGeneticTraitValue(value);

            if (wo.GetGeneticTraitType() == DataConfig.GeneticTraitType.ColorA
            || wo.GetGeneticTraitType() == DataConfig.GeneticTraitType.ColorB
            || wo.GetGeneticTraitType() == DataConfig.GeneticTraitType.PatternColor)
            {
                wo.SetColor(new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1f));
                wo.SetGeneticTraitValue(value % 6);
            }
        }

        [Command("/tpm", "Teleport to another player in multiplayer")]
        public void TeleportMultiplayer(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Teleport to another player in multiplayer.");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/tpm id</color> - teleport to a player, identified via by its number or name");
                AddLine("<margin=1em>Clients:");
                for (int i = 0; i < PlayersDataManager.Instance.GetPlayerDataCount(); i++)
                {
                    var pd = PlayersDataManager.Instance.GetPlayerDataAtIndex(i);
                    AddLine("<margin=2em>" + pd.id + " - " + pd.name);
                }
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>();
                var ac = pm.GetActivePlayerController();

                foreach (var pc in pm.playersControllers)
                {
                    if (pc.OwnerClientId.ToString() == args[1] || pc.playerName.ToLowerInvariant().CompareTo(args[1].ToLowerInvariant()) == 0)
                    {
                        var pos = pc.transform.position;
                        ac.SetPlayerPlacement(pos, ac.transform.rotation);


                        AddLine("<margin=1em>Teleported to: ( "
                            + pos.x.ToString(CultureInfo.InvariantCulture)
                            + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                            + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                            + " )"
                            + " - " + pc.OwnerClientId 
                            + " - " + pc.playerName
                        );
                        break;
                    }
                }
            }
        }

        [Command("/list-gt", "Lists all available genenetic traits, with optional type filtering.")]
        public void ListGT(List<string> args)
        {
            int typefilter = -1;
            if (args.Count > 1)
            {
                typefilter = int.Parse(args[1]);
            }

            var list = GeneticTraitHandler.Instance.GetAllAvailableTraits();
            AddLine("<margin=1em>Listing all genetic traits: " + list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                GeneticTraitData gt = list[i];

                if (typefilter != -1 && typefilter != (int)gt.traitType)
                {
                    continue;
                }

                AddLine("<margin=1em><color=#FFFF00>" + (i + 1));
                AddLine("<margin=2em>Type: <color=#00FF00>" + gt.traitType);
                AddLine("<margin=2em>Value: <color=#00FF00>" + gt.traitValue);

                var colorStr = ((int)(gt.traitColor.r * 255)).ToString("X2")
                    + ((int)(gt.traitColor.g * 255)).ToString("X2")
                    + ((int)(gt.traitColor.b * 255)).ToString("X2");
                AddLine("<margin=2em>Color: <color=#00FF00>#" + colorStr + "</color>   <color=#" + colorStr + "> \u25A0");
                AddLine("<margin=2em>Loot Chance: <color=#00FF00>" + gt.lootChance);
                AddLine("<margin=2em>Can be looted: <color=#00FF00>" + gt.canBeLooted);
                if (gt.extractedFromGroup != null)
                {
                    AddLine("<margin=2em>Extracted from: <color=#00FF00>" + gt.extractedFromGroup.id +
                        " (" + Localization.GetLocalizedString(GameConfig.localizationGroupNameId + gt.extractedFromGroup.id) + ")");
                }
                else
                {
                    AddLine("<margin=2em>Extracted from: <color=#00FF00>N/A");
                }
            }
        }

        [Command("/list-tech-names", "Lists all technology identifiers, their ingame name and description. First argument to sort by id|name|desc, second argument to filter by contains")]
        public void ListTechDetails(List<string> args)
        {
            var filter = "";
            var sort = "name";
            if (args.Count > 1)
            {
                sort = args[1];
            }
            if (args.Count > 2)
            {
                filter = args[2];
            }
            var gds = new List<SpaceCraft.Group>(GroupsHandler.GetAllGroups());
            if (sort == "id")
            {
                gds.Sort((a, b) => a.id.CompareTo(b.id));
            }
            else if (sort == "desc")
            {
                gds.Sort((a, b) => Readable.GetGroupDescription(a).CompareTo(Readable.GetGroupDescription(b)));
            }
            else
            {
                gds.Sort((a, b) => Readable.GetGroupName(a).CompareTo(Readable.GetGroupName(b)));
            }
            foreach (var gd in gds)
            {
                var g = GroupsHandler.GetGroupViaId(gd.id);
                var gid = g.id;
                var nm = Readable.GetGroupName(g);
                var dsc = Readable.GetGroupDescription(g);
                if (
                    gid.Contains(filter, StringComparison.InvariantCultureIgnoreCase)
                    || nm.Contains(filter, StringComparison.InvariantCultureIgnoreCase)
                    |  nm.Contains(filter, StringComparison.InvariantCultureIgnoreCase)
                )
                {
                    AddLine("<color=#FFFF00>" + gid + "</color>\t<color=#00FF00>" + nm + "</color>\t" + dsc);
                }
            }
        }

        [Command("/clear-all-supply", "Clears all supply settings.")]
        public void ClearAllSupply(List<string> _)
        {
            var i = 0;
            foreach (var inv in InventoriesHandler.Instance.GetAllInventories())
            {
                LogisticEntity logisticEntity = inv.Value.GetLogisticEntity();
                if (logisticEntity.HasSupplyGroups())
                {
                    logisticEntity.ClearSupplyGroups();
                    i++;
                }
            }
            AddLine("<margin=1em><color=#FFFF00>" + i + " inventories updated");
        }

        [Command("/clear-all-demand", "Clears all demand settings.")]
        public void ClearAllDemand(List<string> _)
        {
            var i = 0;
            foreach (var inv in InventoriesHandler.Instance.GetAllInventories())
            {
                LogisticEntity logisticEntity = inv.Value.GetLogisticEntity();
                if (logisticEntity.HasDemandGroups())
                {
                    logisticEntity.ClearDemandGroups();
                    i++;
                }
            }
            AddLine("<margin=1em><color=#FFFF00>" + i + " inventories updated");
        }

        [Command("/list-lootables", "List items that when deconstructed produce a recipe.")]
        public void ListLootables(List<string> _)
        {
            foreach (var gi in GroupsHandler.GetAllGroups())
            {
                if (gi.GetLootRecipeOnDeconstruct())
                {
                    var gid = gi.id;
                    var nm = Readable.GetGroupName(gi);
                    var dsc = Readable.GetGroupDescription(gi);
                    AddLine("<color=#FFFF00>" + gid + "</color>\t<color=#00FF00>" + nm + "</color>\t" + dsc);
                }
            }
        }

        [Command("/spawn-blueprint", "Spawns a blueprint for the specified technology.")]
        public void SpawnRecipe(List<string> args)
        {
            if (!(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer))
            {
                AddLine("<margin=1em>This command can't be executed on the client.");
                return;
            }
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Spawns a blueprint for the specified technology");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/spawn-blueprint identifier</color> spawns the blueprint with the specified technology");
                AddLine("<margin=1em>See also <color=#FFFF00>/list-tech</color> for all identifiers");
            }
            else
            {
                var gr = FindGroup(args[1].ToLowerInvariant());
                if (gr == null)
                {
                    DidYouMean(args[1].ToLowerInvariant(), true, true);
                }
                else
                {
                    var groupViaId = GroupsHandler.GetGroupViaId(Managers.GetManager<UnlockingHandler>().GetBlueprintGroupData().id);
                    WorldObject worldObject = WorldObjectsHandler.Instance.CreateNewWorldObject(groupViaId, 0, null, true);
                    worldObject.SetLinkedGroups([gr]);
                    
                    var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                    var inv = pm.GetPlayerBackpack().GetInventory();
                    InventoriesHandler.Instance.AddItemToInventory(worldObject, inv, resetPositionAndRotation: true, success =>
                    {
                        if (!success)
                        {
                            WorldObjectsHandler.Instance.DestroyWorldObject(worldObject);
                            AddLine("<margin=1em>Inventory full");
                        }
                        else
                        {
                            AddLine("<margin=1em>Item spawned");
                        }
                    });
                }
            }
        }

        [Command("/close", "Closes the dialog.")]
        public void Close(List<string> _)
        {
            DestroyConsoleGUI();
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh != null)
            {
                wh.CloseAllWindows();
            }
        }

        [Command("/open", "Opens a chest or machine the player is looking at")]
        public void Open(List<string> args)
        {
            var range = 30f;
            if (args.Count >= 2)
            {
                range = float.Parse(args[1], CultureInfo.InvariantCulture);
            }

            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            if (pm != null)
            {
                var ac = pm.GetAimController();
                if (ac != null)
                {
                    var pt = ac.GetAimRay();

                    var hits = Physics.RaycastAll(pt, range, ~LayerMask.GetMask(GameConfig.commonIgnoredAndWater));

                    foreach (var hit in hits)
                    {
                        var go = hit.transform.gameObject;

                        var ao = go.GetComponentInParent<ActionOpenable>() ?? go.GetComponentInChildren<ActionOpenable>();

                        if (ao != null)
                        {
                            Close(args);
                            ao.OnAction();
                            return;
                        }
                    }
                }
            }
            AddLine("<margin=1em><color=#FF0000>No openable object in sight in range (" + range + "m)");
        }

        [Command("/open-inventory", "Opens an inventory by its id or owner world object id")]
        public void OpenInventory(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Opens an inventory by its id or owner world object id");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/open-inventory identifier [index]</color> open the inventory by its id or world object id");
                AddLine("<margin=3em>The optional index parameter indicates which sub-inventory to open if present. 0 - main, 1 - first secondary, etc.");
                AddLine("<margin=1em>See also <color=#FFFF00>/list-items-nearby</color> for world objects");
                return;
            }

            int id = int.Parse(args[1]);
            int index = 0;
            if (args.Count >= 3)
            {
                index = int.Parse(args[2]);
            }

            var wo = default(WorldObject);
            var inv = InventoriesHandler.Instance.GetInventoryById(id);
            if (inv == null) 
            {
                wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                if (wo == null)
                {
                    AddLine("<margin=1em><color=#FF0000>Inventory or WorldObject not found.");
                    return;
                }
                var secondaries = wo.GetSecondaryInventoriesId();
                if (index > 0 && secondaries != null && secondaries.Count != 0)
                {
                    int secId = Math.Min(index - 1, secondaries.Count - 1);
                    inv = InventoriesHandler.Instance.GetInventoryById(secondaries[secId]);
                }
                else
                {
                    inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());
                }
            }
            if (inv == null)
            {
                AddLine("<margin=1em><color=#FF0000>World object found but not its inventory.");
                return;
            }
            wo ??= WorldObjectsHandler.Instance.GetWorldObjectForInventory(inv);
            if (wo != null)
            {
                var go = wo.GetGameObject();
                if (go != null)
                {
                    foreach (var ao in go.GetComponentsInChildren<ActionOpenable>(true))
                    {
                        var aof = ao;
                        var ia = ao.GetComponentInParent<InventoryAssociated>();
                        var iap = ao.GetComponentInParent<InventoryAssociatedProxy>();
                        if (ia != null && iap == null)
                        {
                            ia.GetInventory(delegate (Inventory inventory)
                            {
                                if (inventory != null && inventory.GetId() == inv.GetId())
                                {
                                    Close(args);
                                    aof.OnAction();
                                }
                            });
                        }
                        else 
                        if (iap != null)
                        {
                            iap.GetInventory((inventory, worldobject) =>
                            {
                                if (inventory != null && inventory.GetId() == inv.GetId())
                                {
                                    Close(args);
                                    aof.OnAction();
                                }
                            });
                        }
                    }
                    return;
                }
                AddLine("<margin=1em><color=#FF0000>Unable to open inventory, gameobject not found");
            }
            AddLine("<margin=1em><color=#FF0000>Unable to open inventory, worldobject not found");
        }

        [Command("/list-inventories", "Lists all inventory identifiers of a world object")]
        public void ListInventory(List<string> args)
        {
            if (args.Count < 2)
            {
                AddLine("<margin=1em>Lists all inventory identifiers of a world object");
                AddLine("<margin=1em>Usage:");
                AddLine("<margin=2em><color=#FFFF00>/list-inventories identifier</color> lists all inventory identifiers associated with the given world object identifier");
                AddLine("<margin=1em>See also <color=#FFFF00>/list-items-nearby</color> for world objects");
                return;
            }

            var id = int.Parse(args[1]);

            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
            if (wo != null)
            {
                var mainId = wo.GetLinkedInventoryId();
                if (mainId <= 0)
                {
                    AddLine("<margin=1em>No inventory");
                    return;
                }
                AddLine("<margin=1em>00. Inventory Id: " + mainId);
                var secIds = wo.GetSecondaryInventoriesId() ?? [];
                var i = 1;
                foreach (var id2 in secIds)
                {
                    AddLine(string.Format("<margin=1em>{0:00}. Inventory Id: {1}", i, id2));
                    i++;
                }
            }
            else
            {
                AddLine("<margin=1em><color=#FF0000>Unknown world object.");
            }
        }

        [Command("/fly", "Toggles fly mode on or off")]
        public void Fly(List<string> _)
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var pmm = pm.GetPlayerMovable();
            pmm.SetFlyMode(!pmm.GetFlyMode());
            AddLine("<margin=1em>Fly mode is " + (pmm.GetFlyMode() ? "ON" : "OFF"));
        }

        [Command("/noclip", "Toggles no clipping on or off with optional movement speed override.")]
        public void NoClip(List<string> args)
        {
            if (args.Count >= 2)
            {
                noClipBaseSpeed = float.Parse(args[1], CultureInfo.InvariantCulture);
            }
            else
            {
                noClipBaseSpeed = 0;
            }

            PlayerMovable pmm = Managers.GetManager<PlayersManager>()
                    .GetActivePlayerController()
                    .GetPlayerMovable();
            var cc = pmm
                .GetComponent<CharacterController>();
            cc.detectCollisions = !cc.detectCollisions;
            cc.enabled = !cc.enabled;
            AddLine("<margin=1em>Noclip is " + (cc.detectCollisions ? "OFF" : "ON"));
        }

        // ***********************************************************************************************
        // Hooks
        // ***********************************************************************************************

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.CloseAllWindows))]
        static bool WindowsHandler_CloseAllWindows()
        {
            // by default, Enter toggles any UI. prevent this while our console is open
            return background == null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowTextInput), nameof(UiWindowTextInput.OnClose))]
        static void UiWindowTextInput_OnClose()
        {
            suppressCommandConsoleKey = true;
            me.StartCoroutine(ConsoleKeyUnlocker());
        }

        static IEnumerator ConsoleKeyUnlocker()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            suppressCommandConsoleKey = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineRocketBackAndForth), nameof(MachineRocketBackAndForth.SetInventoryRocketBackAndForth))]
        static void MachineRocketBackAndForth_SetInventoryRocketBackAndForth(ref float ___updateGrowthEvery)
        {
            ___updateGrowthEvery = tradePlatformDelay;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrowerVegetationStatic), nameof(MachineGrowerVegetationStatic.SetGrowerInventory))]
        static void MachineGrowerVegetationStatic_SetGrowerInventory(ref float ___growthUpdateInterval)
        {
            ___growthUpdateInterval = outsideGrowerDelay;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void FontWorkaround()
        {
            if (fontAsset == null)
            {
                foreach (LocalizedText ltext in FindObjectsByType<LocalizedText>(FindObjectsSortMode.None))
                {
                    if (ltext.textId == "Newsletter_Button")
                    {
                        fontAsset = ltext.GetComponent<TMP_Text>().font;
                        fontMargin = 10;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Vanilla opens a chat window when pressing an enter. We take over that
        /// functionality completely.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), "OnOpenChatWindow")]
        static bool PlayerInputDispatcher_OnOpenChatWindow()
        {
            if (modEnabled.Value)
            {
                Log("Vanilla Chat Window Suppressed");
            }
            return !modEnabled.Value;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowChat), "OnTextReceived")]
        static void UiWindowChat_OnTextReceived(UiWindowChat __instance, ref PlayerData? player, string message)
        {
            if (modEnabled.Value)
            {
                AddLine(player != null
                    ? string.Format("<b><i>{0}</b></i>: {1}", player.Value.name, message)
                    : ("<i>" + message + "</i>")
                    );

                if (background != null)
                {
                    __instance.gameObject.SetActive(false);
                    me.CreateOutputLines();
                }
            }
        }

        // Do not emote while the command console is open by pressing 1-9
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerThirdPersonView), "ShortcutEmote")]
        static bool PlayerThirdPersonView_ShortcutEmote()
        {
            return background == null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            worldLoadCount = 0;
            noClipBaseSpeed = 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMovable), nameof(PlayerMovable.InputOnUnstuck))]
        static bool PlayerMovable_InputOnUnstuck(PlayerMovable __instance, ref float ___m_Fall)
        {
            if (!unstuckDirectional.Value)
            {
                return true;
            }

            var pm = __instance.gameObject.GetComponent<PlayerMainController>();
            ___m_Fall = 0f;

            if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
            {
                pm.SetPlayerPlacement(__instance.transform.position - Vector3.up * unstuckDistance.Value, __instance.transform.rotation, false);
            }
            else
            if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed)
            {
                pm.SetPlayerPlacement(__instance.transform.position + Camera.main.transform.forward * unstuckDistance.Value, __instance.transform.rotation, false);
            }
            else
            {
                pm.SetPlayerPlacement(__instance.transform.position + Vector3.up * unstuckDistance.Value, __instance.transform.rotation, false);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMovable), nameof(PlayerMovable.UpdatePlayerMovement))]
        static bool PlayerMovable_UpdatePlayerMovement(
            PlayerMovable __instance,
            CharacterController ___m_Controller,
            PlayerCanAct ___playerCanAct,
            Vector2 ___lastMoveAxis,
            float ___moveSpeedChangePercentage,
            float ___runActionValue,
            bool ___autoForward)
        {
            if (___m_Controller.detectCollisions)
            {
                return true;
            }

            if (!___playerCanAct.GetCanMove())
            {
                return false;
            }

            var vector = Camera.main.transform.forward;
            float num = __instance.RunSpeed;
            var moveSpeed = __instance.MoveSpeed;
            if (noClipBaseSpeed > 0f)
            {
                moveSpeed = noClipBaseSpeed;
                num = noClipBaseSpeed * 1.5f;
            }
            if (__instance.flyMode)
            {
                num = 250f;
            }
            if (__instance.GetComponent<PlayerEffects>()
                .GetActiveFirstEffectOfType(DataConfig.EffectOnPlayerType.IncreaseSpeed) != null)
            {
                num *= 1.5f;
            }
            Vector2 vector2 = ___lastMoveAxis;
            if (___autoForward)
            {
                vector2.y = 1f;
            }
            float num6 = num + num * ___moveSpeedChangePercentage / 100f;
            float num7 = ((___runActionValue > 0f) ? num6 : moveSpeed);
            Vector3 vector3 = (vector * vector2.y + __instance.transform.right * vector2.x) * num7;

            __instance.GetComponent<PlayerMainController>()
                .SetPlayerPlacement(__instance.transform.position + vector3 * Time.deltaTime, __instance.transform.rotation, false);

            return false;
        }


        // oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo

        void Colorize(List<string> list, string color)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = "<color=" + color + ">" + list[i] + "</color>";
            }
        }

        void Bolden(List<string> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = "<b>" + list[i] + "</b>";
            }
        }

        List<string> JoinPerLine(List<string> items, int perLine)
        {
            var result = new List<string>();

            string str = "";
            for (int i = 0; i < items.Count; i++)
            {
                if (str.Length != 0)
                {
                    str += ", ";
                }
                str += items[i];
                if ((i + 1) % perLine == 0)
                {
                    result.Add(str);
                    str = "";
                }
            }
            if (str.Length != 0)
            {
                result.Add(str);
            }
            return result;
        }

        static List<string> FindSimilar(string userText, IEnumerable<string> texts)
        {
            List<string> result = [];
            foreach (var text in texts)
            {
                if (text.Contains(userText, StringComparison.InvariantCultureIgnoreCase))
                {
                    result.Add(text);
                }
                else
                {
                    var nameLocalized = Localization.GetLocalizedString(GameConfig.localizationGroupNameId + text) ?? "";
                    if (nameLocalized.Contains(userText, StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Add(text);
                    }
                }
            }
            return result;
        }

        class RemoveCommandRegistry : IDisposable
        {
            string name;
            internal RemoveCommandRegistry(string name)
            {
                this.name = name;
            }

            public void Dispose()
            {
                var name = Interlocked.Exchange(ref this.name, null);
                if (name != null)
                {
                    commandRegistry.Remove(name);
                }
            }
        }
        class CommandRegistryEntry
        {
            internal string description;
            internal Action<List<string>> method;
            internal bool standard;
        }
    }

    /// <summary>
    /// The custom attribute to mark command methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class Command(string name, string description) : Attribute
    {
        public string Name { get; set; } = name;
        public string Description { get; set; } = description;
    }

}
