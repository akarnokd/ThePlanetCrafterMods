using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Diagnostics;
using MijuTools;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System;
using System.Text.RegularExpressions;
using System.Reflection;

namespace FeatCommandConsole
{
    // Credits to Aedenthorn's Spawn Object mod, used it as a guide to create an in-game interactive window
    // because so far, I only did overlays or modified existing windows
    // https://github.com/aedenthorn/PlanetCrafterMods/blob/master/SpawnObject/BepInExPlugin.cs

    [BepInPlugin("akarnokd.theplanetcraftermods.featcommandconsole", "(Feat) Command Console", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;
        static ConfigEntry<string> toggleKey;
        static ConfigEntry<int> consoleTop;
        static ConfigEntry<int> consoleLeft;
        static ConfigEntry<int> consoleRight;
        static ConfigEntry<int> consoleBottom;
        static ConfigEntry<int> fontSize;

        static GameObject canvas;
        static GameObject background;
        static GameObject separator;
        static GameObject inputField;
        static TMP_InputField inputFieldText;
        static readonly List<GameObject> outputFieldLines = new();
        static List<string> consoleText = new();
        static TMP_FontAsset fontAsset;

        static InputAction toggleAction;

        class CommandRegistryEntry
        {
            internal string description;
            internal Action<List<string>> method;
        }

        static readonly Dictionary<string, CommandRegistryEntry> commandRegistry = new();

        void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Enable this mod");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable the detailed logging of this mod");
            toggleKey = Config.Bind("General", "ToggleKey", "<Keyboard>/enter", "Key to open the console");

            consoleTop = Config.Bind("General", "ConsoleTop", 200, "Console window's position relative to the top of the screen.");
            consoleLeft = Config.Bind("General", "ConsoleLeft", 300, "Console window's position relative to the left of the screen.");
            consoleRight = Config.Bind("General", "ConsoleRight", 200, "Console window's position relative to the right of the screen.");
            consoleBottom = Config.Bind("General", "ConsoleBottom", 200, "Console window's position relative to the bottom of the screen.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size in the console");

            if (!toggleKey.Value.Contains("<"))
            {
                toggleKey.Value = "<Keyboard>/" + toggleKey.Value;
            }
            toggleAction = new InputAction(name: "Open console", binding: toggleKey.Value);
            toggleAction.Enable();

            log("   Get resource");
            Font osFont = null;

            foreach (var fp in Font.GetPathsToOSFonts())
            {
                if (fp.ToLower().Contains("arial.ttf"))
                {
                    osFont = new Font(fp);
                    log("      Found font at " + fp);
                    break;
                }
            }

            log("   Set asset");
            fontAsset = TMP_FontAsset.CreateFontAsset(osFont);

            createWelcomeText();

            log("Setting up command registry");
            foreach (MethodInfo mi in typeof(Plugin).GetMethods())
            {
                var ca = mi.GetCustomAttribute<Command>();
                if (ca != null)
                {
                    commandRegistry[ca.name] = new CommandRegistryEntry
                    {
                        description = ca.description,
                        method = (list => mi.Invoke(this, new object[] { list }))
                    };

                    log("  " + ca.name + " - " + ca.description);
                }
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void log(object o)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(o);
            }
        }

        static void createWelcomeText()
        {
            consoleText.Add("Welcome to <b>Command Console</b>.");
            consoleText.Add("Type in <b><color=#FFFF00>/help</color></b> to list the available commands.");
            consoleText.Add("");
        }

        void DestroyConsoleGUI()
        {
            log("DestroyConsoleGUI");

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
                log("No UI should be open, closing GUI");
                DestroyConsoleGUI();
                return;
            }

            if (!modEnabled.Value)
            {
                return;
            }
            if (wh.GetHasUiOpen() && background != null)
            {
                if (Keyboard.current[Key.Escape].wasPressedThisFrame)
                {
                    log("Escape pressed. closing GUI");
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
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    inputFieldText.ActivateInputField();
                }
                return;
            }

            if (!toggleAction.WasPressedThisFrame() || background != null)
            {
                return;
            }

