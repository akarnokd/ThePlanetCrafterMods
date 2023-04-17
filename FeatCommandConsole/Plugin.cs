using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections;

namespace FeatCommandConsole
{
    // Credits to Aedenthorn's Spawn Object mod, used it as a guide to create an in-game interactive window
    // because so far, I only did overlays or modified existing windows
    // https://github.com/aedenthorn/PlanetCrafterMods/blob/master/SpawnObject/BepInExPlugin.cs

    [BepInPlugin("akarnokd.theplanetcraftermods.featcommandconsole", "(Feat) Command Console", PluginInfo.PLUGIN_VERSION)]
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
        static ConfigEntry<string> fontName;

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

        const int similarLimit = 3;

        static IEnumerator autorefillCoroutine;

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
            fontName = Config.Bind("General", "FontName", "arial.ttf", "The font name in the console");

            if (!toggleKey.Value.Contains("<"))
            {
                toggleKey.Value = "<Keyboard>/" + toggleKey.Value;
            }
            toggleAction = new InputAction(name: "Open console", binding: toggleKey.Value);
            toggleAction.Enable();

            log("   Get resource");
            Font osFont = null;

            var fn = fontName.Value.ToLower(CultureInfo.InvariantCulture);

            foreach (var fp in Font.GetPathsToOSFonts())
            {
                if (fp.ToLower(CultureInfo.InvariantCulture).Contains(fn))
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
            var ver = typeof(Plugin).GetCustomAttribute<BepInPlugin>().Version;
            consoleText.Add("Welcome to <b>Command Console</b> version <color=#00FF00>" + ver + "</color>.");
            consoleText.Add("<margin=1em>Type in <b><color=#FFFF00>/help</color></b> to list the available commands.");
            consoleText.Add("<margin=1em><i>Use the <b><color=#FFFFFF>Up/Down Arrow</color></b> to cycle command history.</i>");
            consoleText.Add("<margin=1em><i>Use the <b><color=#FFFFFF>Mouse Wheel</color></b> to scroll up/down the output.</i>");
            consoleText.Add("<margin=1em><i>Start typing <color=#FFFF00>/</color> and press TAB to see commands starting with those letters.</i>");
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
                        inputFieldText.caretPosition = inputFieldText.text.Length;
                        inputFieldText.stringPosition = inputFieldText.text.Length;
                    }
                }
                if (Keyboard.current[Key.DownArrow].wasPressedThisFrame)
                {
                    log("DownArrow, commandHistoryIndex = " + commandHistoryIndex + ", commandHistory.Count = " + commandHistory.Count);
                    commandHistoryIndex = Math.Max(0, commandHistoryIndex - 1);
                    if (commandHistoryIndex > 0)
                    {
                        inputFieldText.text = commandHistory[commandHistory.Count - commandHistoryIndex];
                    }
                    else
                    {
                        inputFieldText.text = "";
                    }
                    inputFieldText.ActivateInputField();
                    inputFieldText.caretPosition = inputFieldText.text.Length;
                    inputFieldText.stringPosition = inputFieldText.text.Length;
                }
                if (Keyboard.current[Key.Tab].wasPressedThisFrame)
                {
                    List<string> list = new();
                    foreach (var k in commandRegistry.Keys)
                    {
                        if (k.StartsWith(inputFieldText.text))
                        {
                            list.Add(k);
                        }
                    }
                    if (list.Count != 0)
                    {
                        list.Sort();
                        Colorize(list, "#FFFF00");
                        foreach (var k in joinPerLine(list, 10))
                        {
                            addLine("<margin=2em>" + k);
                        }

                        createOutputLines();
                    }
                    inputFieldText.ActivateInputField();
                    inputFieldText.caretPosition = inputFieldText.text.Length;
                }
                return;
            }
            if (wh.GetHasUiOpen() && toggleAction.WasPressedThisFrame() && background == null)
            {
                return;
            }
            if (!toggleAction.WasPressedThisFrame() || background != null)
            {
                return;
            }

            logger.LogInfo("GetHasUiOpen: " + wh.GetHasUiOpen() + ", Background null?" + (background == null));

            canvas = new GameObject("CommandConsoleCanvas");
            var c = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.transform.SetAsLastSibling();

            log("Creating the background");

            RecreateBackground(wh);
            log("Done");
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
            inputFieldText.caretColor = Color.white;
            inputFieldText.selectionColor = Color.gray;
            inputFieldText.onFocusSelectAll = false;

            log("   Set position");
            rect = inputField.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -panelHeight / 2 + (5 + fontSize.Value) / 2, 0);
            rect.sizeDelta = new Vector2(panelWidth - 10, 5 + fontSize.Value);

            createOutputLines();

            log("Patch in the custom text window");
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.TextInput;

            log("Activating the field");
            inputFieldText.enabled = false;
            inputFieldText.enabled = true;

            inputFieldText.Select();
            inputFieldText.ActivateInputField();
        }

        void createOutputLines()
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
                            addLine("<margin=1em><color=#FF8080>" + el);
                        }
                    }
                }
                else
                {
                    addLine("<color=#FF0000>Unknown command</color>");
                }

            }
            scrollOffset = 0;
            createOutputLines();

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
            return new(Regex.Split(text, "\\s+"));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.CloseAllWindows))]
        static bool WindowsHandler_CloseAllWindows()
        {
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
                if (args[1] == "*")
                {
                    addLine("<margin=1em>Available commands:");
                    var list = new List<string>();
                    foreach (var kv in commandRegistry)
                    {
                        list.Add(kv.Key);
                    }
                    list.Sort();

                    foreach (var cmd in list)
                    {
                        var reg = commandRegistry[cmd];
                        addLine("<margin=2em><color=#FFFF00>" + cmd + "</color> - " + reg.description);
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
                        log("Help for " + args[1] + " - " + cmd.description);
                        addLine("<margin=1em>" + cmd.description);
                    }
                    else
                    {
                        addLine("<margin=1em><color=#FFFF00>Unknown command");
                    }
                }
            }
        }

        void HelpListCommands()
        {
            addLine("<margin=1em>Type <b><color=#FFFF00>/help [command]</color></b> to get specific command info.");
            addLine("<margin=1em>Type <b><color=#FFFF00>/help *</color></b> to list all commands with their description.");
            addLine("<margin=1em>Available commands:");
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
                addLine("<margin=2em><color=#FFFF00>/spawn basic [amount]</color> - Spawn some food, water, oxygen and beginner materials");
                addLine("<margin=2em><color=#FFFF00>/spawn advanced [amount]</color> - Spawn the best equipment");
                addLine("<margin=2em><color=#FFFF00>/spawn itemid [amount]</color> - spawn the given item by the given amount");
            } else
            {
                if (args[1] == "list")
                {
                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> possibleSpawns = new();
                    foreach (var g in GroupsHandler.GetAllGroups())
                    {
                        if (g is GroupItem gi && gi.GetId().ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
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
                else if (args[1] == "basic")
                {
                    string[] resources =
                    {
                        "astrofood", "WaterBottle1", "OxygenCapsule1", "Iron", "Cobalt", "Titanium", "Magnesium", "Silicon", "Aluminium"
                    };
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
                    {
                        "MultiToolLight2", "MultiToolDeconstruct2", "Backpack5", 
                        "Jetpack3", "BootsSpeed3", "MultiBuild", "MultiToolMineSpeed4",
                        "EquipmentIncrease3", "OxygenTank4", "HudCompass"
                    };
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

                    var gid = args[1].ToLower(CultureInfo.InvariantCulture);
                    SpaceCraft.Group g = FindGroup(gid);

                    if (g == null)
                    {
                        DidYouMean(gid, false);
                    }
                    else if (!(g is GroupItem))
                    {
                        addLine("<margin=1em><color=#FF0000>This item can't be spawned.");
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
                        var wo = WorldObjectsHandler.CreateNewWorldObject(gr);
                        if (inv.AddItem(wo))
                        {
                            added++;
                        }
                        else
                        {
                            WorldObjectsHandler.DestroyWorldObject(wo);
                        }
                    }
                    else
                    {
                        addLine("<margin=1em><color=red>Unknown item " + gid);
                    }
                }
            }

            int count = amount * resources.Length;
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

        [Command("/tp", "Teleport to a user-named location or an x, y, z position")]
        public void Teleport(List<string> args)
        {
            if (args.Count != 2 && args.Count != 4)
            {
                addLine("<margin=1em>Teleport to a user-named location or an x, y, z position");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tp location-name</color> - teleport to location-name");
                addLine("<margin=2em><color=#FFFF00>/tp x y z</color> - teleport to a specific coordinate");
                addLine("<margin=2em><color=#FFFF00>/tp x:y:z</color> - teleport to a specific coordinate described by the colon format");
                addLine("<margin=1em>See also <color=#FFFF00>/tp-create</color>, <color=#FFFF00>/tp-list</color>, <color=#FFFF00>/tp-remove</color>, ");
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

                    addLine("<margin=1em>Teleported to: ( "
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

                    addLine("<margin=1em>Teleported to: <color=#00FF00>" + args[1] + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
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

                addLine("<margin=1em>Teleported to: ( "
                    + args[1]
                    + ", " + args[2]
                    + ", " + args[3]
                    + " )"
                );
            }
        }

        [Command("/tpr", "Teleport relative to the current location")]
        public void TeleportRelative(List<string> args)
        {
            if (args.Count != 2 && args.Count != 4)
            {
                addLine("<margin=1em>Teleport relative to the current location");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tpr x y z</color> - teleport with the specified deltas");
                addLine("<margin=2em><color=#FFFF00>/tpr x:y:z</color> - teleport with the specified deltas described by the colon format");
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

                    addLine("<margin=1em>Teleported to: ( "
                        + x.ToString(CultureInfo.InvariantCulture)
                        + ", " + y.ToString(CultureInfo.InvariantCulture)
                        + ", " + z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
                }
                else
                {
                    addLine("<margin=1em><color=#FF0000>Invalid relative offset(s).");
                }
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();

                var x = float.Parse(args[1], CultureInfo.InvariantCulture) + pm.transform.position.x;
                var y = float.Parse(args[2], CultureInfo.InvariantCulture) + pm.transform.position.y;
                var z = float.Parse(args[3], CultureInfo.InvariantCulture) + pm.transform.position.z;

                pm.SetPlayerPlacement(new Vector3(x, y, z), pm.transform.rotation);

                addLine("<margin=1em>Teleported to: ( "
                    + x.ToString(CultureInfo.InvariantCulture)
                    + ", " + y.ToString(CultureInfo.InvariantCulture)
                    + ", " + z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
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

                var pos = pm.transform.position;
                savedTeleportLocations[args[1]] = pos;
                PersistTeleportLocations();

                addLine("<margin=1em>Teleport location created: <color=#00FF00>" + args[1] + "</color> at ( "
                    + pos.x.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                    + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                    + " )"
                );
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

                var tpName = args[1];
                if (savedTeleportLocations.TryGetValue(tpName, out var pos))
                {
                    savedTeleportLocations.Remove(tpName);
                    PersistTeleportLocations();
                    addLine("<margin=1em>Teleport location removed: <color=#00FF00>" + tpName + "</color> at ( "
                        + pos.x.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.y.ToString(CultureInfo.InvariantCulture)
                        + ", " + pos.z.ToString(CultureInfo.InvariantCulture)
                        + " )"
                    );
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
                string filename = string.Format("{0}/{1}", Application.persistentDataPath, "CommandConsole_Locations.txt");

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

        [Command("/list-stages", "List the terraformation stages along with their Ti amounts")]
        public void ListStages(List<string> args)
        {
            var tfm = Managers.GetManager<TerraformStagesHandler>();
            foreach (var stage in tfm.GetAllTerraGlobalStages())
            {
                addLine("<margin=1em><color=#FFFFFF>" + stage.GetTerraId()
                    + "</color> <color=#00FF00>\"" + Readable.GetTerraformStageName(stage)
                    + "\"</color> @ <b>"
                    + string.Format("{0:#,##0}", stage.GetStageStartValue()) + "</b> " + stage.GetWorldUnitType());
            }
        }

        [Command("/add-ti", "Adds the specified amount to the Ti value")]
        public void AddTi(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Ti value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-ti amount</color> - Ti += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Terraformation, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Terraformation updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-heat", "Adds the specified amount to the Heat value")]
        public void AddHeat(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Heat value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-heat amount</color> - Heat += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Heat, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Heat updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-oxygen", "Adds the specified amount to the Oxygen value")]
        public void AddOxygen(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Oxygen value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-oxygen amount</color> - Oxygen += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Oxygen, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Oxygen updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-pressure", "Adds the specified amount to the Pressure value")]
        public void AddPressure(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Pressure value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-pressure amount</color> - Pressure += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Pressure, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Pressure updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-biomass", "Adds the specified amount to the Biomass value")]
        public void AddBiomass(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Biomass value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-biomass amount</color> - Biomass += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-plants</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Biomass, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Biomass updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-plants", "Adds the specified amount to the Plants value")]
        public void AddPlants(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Plants value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-plants amount</color> - Plants += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-insects</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Plants, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Plants updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-insects", "Adds the specified amount to the Insects value")]
        public void AddInsects(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Insects value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-insects amount</color> - Insects += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color> or <color=#FFFF00>/add-animals</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Insects, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Insects updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        [Command("/add-animals", "Adds the specified amount to the Animals value")]
        public void AddAnimals(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds the specified amount to the Animals value");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-animals amount</color> - Animals += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-ti</color>, <color=#FFFF00>/add-heat</color>, <color=#FFFF00>/add-oxygen</color>, <color=#FFFF00>/add-pressure</color>,");
                addLine("<margin=1em><color=#FFFF00>/add-biomass</color>, <color=#FFFF00>/add-plants</color> or <color=#FFFF00>/add-insects</color>.");
            }
            else
            {
                var newValue = AddToWorldUnit(DataConfig.WorldUnitType.Animals, float.Parse(args[1], CultureInfo.InvariantCulture));
                addLine("<margin=1em>Animals updated. Now at <color=#00FF00>" + string.Format("{0:#,##0}", newValue));
            }
        }

        float AddToWorldUnit(DataConfig.WorldUnitType wut, float amount)
        {
            var result = 0.0f;
            var worldUnitCurrentTotalValue = AccessTools.Field(typeof(WorldUnit), "currentTotalValue");
            var worldUnitsPositioningWorldUnitsHandler = AccessTools.Field(typeof(WorldUnitPositioning), "worldUnitsHandler");
            var worldUnitsPositioningHasMadeFirstInit = AccessTools.Field(typeof(WorldUnitPositioning), "hasMadeFirstInit");

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            foreach (WorldUnit wu in wuh.GetAllWorldUnits())
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

            List<GameObject> allWaterVolumes = Managers.GetManager<WaterHandler>().GetAllWaterVolumes();
            //LogInfo("allWaterVolumes.Count = " + allWaterVolumes.Count);
            foreach (GameObject go1 in allWaterVolumes)
            {
                var wup = go1.GetComponent<WorldUnitPositioning>();

                //LogInfo("WorldUnitPositioning-Before: " + wup.transform.position);

                worldUnitsPositioningWorldUnitsHandler.SetValue(wup, wuh);
                worldUnitsPositioningHasMadeFirstInit.SetValue(wup, false);
                wup.UpdateEvolutionPositioning();

                //LogInfo("WorldUnitPositioning-After: " + wup.transform.position);
            }
            return result;
        }

        [Command("/list-microchip-tiers", "Lists all unlock tiers and unlock items of the microchips")]
        public void ListMicrochipTiers(List<string> args)
        {
            UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

            List<List<GroupData>> tiers = new List<List<GroupData>>
            {
                unlock.tier1GroupToUnlock,
                unlock.tier2GroupToUnlock,
                unlock.tier3GroupToUnlock,
                unlock.tier4GroupToUnlock,
                unlock.tier5GroupToUnlock,
                unlock.tier6GroupToUnlock,
                unlock.tier7GroupToUnlock,
                unlock.tier8GroupToUnlock,
                unlock.tier9GroupToUnlock,
                unlock.tier10GroupToUnlock,
            };

            for (int i = 0; i < tiers.Count; i++)
            {
                List<GroupData> gd = tiers[i];
                addLine("<margin=1em><b>Tier #" + (i + 1));

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
                        addLine("<margin=2em>" + sb.ToString());
                    }
                }
                else
                {
                    addLine("<margin=2em>None");
                }
            }

        }

        [Command("/list-microchip-unlocks", "List the identifiers of microchip unlocks; can specify prefix")]
        public void ListMicrochipUnlocks(List<string> args)
        {
            UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

            List<List<GroupData>> tiers = new List<List<GroupData>>
            {
                unlock.tier1GroupToUnlock,
                unlock.tier2GroupToUnlock,
                unlock.tier3GroupToUnlock,
                unlock.tier4GroupToUnlock,
                unlock.tier5GroupToUnlock,
                unlock.tier6GroupToUnlock,
                unlock.tier7GroupToUnlock,
                unlock.tier8GroupToUnlock,
                unlock.tier9GroupToUnlock,
                unlock.tier10GroupToUnlock,
            };

            string prefix = "";
            if (args.Count >= 2)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }

            List<string> list = new();

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
                addLine("<margin=1em>No microchip unlocks found.");
            }
            else
            {
                list.Sort();
                Colorize(list, "#00FF00");
                foreach (var line in joinPerLine(list, 5))
                {
                    addLine("<margin=2em>" + line);
                }
            }
        }

        [Command("/unlock-microchip", "Unlocks a specific microchip techology")]
        public void UnlockMicrochip(List<string> args)
        {
            if (args.Count < 2)
            {
                addLine("<margin=1em>Unlocks a specific microchip techology");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/unlock-microchip list [prefix]</color> - lists technologies not unlocked yet");
                addLine("<margin=2em><color=#FFFF00>/unlock-microchip identifier</color> - Unlocks the given technology");
            }
            else
            {
                if (args[1] == "list")
                {
                    UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                    List<GroupData> tiers = new List<GroupData>();
                    tiers.AddRange(unlock.tier1GroupToUnlock);
                    tiers.AddRange(unlock.tier2GroupToUnlock);
                    tiers.AddRange(unlock.tier3GroupToUnlock);
                    tiers.AddRange(unlock.tier4GroupToUnlock);
                    tiers.AddRange(unlock.tier5GroupToUnlock);
                    tiers.AddRange(unlock.tier6GroupToUnlock);
                    tiers.AddRange(unlock.tier7GroupToUnlock);
                    tiers.AddRange(unlock.tier8GroupToUnlock);
                    tiers.AddRange(unlock.tier9GroupToUnlock);
                    tiers.AddRange(unlock.tier10GroupToUnlock);

                    var prefix = "";
                    if (args.Count > 2)
                    {
                        prefix = args[2].ToLower(CultureInfo.InvariantCulture);
                    }
                    List<string> list = new();
                    foreach (var gd in tiers)
                    {
                        var g = GroupsHandler.GetGroupViaId(gd.id);
                        if (!GroupsHandler.IsGloballyUnlocked(g) && g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            list.Add(g.id);
                        }
                    }
                    if (list.Count != 0)
                    {
                        list.Sort();
                        Colorize(list, "#00FF00");
                        foreach (var line in joinPerLine(list, 5))
                        {
                            addLine("<margin=2em>" + line);
                        }
                    }
                }
                else
                {
                    var gr = GroupsHandler.GetGroupViaId(args[1]);
                    if (gr != null)
                    {
                        addLine("<margin=1em>Unlocked: <color=#FFFFFF>" + gr.id + "</color> <color=#00FF00>\"" + Readable.GetGroupName(gr) + "\"");
                        GroupsHandler.UnlockGroupGlobally(gr);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        addLine("<margin=1em><color=#FF0000>Unknown technology");
                    }
                }
            }
        }

        [Command("/unlock", "Unlocks a specific techology")]
        public void Unlock(List<string> args)
        {
            if (args.Count < 2)
            {
                addLine("<margin=1em>Unlocks a specific techology");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/unlock list [prefix]</color> - lists technologies not unlocked yet");
                addLine("<margin=2em><color=#FFFF00>/unlock identifier</color> - Unlocks the given technology");
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
                    List<string> list = new();
                    foreach (var gd in GroupsHandler.GetAllGroups())
                    {
                        var g = GroupsHandler.GetGroupViaId(gd.id);
                        if (!GroupsHandler.IsGloballyUnlocked(g) && g.id.ToLower(CultureInfo.InvariantCulture).StartsWith(prefix))
                        {
                            list.Add(g.id);
                        }
                    }
                    if (list.Count != 0)
                    {
                        list.Sort();
                        Colorize(list, "#00FF00");
                        foreach (var line in joinPerLine(list, 5))
                        {
                            addLine("<margin=2em>" + line);
                        }
                    }
                }
                else
                {
                    var gr = GroupsHandler.GetGroupViaId(args[1]);
                    if (gr != null)
                    {
                        addLine("<margin=1em>Unlocked: <color=#FFFFFF>" + gr.id + "</color> <color=#00FF00>\"" + Readable.GetGroupName(gr) + "\"");
                        GroupsHandler.UnlockGroupGlobally(gr);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        addLine("<margin=1em><color=#FF0000>Unknown technology");
                    }
                }
            }
        }

        [Command("/list-tech", "Lists all technology identifiers; can filter via prefix")]
        public void ListTech(List<string> args)
        {
            var prefix = "";
            if (args.Count > 1)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }
            List<string> list = new();
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
                foreach (var line in joinPerLine(list, 5))
                {
                    addLine("<margin=2em>" + line);
                }
            }
        }

        SpaceCraft.Group FindGroup(string gid)
        {
            foreach (var gr in GroupsHandler.GetAllGroups())
            {
                var gci = gr.GetId().ToLower(CultureInfo.InvariantCulture);
                if (gci == gid && !gci.StartsWith("spacemultiplier"))
                {
                    return gr;

                }
            }
            return null;
        }

        void DidYouMean(string gid, bool isStructure)
        {
            List<string> similar = FindSimilar(gid, GroupsHandler.GetAllGroups()
                .Where(g => { 
                    if (isStructure && g is GroupConstructible && !g.id.StartsWith("SpaceMultiplier"))
                    {
                        return true;
                    }
                    return !isStructure && g is GroupItem;
                })
                .Select(g => g.id.ToLower(CultureInfo.InvariantCulture)));
            if (similar.Count != 0)
            {
                similar.Sort();
                Colorize(similar, "#00FF00");

                if (isStructure)
                {
                    addLine("<margin=1em><color=#FF0000>Unknown structure.</color> Did you mean?");
                }
                else
                {
                    addLine("<margin=1em><color=#FF0000>Unknown item.</color> Did you mean?");
                }
                foreach (var line in joinPerLine(similar, 5))
                {
                    addLine("<margin=2em>" + line);
                }
            }
            else
            {
                if (isStructure)
                {
                    addLine("<margin=1em><color=#FF0000>Unknown structure.</color>");
                }
                else
                {
                    addLine("<margin=1em><color=#FF0000>Unknown item.</color>");
                }
            }

        }

        [Command("/tech-info", "Shows detailed information about a technology")]
        public void TechInfo(List<string> args)
        {
            if (args.Count < 2)
            {
                addLine("<margin=1em>Shows detailed information about a technology");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/tech-info identifier</color> - show detailed info");
                addLine("<margin=1em>See also <color=#FFFF00>/list-tech</color> for all identifiers");
            }
            else
            {
                var gr = FindGroup(args[1]);
                if (gr == null)
                {
                    DidYouMean(args[1], false);
                }
                else
                {
                    addLine("<margin=1em><b>ID:</b> <color=#00FF00>" + gr.id);
                    addLine("<margin=1em><b>Name:</b> <color=#00FF00>" + Readable.GetGroupName(gr));
                    addLine("<margin=1em><b>Description:</b> <color=#00FF00>" + Readable.GetGroupDescription(gr));
                    addLine("<margin=1em><b>Is unlocked:</b> <color=#00FF00>" + GroupsHandler.IsGloballyUnlocked(gr));
                    addLine("<margin=2em><b>Unlocked at:</b> <color=#00FF00>" + string.Format("{0:#,##0}", gr.GetUnlockingInfos().GetUnlockingValue()) + " " + gr.GetUnlockingInfos().GetWorldUnit());
                    if (gr is GroupItem gi)
                    {
                        addLine("<margin=1em><b>Class:</b> Item");
                        if (gi.GetUsableType() != DataConfig.UsableType.Null)
                        {
                            addLine("<margin=2em><b>Usable:</b> " + gi.GetUsableType());
                        }
                        if (gi.GetEquipableType() != DataConfig.EquipableType.Null)
                        {
                            addLine("<margin=2em><b>Equipable:</b> " + gi.GetEquipableType());
                        }
                        if (gi.GetItemCategory() != DataConfig.ItemCategory.Null)
                        {
                            addLine("<margin=2em><b>Category:</b> <color=#00FF00>" + gi.GetItemCategory());
                        }
                        if (gi.GetItemSubCategory() != DataConfig.ItemSubCategory.Null)
                        {
                            addLine("<margin=2em><b>Subcategory:</b> <color=#00FF00>" + gi.GetItemSubCategory());
                        }
                        addLine("<margin=2em><b>Value:</b> <color=#00FF00>" + gi.GetGroupValue());

                        List<string> list = new();
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
                            addLine("<margin=2em><b>Craftable in:</b> " + String.Join(", ", list));
                        }

                        list = new();
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
                            addLine("<margin=2em><b>Unit:</b> " + string.Join(", ", list));
                        }

                        var ggi = gi.GetGrowableGroup();
                        if (ggi != null)
                        {
                            addLine("<margin=2em><b>Grows:</b> <color=#00FF00>" + ggi.GetId() + " \"" + Readable.GetGroupName(ggi));
                        }
                        addLine("<margin=2em><b>Chance to spawn:</b> <color=#00FF00>" + gi.GetChanceToSpawn());
                        addLine("<margin=2em><b>Destroyable:</b> <color=#00FF00>" + !gi.GetCantBeDestroyed());
                    }
                    else if (gr is GroupConstructible gc)
                    {
                        addLine("<margin=1em><b>Class:</b> Building");
                        addLine("<margin=1em><b>Category:</b> <color=#00FF00>" + gc.GetGroupCategory());
                        if (gc.GetWorldUnitMultiplied() != DataConfig.WorldUnitType.Null)
                        {
                            addLine("<margin=1em><b>Unit multiplied:</b> <color=#00FF00>" + gc.GetWorldUnitMultiplied());
                        }
                        List<string> list = new();
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
                            addLine("<margin=1em><b>Unit generation:</b> " + string.Join(", ", list));
                        }
                        var ng = gc.GetNextTierGroup();
                        if (ng != null)
                        {
                            addLine("<margin=1em><b>Next tier group:</b> <color=#00FF00>" + ng.id + " \"" + Readable.GetGroupName(ng) + "\""); 
                        }
                    } else
                    {
                        addLine("<margin=1em><b>Class:</b> Unknown");
                    }
                    var recipe = gr.GetRecipe();
                    if (recipe != null)
                    {
                        var ingr = recipe.GetIngredientsGroupInRecipe();
                        if (ingr.Count != 0)
                        {
                            addLine("<margin=1em><b>Recipe:</b>");
                            foreach (var rg in ingr)
                            {
                                addLine("<margin=2em><color=#00FF00>" + rg.id + " \"" + Readable.GetGroupName(rg) + "\"");
                            }
                        }
                        else
                        {
                            addLine("<margin=1em><b>Recipe:</b> None");
                        }
                    }
                }
            }
        }

        [Command("/copy-to-clipboard", "Copies the console history to the system clipboard")]
        public void CopyToClipboard(List<string> args)
        {
            GUIUtility.systemCopyBuffer = string.Join("\n", consoleText);
        }

        [Command("/ctc", "Copies the console history to the system clipboard without formatting")]
        public void CopyToClipboard2(List<string> args)
        {
            var str = string.Join("\n", consoleText);
            str = str.Replace("<margin=1em>", "    ");
            str = str.Replace("<margin=2em>", "        ");
            str = str.Replace("<margin=3em>", "            ");
            str = str.Replace("<margin=4em>", "                ");
            str = str.Replace("<margin=5em>", "                    ");
            GUIUtility.systemCopyBuffer = Regex.Replace(str, "<\\/?.*?>", "");
        }

        [Command("/refill", "Refills the Health, Water and Oxygen meters")]
        public void Refill(List<string> args)
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var gh = pm.GetGaugesHandler();
            gh.AddHealth(100);
            gh.AddWater(100);
            gh.AddOxygen(1000);
            addLine("<margin=1em>Health, Water and Oxygen refilled");
        }

        [Command("/auto-refill", "Automatically refills the Health, Water and Oxygen. Re-issue command to stop.")]
        public void AutoRefill(List<string> args)
        {
            if (autorefillCoroutine != null)
            {
                addLine("<margin=1em>Auto Refill stopped");
                StopCoroutine(autorefillCoroutine);
                autorefillCoroutine = null;
            }
            else
            {
                addLine("<margin=1em>Auto Refill started");
                autorefillCoroutine = AutoRefillCoroutine();
                StartCoroutine(autorefillCoroutine);
            }
        }

        IEnumerator AutoRefillCoroutine()
        {
            for (; ; ) { 
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddHealth(100);
                gh.AddWater(100);
                gh.AddOxygen(1000);
                    yield return new WaitForSeconds(1);
            }
        }

        [Command("/add-health", "Adds a specific Health amount to the player")]
        public void AddHealth(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds a specific Health amount to the player");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-health amount</color> - Health += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-water</color> or <color=#FFFF00>/add-air</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddHealth(int.Parse(args[1]));
            }
        }

        [Command("/add-water", "Adds a specific Water amount to the player")]
        public void AddWater(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds a specific Water amount to the player");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-water amount</color> - Water += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-health</color> or <color=#FFFF00>/add-air</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddWater(int.Parse(args[1]));
            }
        }

        [Command("/add-air", "Adds a specific Air amount to the player")]
        public void AddAir(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Adds a specific Air amount to the player");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/add-air amount</color> - Water += amount");
                addLine("<margin=1em>See also <color=#FFFF00>/add-health</color> or <color=#FFFF00>/add-water</color>");
            }
            else
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var gh = pm.GetGaugesHandler();
                gh.AddOxygen(int.Parse(args[1]));
            }
        }

        [Command("/die", "Kills the player")]
        public void Die(List<string> args)
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            var sm = pm.GetPlayerStatus();
            DyingConsequencesHandler.HandleDyingConsequences(pm, GroupsHandler.GetGroupViaId(sm.canisterGroup.id));
            sm.DieAndRespawn();
            addLine("<margin=1em>Player died and respawned.");
            //Managers.GetManager<WindowsHandler>().CloseAllWindows();
        }

        [Command("/list-larvae", "Show information about larvae sequencing; can use prefix filter")]
        public void LarvaeSequenceInfo(List<string> args)
        {
            var prefix = "";
            if (args.Count > 1)
            {
                prefix = args[1].ToLower(CultureInfo.InvariantCulture);
            }

            Dictionary<string, List<string>> larvaeToSequenceInto = new();
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
                                list = new List<string>();
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

                addLine("<margin=1em><b><color=#00FFFF>" + larve.id + " \"" + Readable.GetGroupName(larve) + "\"");
                var ull = larve.GetUnlockingInfos();
                if (ull.GetIsUnlocked())
                {
                    addLine("<margin=2em><b>Unlocked:</b> true");
                }
                else
                {
                    if (ull.GetWorldUnit() != DataConfig.WorldUnitType.Null)
                    {
                        addLine("<margin=2em><b>Unlocked:</b> false");
                        addLine("<margin=4em><b>Unlocked at:</b> " + string.Format("{0:#,##0}", ull.GetUnlockingValue()) + " " + ull.GetWorldUnit());
                    }
                    else
                    {
                        addLine("<margin=2em><b>Unlocked globally:</b> " + larve.GetIsGloballyUnlocked());
                    }
                }
                addLine("<margin=2em><b>Outcomes</b>");

                foreach (var outcome in outcomes)
                {
                    var og = GroupsHandler.GetGroupViaId(outcome) as GroupItem;
                    if (og != null) {
                        var chance = og.GetChanceToSpawn();
                        if (chance == 0)
                        {
                            chance = 100;
                        }
                        var ul = og.GetUnlockingInfos();
                        if (ul.GetIsUnlocked())
                        {
                            addLine("<margin=3em><color=#00FF00>" + og.id + " \"" + Readable.GetGroupName(og) + "\"</color> = <b>" + chance + " %</b>");
                        }
                        else
                        {
                            addLine("<margin=3em><color=#FF0000>[Not unlocked]</color> <color=#00FF00>" + og.id + " \"" + Readable.GetGroupName(og) + "\"</color> = <b>" + chance + " %</b>");
                        }
                        if (ul.GetWorldUnit() != DataConfig.WorldUnitType.Null)
                        {
                            addLine("<margin=4em><b>Unlocked at:</b> " + string.Format("{0:#,##0}", ul.GetUnlockingValue()) + " " + ul.GetWorldUnit());
                        }
                        else
                        {
                            addLine("<margin=4em><b>Unlocked globally:</b> " + og.GetIsGloballyUnlocked());
                        }
                    }
                }
            }
        }

        [Command("/list-loot", "List chest loot information")]
        public void ListLoot(List<string> args)
        {
            var stagesLH = Managers.GetManager<InventoryLootHandler>();
            var stages = stagesLH.lootTerraStages;

            logger.LogInfo("Found " + stages.Count + " stages");
            stages.Sort((a, b) =>
            {
                float v1 = a.terraStage.GetStageStartValue();
                float v2 = b.terraStage.GetStageStartValue();
                return v1 < v2 ? -1 : (v1 > v2 ? 1 : 0);
            });
            foreach (InventoryLootStage ils in stages)
            {
                addLine("<margin=1em><b><color=#00FFFF>" + ils.terraStage.GetTerraId() + " \"" + Readable.GetTerraformStageName(ils.terraStage) + "\"</color></b> at "
                    + string.Format("{0:#,##0}", ils.terraStage.GetStageStartValue()) + " Ti");

                string[] titles = { "Common", "Uncommon", "Rare", "Very Rare", "Ultra Rare" };
                List<List<GroupData>> gs = new List<List<GroupData>>()
                {
                    ils.commonItems, ils.unCommonItems, ils.rareItems, ils.veryRareItems, ils.ultraRareItems
                };
                float boostAmount = (float)AccessTools.Field(typeof(InventoryLootStage), "boostedMultiplier").GetValue(ils);

                List<float> chances = new List<float>
                {
                    100,
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceUnCommon").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceRare").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceVeryRare").GetValue(ils),
                    (float)AccessTools.Field(typeof(InventoryLootStage), "chanceUltraRare").GetValue(ils),
                };

                for (int i = 0; i < titles.Length; i++)
                {
                    addLine("<margin=2em><b>" + titles[i] + "</b> (Chance: " + chances[i] + " %, Boost multiplier: " + boostAmount + ")");
                    foreach (GroupData g in gs[i])
                    {
                        addLine("<margin=3em><color=#00FF00>" + g.id + " \"" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(g.id)) + "\"");
                    }
                }
            }
        }

        [Command("/list-larvae-zones", "Lists the larvae zones and the larvae that can spawn there.")]
        public void ListLarvaeZones(List<string> args)
        {
            var sectors = FindObjectsOfType<Sector>();
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

            foreach (LarvaeZone lz in FindObjectsOfType<LarvaeZone>())
            {
                var pool = lz.GetLarvaesToAddToPool();
                var bounds = lz.GetComponent<Collider>().bounds;
                var center = bounds.center;
                var extents = bounds.extents;

                var captureLarvaeZoneCurrentSector = "";
                foreach (SectorEnter sector in FindObjectsOfType<SectorEnter>())
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

                addLine("<margin=1em>Sector <color=#00FFFF>" + captureLarvaeZoneCurrentSector);

                addLine("<margin=2em>Position = " + string.Join(", ", (int)center.x, (int)center.y, (int)center.z)
                    + ", Extents = " + string.Join(", ", (int)extents.x, (int)extents.y, (int)extents.z));

                if (pool != null && pool.Count != 0)
                {
                    foreach (var lp in pool)
                    {
                        addLine("<margin=3em><color=#00FF00>" + lp.id
                            + " \"" + Readable.GetGroupName(GroupsHandler.GetGroupViaId(lp.id)) + "\"</color>, Chance = " + lp.chanceToSpawn + "%");
                    }
                }
                else
                {
                    addLine("<margin=3em>No larvae spawn info.");
                }
            }

            addLine("<color=#FF8080>Warning! You may want to reload this save to avoid game issues.");
        }

        [Command("/build", "Build a structure. The ingredients are automatically added to the inventory first.")]
        public void Build(List<string> args)
        {
            if (args.Count == 1)
            {
                addLine("<margin=1em>Build a structure. The ingredients are automatically added to the inventory first.");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/build list [name-prefix]</color> - list the item ids that can be built");
                addLine("<margin=2em><color=#FFFF00>/build itemid [count]</color> - get the ingredients and start building it by showing the ghost");
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
                    List<string> possibleStructures = new();
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
                    foreach (var line in joinPerLine(possibleStructures, 5))
                    {
                        addLine("<margin=1em>" + line);
                    }
                }
                else
                {
                    var gid = args[1].ToLower(CultureInfo.InvariantCulture);
                    SpaceCraft.Group g = FindGroup(gid);
                    if (g == null)
                    {
                        DidYouMean(gid, true);
                    }
                    else if (!(g is GroupConstructible))
                    {
                        addLine("<color=#FF0000>This item can't be built.");
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
                                var wo = WorldObjectsHandler.CreateNewWorldObject(ri);
                                if (!inv.AddItem(wo))
                                {
                                    WorldObjectsHandler.DestroyWorldObject(wo);
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
                            addLine("<color=#FF0000>Inventory full.");
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
                                Logger.LogInfo("Cancelling previous ghost");
                                pb.InputOnCancelAction();
                            }

                            Logger.LogInfo("Activating ghost for " + gc.GetId());
                            pb.SetNewGhost(gc);
                        }
                    }
                }
            }
        }

        [Command("/raise", "Raises player-placed objects in a radius (cylindrically)")]
        public void Raise(List<string> args)
        {
            if (args.Count != 3)
            {
                addLine("<margin=1em>Raises player-placed objects in a radius (cylindrically).");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/raise radius amount</color> - raise all items within the given radius by the given amount");
            }
            else
            {
                var radius = Math.Abs(float.Parse(args[1]));
                var amount = float.Parse(args[2]);

                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController();
                var pos = pm.transform.position;
                var posXY = new Vector2(pos.x, pos.z);

                int i = 0;
                foreach (var wo in WorldObjectsHandler.GetAllWorldObjects())
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
                addLine("<margin=1em>" + i + " objects affected.");
            }
        }

        [Command("/console-set-left", "Sets the Command Console's window's left position on the screen")]
        public void ConsoleLeft(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Sets the Command Console's left position on the screen.");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/console-set-left value</color> - Set the window's left position");
            }
            else
            {
                consoleLeft.Value = int.Parse(args[1]);
            }
            addLine("<margin=1em>Current Left: " + consoleLeft.Value);
            RecreateBackground(Managers.GetManager<WindowsHandler>());
        }

        [Command("/console-set-right", "Sets the Command Console's window's right position on the screen")]
        public void ConsoleRight(List<string> args)
        {
            if (args.Count != 2)
            {
                addLine("<margin=1em>Sets the Command Console's right position on the screen.");
                addLine("<margin=1em>Usage:");
                addLine("<margin=2em><color=#FFFF00>/console-set-right value</color> - Set the window's right position");
            }
            else
            {
                consoleRight.Value = int.Parse(args[1]);
            }
            addLine("<margin=1em>Current Right: " + consoleRight.Value);
            RecreateBackground(Managers.GetManager<WindowsHandler>());
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

        static List<string> FindSimilar(string userText, IEnumerable<string> texts)
        {
            List<string> result = new();
            foreach (var text in texts)
            {
                if (text.Contains(userText))
                {
                    result.Add(text);
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
