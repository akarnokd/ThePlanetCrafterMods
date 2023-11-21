using BepInEx;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using BepInEx.Bootstrap;
using System.Collections;
using System.Reflection;
using LibCommon;

namespace CheatTeleportNearestMinable
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatteleportnearestminable", "(Cheat) Teleport To Nearest Minable", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        /// <summary>
        /// List of comma-separated resource ids to look for.
        /// </summary>
        static HashSet<string> resourceSet;

        static HashSet<string> larvaeSet;

        static HashSet<string> fishSet;

        static HashSet<string> frogSet;

        static ConfigEntry<string> modeSwitchKey;

        static ConfigEntry<float> radius;

        static ConfigEntry<float> checkDelay;

        static bool autoMode;

        static Func<string> getMultiplayerMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            string resourceSetStr = Config.Bind("General", "ResourceSet", StandardResourceSets.defaultOres, "List of comma-separated resource ids to look for.").Value;

            resourceSet = new HashSet<string>(resourceSetStr.Split(','));

            var larvaeSetStr = Config.Bind("General", "LarvaeSet", StandardResourceSets.defaultLarvae, "List of comma-separated larvae ids to look for.");

            larvaeSet = new HashSet<string>(larvaeSetStr.Value.Split(','));

            var fishSetStr = Config.Bind("General", "FishSet", StandardResourceSets.defaultFish, "List of comma-separated fish ids to look for.");

            fishSet = new HashSet<string>(fishSetStr.Value.Split(','));

            var frogSetStr = Config.Bind("General", "FrogSet", StandardResourceSets.defaultFrogs, "List of comma-separated frog ids to look for.");

            frogSet = new HashSet<string>(frogSetStr.Value.Split(','));

            modeSwitchKey = Config.Bind("General", "ToggleAutomatic", "V", "Press this key (without modifiers) to enable automatic mining/grabbing in a radius.");

            radius = Config.Bind("General", "Radius", 90f, "The automatic mining/grabbing radius.");

            checkDelay = Config.Bind("General", "Delay", 5f, "The delay between automatic checks in seconds");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, managing multiplayer mode");

                getMultiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);

            }

            Harmony.CreateAndPatchAll(typeof(Plugin));

            StartCoroutine(DetectionLoop());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnShowFeedbackDispatcher))]
        static bool PlayerInputDispatcher_OnShowFeedbackDispatcher()
        {
            var player = Managers.GetManager<PlayersManager>()?.GetActivePlayerController();

            if (player == null)
            {
                return true;
            }

            Vector3 playerPos = player.transform.position;

            var minables = FindMinableObjectsAround();
            var grabables = FindGrabableObjectsAround();

            var foundPlaced = false;
            var lastDistance = float.MaxValue;
            Actionnable found = null;

            foreach (var m in Both(minables, grabables))
            {
                var p = m.gameObject.transform.position;
                var d = Vector3.Distance(playerPos, p);
                if (!foundPlaced || d < lastDistance)
                {
                    lastDistance = d;
                    found = m;
                    foundPlaced = true;
                }
            }

            if (foundPlaced)
            {
                bool isShift = Keyboard.current[Key.LeftShift].isPressed;
                bool isCtrl = Keyboard.current[Key.LeftCtrl].isPressed;

                if (isShift || isCtrl)
                {
                    if (getMultiplayerMode == null || getMultiplayerMode() != "CoopClient")
                    {
                        if (found.TryGetComponent<WorldObjectAssociated>(out var woa))
                        {
                            var wo = woa.GetWorldObject();
                            var inv = player.GetPlayerBackpack().GetInventory();
                            if (!inv.AddItem(wo))
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 2f, "");
                            }
                            else
                            {
                                wo.SetDontSaveMe(false);
                                var nearest = found.gameObject.transform.position;
                                var gr = wo.GetGroup();

                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, 
                                    "< " + (int)nearest.x + ", " + (int)nearest.y + ", " + (int)nearest.z + " >     "
                                    + gr.id + " \"" + Readable.GetGroupName(gr)  + "\"    --- PICKED UP ---");

                                ShowInventoryAdded(wo, inv);

                                Destroy(found.gameObject);
                            }
                        }
                    }
                    else
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Multiplayer Client mode not supported");
                    }
                }
                if (!isCtrl)
                {
                    player.SetPlayerPlacement(found.transform.position, player.transform.rotation);
                }
                
                return false;
            }
            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Nothing to mine/grab");

            return false;
        }

        static List<ActionMinable> FindMinableObjectsAround()
        {
            List<ActionMinable> result = new();

            foreach(var am in FindObjectsByType<ActionMinable>(FindObjectsSortMode.None))
            {
                if (am.TryGetComponent<WorldObjectFromScene>(out var wos))
                {
                    if (resourceSet.Contains(wos.GetGroupData().id))
                    {
                        result.Add(am);
                    }
                }
                else
                if (am.TryGetComponent<WorldObjectAssociated>(out var woa))
                {
                    var gid = woa.GetWorldObject()?.GetGroup()?.GetId();
                    if (gid != null && resourceSet.Contains(gid))
                    {
                        result.Add(am);
                    }
                }
            }

            return result;
        }

        static List<ActionGrabable> FindGrabableObjectsAround()
        {
            List<ActionGrabable> result = new();

            foreach (var am in FindObjectsByType<ActionGrabable>(FindObjectsSortMode.None))
            {
                if (am.TryGetComponent<WorldObjectFromScene>(out var wos))
                {
                    var id = wos.GetGroupData().id;
                    if (larvaeSet.Contains(id) || fishSet.Contains(id) || frogSet.Contains(id))
                    {
                        result.Add(am);
                    }
                }
                else
                if (am.TryGetComponent<WorldObjectAssociated>(out var woa))
                {
                    var gid = woa.GetWorldObject()?.GetGroup()?.GetId();
                    if (gid != null && (larvaeSet.Contains(gid) || fishSet.Contains(gid) || frogSet.Contains(gid)))
                    {
                        result.Add(am);
                    }
                }
            }

            return result;
        }

        static void ShowInventoryAdded(WorldObject worldObject, Inventory inventory)
        {
            int c = 0;
            Group group = worldObject.GetGroup();
            string gid = group.GetId();
            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == gid)
                {
                    c++;
                }
            }

            string text = Readable.GetGroupName(group) + " + 1 (  " + c + "  )";
            var ids = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            ids.AddInformation(2f, text, DataConfig.UiInformationsType.InInventory, group.GetImage());
        }

        static void ShowInventoryAdded(string groupId, int count, Inventory inventory)
        {
            int c = 0;
            Group group = GroupsHandler.GetGroupViaId(groupId);
            string gid = group.GetId();
            foreach (WorldObject wo in inventory.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == gid)
                {
                    c++;
                }
            }

            string text = Readable.GetGroupName(group) + " + " + count + " (  " + c + "  )";
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            informationsDisplayer.AddInformation(2f, text, DataConfig.UiInformationsType.InInventory, group.GetImage());
        }

        void Update()
        {
            FieldInfo pi = typeof(Key).GetField(modeSwitchKey.Value.ToString().ToUpper());
            Key k = Key.V;
            if (pi != null)
            {
                k = (Key)pi.GetRawConstantValue();
            }
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (Keyboard.current[k].wasPressedThisFrame && wh != null && !wh.GetHasUiOpen())
            {
                if (getMultiplayerMode == null || getMultiplayerMode() != "CoopClient")
                {
                    autoMode = !autoMode;
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 1f, "Automatic mine/grab mode: " + autoMode);
                }
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Multiplayer Client mode not supported");
                }
            }
        }

        IEnumerator DetectionLoop()
        {
            for (; ; )
            {
                HandleDetection();
                yield return new WaitForSeconds(checkDelay.Value);
            }
        }

        static IEnumerable<Actionnable> Both(IEnumerable<ActionMinable> minables, IEnumerable<ActionGrabable> grabables)
        {
            foreach (var m in minables)
            {
                yield return m;
            }
            foreach (var g in grabables)
            {
                yield return g;
            }
        }

        void HandleDetection()
        {
            var player = Managers.GetManager<PlayersManager>()?.GetActivePlayerController();

            if (player == null)
            {
                return;
            }

            if (!autoMode)
            {
                return;
            }

            var r = radius.Value;
            var minables = FindMinableObjectsAround();
            var grabables = FindGrabableObjectsAround();
            var playerPos = player.transform.position;
            var inv = player.GetPlayerBackpack().GetInventory();

            int found = 0;
            int taken = 0;
            Dictionary<string, int> takenMap = new();

            foreach (var m in Both(minables, grabables))
            {
                var p = m.gameObject.transform.position;
                if (Vector3.Distance(playerPos, p) <= r)
                {
                    found++;
                    if (m.TryGetComponent<WorldObjectAssociated>(out var woa))
                    {
                        var wo = woa.GetWorldObject();
                        if (inv.AddItem(wo))
                        {
                            wo.SetDontSaveMe(false);

                            taken++;

                            var gid = wo.GetGroup().id;
                            takenMap.TryGetValue(gid, out var c);
                            takenMap[gid] = c + 1;

                            Destroy(m.gameObject);
                        }
                    }
                }
            }

            if (found != 0)
            {
                foreach (var kv in takenMap)
                {
                    ShowInventoryAdded(kv.Key, kv.Value, inv);
                }

                if (found != taken)
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Items found: " + found + ", taken: " + taken + " (inventory got full)");
                }
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Items found and taken: " + found);
                }
            }
        }

    }
}