            canvas = new GameObject("CommandConsoleCanvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.transform.SetAsLastSibling();

            log("Creating the background");

            int panelWidth = Screen.width - consoleLeft.Value - consoleRight.Value;
            int panelHeight = Screen.height - consoleTop.Value - consoleBottom.Value;

            int panelX = -Screen.width / 2 + consoleLeft.Value + panelWidth / 2;
            int panelY = Screen.height / 2 - consoleTop.Value - panelHeight / 2;

            RectTransform rect;

            background = new GameObject("CommandConsoleBackground");
            background.transform.parent = canvas.transform;
            var img = background.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(panelX, panelY, 0); // new Vector3(-Screen.width / 2 + consoleLeft.Value, Screen.height / 2 - consoleTop.Value, 0);
            rect.sizeDelta = new Vector2(panelWidth, panelHeight);

            separator = new GameObject("CommandConsoleSeparator");
            separator.transform.parent = background.transform;
            img = separator.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -panelHeight / 2 + (5 + fontSize.Value), 0);
            rect.sizeDelta = new Vector2(panelWidth - 10, 2);

            // ---------------------------------------------

            log("Creating the text field");
            inputField = new GameObject("CommandConsoleInput");
            inputField.transform.parent = background.transform;

            log("   Create TMP_InputField");
            inputFieldText = inputField.AddComponent<TMP_InputField>();
            log("   Create TextMeshProUGUI");
            var txt = inputField.AddComponent<TextMeshProUGUI>();
            inputFieldText.textComponent = txt;

            log("   Set asset");
            inputFieldText.fontAsset = fontAsset;
            log("   Set set pointSize");
            inputFieldText.pointSize = fontSize.Value;
            log("   Set text");
            //inputFieldText.text = "example...";
            inputFieldText.enabled = true;

