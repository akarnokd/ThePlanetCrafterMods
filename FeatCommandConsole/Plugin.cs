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
using System.Threading;
using System.Globalization;
using System.IO;

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

        static int scrollOffset;
        static int commandHistoryIndex;
        static readonly List<string> commandHistory = new();

        static InputAction toggleAction;

        static readonly Dictionary<string, CommandRegistryEntry> commandRegistry = new();

        static Dictionary<string, Vector3> savedTeleportLocations;

        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
        // API
        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

        public static IDisposable RegisterCommand(string name, string description, Action<List<string>> action)
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
                method = action,
                standard = false
            };
            return new RemoveCommandRegistry(name);
        }

        // xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

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
                        method = (list => mi.Invoke(this, new object[] { list })),
                        standard = true
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
            consoleText.Add("<i>Use the <b><color=#FFFFFF>Up/Down Arrow</color></b> to cycle command history.</i>");
            consoleText.Add("<i>Use the <b><color=#FFFFFF>Mouse Wheel</color></b> to scroll up/down the output.</i>");
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
                var ms = Mouse.current.scroll.ReadValue();
                if (ms.y != 0)
                {
                    log(" Scrolling " + ms.y);
                    if (ms.y > 0)
                    {
                        scrollOffset = Math.Min(consoleText.Count - 1, scrollOffset + 1);
                    }
                    else
                    {
                        scrollOffset = Math.Max(0, scrollOffset - 1);
                    }
                    createOutputLines();
                }
                if (Keyboard.current[Key.UpArrow].wasPressedThisFrame)
                {
                    log("UpArrow, commandHistoryIndex = " + commandHistoryIndex + ", commandHistory.Count = " + commandHistory.Count);
                    if (commandHistoryIndex < commandHistory.Count)
                    {
                        commandHistoryIndex++;
                        inputFieldText.text = commandHistory[commandHistory.Count - commandHistoryIndex];
                        inputFieldText.ActivateInputField();
                    }
                }
                if (Keyboard.current[Key.DownArrow].wasPressedThisFrame)
                {
                    log("DownArrow, commandHistoryIndex = " + commandHistoryIndex + ", commandHistory.Count = " + commandHistory.Count);
                    commandHistoryIndex = Math.Max(0, commandHistoryIndex - 1);
                    if (commandHistoryIndex > 0 )
                    {
                        inputFieldText.text = commandHistory[commandHistory.Count - commandHistoryIndex];
                    }
                    else
                    {
                        inputFieldText.text = "";
                    }
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

            createOutputLines();

            log("Patch in the custom text window");
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.TextInput;

            log("Activating the field");
            inputFieldText.Select();
            inputFieldText.ActivateInputField();
            log("Done");
        }

        void createOutputLines()
        {
            int panelWidth = Screen.width - consoleLeft.Value - consoleRight.Value;
            int panelHeight = Screen.height - consoleTop.Value - consoleBottom.Value;

            // Clear previous lines
            foreach (var go in outputFieldLines)
            {
                Destroy(go);
            }
            outputFieldLines.Clear();

            log("Set output lines");
            int outputY = -panelHeight / 2 + (5 + fontSize.Value) * 3 / 2;

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
            commandHistory.Add(text);

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
            scrollOffset = 0;
            createOutputLines();

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

        void HelpListCommands()
        {
            addLine("Available commands:");
            var list = new List<string>();
            foreach (var kv in commandRegistry)
            {
                list.Add(kv.Key);
            }
            list.Sort();
            Colorize(list, "#FFFF00");
            Bolden(list);
            foreach (var line in joinPerLine(list, 10))
            {
                addLine("<margin=2em>" + line);
            }
            addLine("<margin=1em>Type <b><color=#FFFF00>/help [command]</color></b> to get specific command info.");
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

        [Command("/tp", "Teleport to a user-named location or an x, y, z position")]
        public void Teleport(List<string> args)
        {
            if (args.Count != 2 && args.Count != 4)
            {
                addLine("<margin=1em>Teleport to a user-named location or an x, y, z position");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tp location-name</color> - teleport to location-name");
                addLine("<margin=2em><color=#FFFF00>/tp x y z</color> - teleport to a specific coordinate");
                addLine("<margin=1em>See also <color=#FFFF00>/tp-create</color>, <color=#FFFF00>/tp-list</color>, <color=#FFFF00>/tp-remove</color>, ");
            }
            else
            if (args.Count == 2)
            {
                if (TryGetSavedTeleportLocation(args[1], out var pos))
                {
                    var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                    pm.SetPlayerPlacement(pos, pm.transform.rotation);
                }
                else
                {
                    addLine("<margin=1em><color=#FF0000>Unknown location.");
                    addLine("<margin=1em>Use <b><color=#FFFF00>/tp-list</color></b> to get all known named locations.");
                }
            }
            else
            {
                var x = float.Parse(args[1], CultureInfo.InvariantCulture);
                var y = float.Parse(args[2], CultureInfo.InvariantCulture);
                var z = float.Parse(args[3], CultureInfo.InvariantCulture);

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);
            }
        }

        [Command("/tp-list", "List all known user-named teleport locations; can specify name prefix")]
        public void TeleportList(List<string> args)
        {
            EnsureTeleportLocations();
            if (savedTeleportLocations.Count != 0)
            {
                List<string> tpNames = new List<string>();
                string prefix = "";
                if (args.Count >= 2)
                {
                    prefix = args[1].ToLower();
                }
                foreach (var n in savedTeleportLocations.Keys)
                {
                    if (n.ToLower().StartsWith(prefix))
                    {
                        tpNames.Add(n);
                    }
                }
                tpNames.Sort();
                foreach (var tpName in tpNames)
                {
                    var pos = savedTeleportLocations[tpName];
                    addLine("<margin=1em><color=#00FF00>" + tpName + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
            }
            else
            {
                addLine("<margin=1em>No user-named teleport locations known");
                addLine("<margin=1em>Use <b><color=#FFFF00>/tp-create</color></b> to create one for the current location.");
            }
        }

        [Command("/tp-create", "Save the current player location as a named location")]
        public void TeleportCreate(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Save the current player location as a named location");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tp-create location-name</color> - save the current location with the given name");
            } else
            {
                EnsureTeleportLocations();
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                savedTeleportLocations[args[1]] = pm.transform.position;
                PersistTeleportLocations();
            }
        }

        [Command("/tp-remove", "Remove a user-named teleport location")]
        public void TeleportRemove(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Remove the specified user-named teleport location");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tp-remove location-name</color> - save the current location with the given name");
                addLine("<margin=1em>See also <color=#FFFF00>/tp-list</color>.");
            }
            else
            {
                EnsureTeleportLocations();
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                if (savedTeleportLocations.Remove(args[1]))
                {
                    PersistTeleportLocations();
                }
                else
                {
                    addLine("<margin=1em><color=#FF0000>Unknown location");
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
                savedTeleportLocations = new();

                string filename = string.Format("{0}/{1}.json", Application.persistentDataPath, "CommandConsole_Locations.txt");

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
                            log(ex);
                        }
                    }
                }
            }
        }

        static void PersistTeleportLocations()
        {
            if (savedTeleportLocations != null)
            {
                string filename = string.Format("{0}/{1}.json", Application.persistentDataPath, "CommandConsole_Locations.txt");

                List<string> lines = new();

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