            log("   Set position");
            rect = inputField.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -panelHeight / 2 + (5 + fontSize.Value) / 2, 0);
            rect.sizeDelta = new Vector2(panelWidth - 10, 5 + fontSize.Value);

            createOutputLines(panelWidth, panelHeight);

            log("Patch in the custom text window");
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.TextInput;

            log("Activating the field");
            inputFieldText.Select();
            inputFieldText.ActivateInputField();
            log("Done");
        }

        void createOutputLines(int panelWidth, int panelHeight)
        {
            // Clear previous lines
            foreach (var go in outputFieldLines)
            {
                Destroy(go);
            }
            outputFieldLines.Clear();

            log("Set output lines");
            int outputY = -panelHeight / 2 + (5 + fontSize.Value) * 3 / 2;

            int j = 0;
            for (int i = consoleText.Count - 1; i >= 0; i--)
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
                outputFieldText.enableWordWrapping = false;
                outputFieldText.text = line;

                var rect = outputFieldText.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(0, outputY, 0);
                rect.sizeDelta = new Vector2(panelWidth - 10, 5 + fontSize.Value);

                outputFieldLines.Add(outputField);

                j++;
                outputY += (5 + fontSize.Value);
            }
        }

        void ExecuteConsoleCommand(string text)
        {
            log("Debug executing command: " + text);
            text = text.Trim();
            if (text.Length == 0)
            {
                return;
            }
            consoleText.Add("<color=#FFFF00><noparse>" + text.Replace("</noparse>", "") + "</noparse></color>");

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
                    }
                }
                else
                {
                    addLine("<color=#FF0000>Unknown command</color>");
                }

            }
            int panelWidth = Screen.width - consoleLeft.Value - consoleRight.Value;
            int panelHeight = Screen.height - consoleTop.Value - consoleBottom.Value;
            createOutputLines(panelWidth, panelHeight);

            inputFieldText.text = "";
            inputFieldText.Select();
            inputFieldText.ActivateInputField();
        }

        List<string> ParseConsoleCommand(string text)
        {
            return new(Regex.Split(text, "\\s+"));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.CloseAllWindows))]
        static bool WindowsHandler_CloseAllWindows()
        {
            //log(Environment.StackTrace);
            //log("Background != null:  " + (background != null));
            // by default, Enter toggles any UI. prevent this while our console is open
            return background == null;
        }

        static void addLine(string line)
        {
            consoleText.Add(line);            
        }

        // oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
        // command methods
        // oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo

        [Command("/help", "Displays the list of commands or their own descriptions")]
        public void Help(List<string> args)
        {
            if (args.Count == 1)
            {
                HelpListCommands();
            }
            else
            {
                commandRegistry.TryGetValue(args[1], out var cmd);
                if (cmd == null) {
                    commandRegistry.TryGetValue("/" + args[1], out cmd);
                }
                if (cmd != null)
                {
                    log("Help for " + args[1] + " - " + cmd.description);
                    addLine("<margin=1em>" + cmd.description);
                }
                else
                {
                    addLine("<margin=1em><color=#FFFF00>Unknown command");
                }
            }
        }

        [Command("/clear", "Clears the console history")]
        public void Clear(List<string> args)
        {
            consoleText.Clear();
        }
        [Command("/spawn", "Spawns an item and adds them to the player inventory.")]
        public void Spawn(List<string> args)
        {
            if (args.Count == 1)
            {
                addLine("<margin=1em>Spawn item(s) or list items that can be possibly spawn");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/spawn list [name-prefix]</color> - list the item ids that can be spawn");
                addLine("<margin=2em><color=#FFFF00>/spawn itemid [amount]</color> - spawn the given item by the given amount");
            } else
            {
                if (args[1] == "list")
                {
                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower();
                    }
                    List<string> possibleSpawns = new();
                    foreach (var g in GroupsHandler.GetAllGroups())
                    {
                        if (g is GroupItem gi && gi.GetId().ToLower().StartsWith(prefix))
                        {
                            possibleSpawns.Add(gi.GetId());
                        }
                    }
                    possibleSpawns.Sort();
                    Colorize(possibleSpawns, "#00FF00");
                    foreach (var line in joinPerLine(possibleSpawns, 5))
                    {
                        addLine("<margin=1em>" + line);
                    }
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

                    var gid = args[1].ToLower();
                    SpaceCraft.Group g = null;

                    foreach (var gr in GroupsHandler.GetAllGroups())
                    {
                        if (gr.GetId().ToLower() == gid)
                        {
                            g = gr;

                        }
                    }

                    if (g == null)
                    {
                        addLine("<color=#FF0000>Unknown item.");
                    }
                    else if (!(g is GroupItem))
                    {
                        addLine("<color=#FF0000>This item can't be spawned.");
                    }
                    else
                    {
                        var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                        var inv = pm.GetPlayerBackpack().GetInventory();
                        int added = 0;
                        for (int i = 0; i < count; i++)
                        {
                            var wo = WorldObjectsHandler.CreateNewWorldObject(g);

                            if (!inv.AddItem(wo))
                            {
                                WorldObjectsHandler.DestroyWorldObject(wo);
                                break;
                            }
                            added++;
                        }

                        if (added == count)
                        {
                            addLine("<margin=1em>Items added");
                        }
                        else if (added > 0)
                        {
                            addLine("<margin=1em>Some items added (" + added + "). Inventory full.");
                        }
                        else
                        {
                            addLine("<margin=1em>Inventory full.");
                        }
                    }
                }
            }
        }

        void HelpListCommands()
        {

            addLine("Available commands:");
            var list = new List<string>();
            foreach (var kv in commandRegistry)
            {
                list.Add(kv.Key);
            }
            list.Sort();
            Colorize(list, "#FFFFFF");
            foreach (var line in joinPerLine(list, 10))
            {
                addLine("<margin=1em>" + line);
            }
            addLine("Type <b><color=#FFFF00>/help [command]</color></b> to get specific command info.");
        }

        void Colorize(List<string> list, string color)
        {
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = "<color=" + color + ">" + list[i] + "</color>";
            }
        }

        List<string> joinPerLine(List<string> items, int perLine)
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
    }

    /// <summary>
    /// The custom attribute to mark command methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class Command : Attribute
    {
        public string name { get; set; }
        public string description { get; set; }

        public Command(string name, string description)
        {
            this.name = name;
            this.description = description;
        }
    }
}
