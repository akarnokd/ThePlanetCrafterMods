using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Linq;
using BepInEx.Configuration;
using System;
using BepInEx.Bootstrap;
using FeatMultiplayer;
using System.Collections.Concurrent;

namespace FeatSpaceCows
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featspacecows", "(Feat) Space Cows", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        static ConfigEntry<bool> debugMode;

        static Texture2D cow1;

        static readonly Dictionary<int, SpaceCow> cowAroundSpreader = new();

        static float productionSpeed = 120;

        static float animalUnitsPerTick = 60;

        internal static FeatMultiplayerApi multiplayer;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Config.Bind("General", "Enabled", true, "Enable this mod?").Value)
            {
                debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

                logger = Logger;

                Assembly me = Assembly.GetExecutingAssembly();
                string dir = Path.GetDirectoryName(me.Location);

                cow1 = LoadPNG(Path.Combine(dir, "SpaceCow1.png"));

                multiplayer = FeatMultiplayerApi.Create();
                if (multiplayer.IsAvailable()) { 
                    Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, enabling multiplayer support");

                    multiplayer.AddMessageParser(TryAddRemoveSpaceCowMessageParser);
                    multiplayer.AddMessageReceiver(TryAddRemoveSpaceCowMessageReceiver);
                }

                Harmony.CreateAndPatchAll(typeof(Plugin));

                StartCoroutine(SpreaderCheckerLoop(2f));
            } 
            else
            {
                Logger.LogInfo($"Plugin is disabled via config.");
            }
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(300, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        string DebugWorldObject(WorldObject wo)
        {
            if (wo != null)
            {
                var str = wo.GetId() + ", " + wo.GetGroup().GetId();
                var txt = wo.GetText();
                if (!string.IsNullOrEmpty(txt))
                {
                    str += ", \"" + txt + "\"";
                }
                str += ", " + (wo.GetIsPlaced() ? wo.GetPosition() : "");
                return str;
            }
            return "null";
        }

        IEnumerator SpreaderCheckerLoop(float delay)
        {
            for (; ; )
            {
                CheckGrassSpreaders();
                yield return new WaitForSeconds(delay);
            }
        }

        void CheckGrassSpreaders()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p == null || p.GetActivePlayerController() == null)
            {
                log("Player is not ready");
                return;
            }
            if (!multiplayer.IsAvailable() || multiplayer.GetState() != FeatMultiplayerApi.MultiplayerState.CoopClient)
            {
                log("Total SpaceCow Count = " + cowAroundSpreader.Count);
                HashSet<int> found = new();
                List<WorldObject> allSpreaders = new();
                foreach (var wo in WorldObjectsHandler.GetConstructedWorldObjects())
                {
                    if (wo.GetGroup().GetId() == "GrassSpreader1")
                    {
                        allSpreaders.Add(wo);
                    }
                }

                foreach (var wo in allSpreaders)
                {
                    var id = wo.GetId();
                    found.Add(id);
                    if (!cowAroundSpreader.ContainsKey(id))
                    {
                        CreateCow(wo, allSpreaders);
                    }

                }

                SendAddSpaceCowAll();

                foreach (var id in new List<KeyValuePair<int, SpaceCow>>(cowAroundSpreader))
                {
                    if (!found.Contains(id.Key))
                    {
                        log("GrassSpreader1 no longer exist, removing SpaceCow @ " + id.Key);
                        RemoveCow(id.Value);
                        cowAroundSpreader.Remove(id.Key);
                    }
                }
            }
            else
            {
                log("Multiplayer: we run on the client.");
            }
        }

        static Vector2 OnCircle(float radius, float angleRadians)
        {
            return new Vector2(radius * Mathf.Cos(angleRadians), radius * Mathf.Sin(angleRadians));
        }

        bool CheckCollisions(Vector3 positionCandidate, List<WorldObject> allSpreaders)
        {
            float minDistance = 3f;
            foreach (var otherCow in cowAroundSpreader.Values)
            {
                if (Vector3.Distance(otherCow.rawPosition, positionCandidate) < minDistance)
                {
                    return true;
                }
            }

            foreach (var wo in allSpreaders)
            {
                if (Vector3.Distance(wo.GetPosition(), positionCandidate) < minDistance)
                {
                    return true;
                }
            }
            return false;
        }

        void CreateCow(WorldObject wo, List<WorldObject> allSpreaders)
        {
            int tries = 100;
            while (tries-- > 0)
            {
                var positionRng = OnCircle(UnityEngine.Random.Range(3, 6), UnityEngine.Random.Range(0, Mathf.PI * 2));

                var positionCandidate = wo.GetPosition() + new Vector3(positionRng.x, 0, positionRng.y);

                if (CheckCollisions(positionCandidate, allSpreaders))
                {
                    continue;
                }

                Vector3 positionCandidateAbove = positionCandidate + new Vector3(0, 6, 0);
                Vector3 downward = new Vector3(0, -1, 0);
                float scanDown = 10f;
                int ignoredLayers = ~LayerMask.GetMask(GameConfig.commonIgnoredLayers.Concat(new string[] { GameConfig.layerWaterName }).ToArray<string>()); ;

                if (Physics.Raycast(new Ray(positionCandidateAbove, downward), out var raycastHit, scanDown, ignoredLayers))
                {

                    var lookTowards = (wo.GetPosition() - raycastHit.point).normalized;
                    var rot = Quaternion.LookRotation(lookTowards) * Quaternion.Euler(0, 90, 0);

                    log("Adding SpaceCow around " + DebugWorldObject(wo));
                    SpaceCow cow = SpaceCow.CreateCow(cow1, Color.white);
                    cow.parent = wo;
                    cow.SetPosition(raycastHit.point, rot);
                    log("       SpaceCow at " + raycastHit.point + " radius " + positionRng.magnitude);

                    SetupInventory(cow);

                    cowAroundSpreader[wo.GetId()] = cow;

                    SendAddSpaceCow(cow);

                    return;
                }
            }
            log("Adding SpaceCow around failed, no valid spawn position found" + DebugWorldObject(wo));
        }

        void RemoveCow(SpaceCow cow)
        {
            var invAssoc = cow.body.GetComponent<InventoryAssociated>();
            if (invAssoc != null)
            {
                var wos = invAssoc.GetInventory().GetInsideWorldObjects();
                for (int i = wos.Count - 1; i >= 0; i--)
                {
                    WorldObjectsHandler.DestroyWorldObject(wos[i]);
                }
                wos.Clear();
            }
            SendRemoveSpaceCow(cow);
            if (cow.inventory != null)
            {
                InventoriesHandler.DestroyInventory(cow.inventory.GetId());
            }
            cow.Destroy();
        }

        void SetupInventory(SpaceCow cow)
        {
            var invAssoc = cow.body.AddComponent<InventoryAssociated>();
            var inv = InventoriesHandler.CreateNewInventory(3);
            invAssoc.SetInventory(inv);
            cow.inventory = inv;

            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("WaterBottle1"));
            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("astrofood"));
            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("MethanCapsule1"));

            var aop = cow.body.AddComponent<SpaceCowActionOpenable>();
            aop.inventory = inv;

            invAssoc.StartCoroutine(SpaceCowGeneratorLoop(productionSpeed, cow, inv));
        }

        IEnumerator SpaceCowGeneratorLoop(float delay, SpaceCow cow, Inventory inv)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(delay);
                SpaceCowGenerate(cow, inv);
            }
        }

        void SpaceCowGenerate(SpaceCow cow, Inventory inv)
        {
            log("SpaceCow Generator for " + DebugWorldObject(cow.parent));

            if (inv.GetInsideWorldObjects().Count == 0)
            {
                log("         Depositing products");
                AddToInventory(inv, "WaterBottle1");
                AddToInventory(inv, "astrofood");
                AddToInventory(inv, "MethanCapsule1");
            }
            AddToWorldUnit(DataConfig.WorldUnitType.Animals, DataConfig.WorldUnitType.Biomass, DataConfig.WorldUnitType.Terraformation);
        }

        void AddToWorldUnit(params DataConfig.WorldUnitType[] wut)
        {
            var wuh = Managers.GetManager<WorldUnitsHandler>();
            if (wuh != null)
            {
                foreach (var w in wut)
                {
                    var wu = wuh.GetUnit(w);

                    var before = wu.GetValue();
                    var after = before + animalUnitsPerTick;

                    log("         Producing WorldUnit(" + w + "): " + before + " -> " + after);

                    AccessTools.Field(typeof(WorldUnit), "currentTotalValue").SetValue(wu, after);
                }
            }
        }

        void AddToInventory(Inventory inv, string groupId)
        {
            var gr = GroupsHandler.GetGroupViaId(groupId);
            var wo = WorldObjectsHandler.CreateNewWorldObject(gr);
            if (multiplayer.IsAvailable() && multiplayer.GetState() == FeatMultiplayerApi.MultiplayerState.CoopHost)
            {
                multiplayer.SendWorldObject(wo);
            }
            if (!inv.AddItem(wo))
            {
                WorldObjectsHandler.DestroyWorldObject(wo);
            }
        }

        class SpaceCowActionOpenable : ActionOpenable
        {
            internal Inventory inventory;

            public override void OnAction()
            {
                Inventory backpack = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerBackpack()
                    .GetInventory();
                UiWindowContainer uiWindowContainer = (UiWindowContainer)Managers.GetManager<WindowsHandler>().OpenAndReturnUi(DataConfig.UiType.Container);
                if (uiWindowContainer != null)
                {
                    uiWindowContainer.SetInventories(backpack, inventory, false);
                }
            }

            public override void OnHover()
            {
                hudHandler.DisplayCursorText("UI_Open", 0f, "Space Cow");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static void SavedDataHandler_SetAndGetWorldObjects(List<JsonableWorldObject> __result)
        {
            HashSet<int> woIdsToHide = new();
            foreach (var cow in cowAroundSpreader.Values)
            {
                if (cow.inventory != null)
                {
                    foreach (var wo in cow.inventory.GetInsideWorldObjects())
                    {
                        woIdsToHide.Add(wo.GetId());
                    }
                }
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                if (woIdsToHide.Contains(__result[i].id))
                {
                    __result.RemoveAt(i);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetInventories")]
        static void SavedDataHandler_GetAndSetInventories(List<JsonableInventory> __result)
        {
            HashSet<int> invsToHide = new();
            foreach (var cow in cowAroundSpreader.Values)
            {
                if (cow.inventory != null)
                {
                    invsToHide.Add(cow.inventory.GetId());
                }
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                if (invsToHide.Contains(__result[i].id))
                {
                    __result.RemoveAt(i);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            log("Clearing Cows = " + cowAroundSpreader.Count);
            cowAroundSpreader.Clear();
            log("                Done");
        }

        // -------------------------------------------------------------------------

        void SendAddSpaceCow(SpaceCow cow)
        {
            if (multiplayer.IsAvailable() && multiplayer.GetState() == FeatMultiplayerApi.MultiplayerState.CoopHost) {
                var msg = new MessageAddRemoveSpaceCow();
                msg.parentId = cow.parent.GetId();
                msg.inventoryId = cow.inventory.GetId();
                msg.position = cow.rawPosition;
                msg.rotation = cow.rawRotation;
                msg.color = cow.color;
                msg.added = true;

                multiplayer.LogInfo("SpaceCow: Sending New " + msg.parentId + " (" + cow.parent.GetPosition() + ")");
                multiplayer.Send(msg);
                multiplayer.Signal();
            }
        }

        void SendAddSpaceCowAll()
        {
            if (multiplayer.IsAvailable()
                && multiplayer.GetState() == FeatMultiplayerApi.MultiplayerState.CoopHost
                && cowAroundSpreader.Count != 0)
            {
                foreach (var cow in cowAroundSpreader.Values)
                {
                    var msg = new MessageAddRemoveSpaceCow();
                    msg.parentId = cow.parent.GetId();
                    msg.inventoryId = cow.inventory.GetId();
                    msg.position = cow.rawPosition;
                    msg.rotation = cow.rawRotation;
                    msg.color = cow.color;
                    msg.added = true;

                    multiplayer.LogInfo("SpaceCow: Sending " + msg.parentId + " (" + cow.parent.GetPosition() + ")");

                    multiplayer.Send(msg);
                }
                multiplayer.Signal();
            }
        }

        void SendRemoveSpaceCow(SpaceCow cow)
        {
            if (multiplayer.IsAvailable() && multiplayer.GetState() == FeatMultiplayerApi.MultiplayerState.CoopHost)
            {
                var msg = new MessageAddRemoveSpaceCow();
                msg.parentId = cow.parent.GetId();
                msg.inventoryId = cow.inventory.GetId();

                multiplayer.LogInfo("SpaceCow: Removing at " + msg.parentId + " (" + cow.parent.GetPosition() + ")");

                multiplayer.Send(msg);
                multiplayer.Signal();
            }
        }

        bool TryAddRemoveSpaceCowMessageParser(int sender, string message, ConcurrentQueue<object> queue)
        {
            if (MessageAddRemoveSpaceCow.TryParse(message, out var msg))
            {
                queue.Enqueue(msg);
                return true;
            }
            return false;
        }

        bool TryAddRemoveSpaceCowMessageReceiver(object msg)
        {
            if (msg is MessageAddRemoveSpaceCow m)
            {
                ReceiveMessageAddRemoveSpaceCow(m);
                return true;
            }
            return false;
        }

        void ReceiveMessageAddRemoveSpaceCow(MessageAddRemoveSpaceCow msg)
        {
            if (multiplayer.GetState() == FeatMultiplayerApi.MultiplayerState.CoopClient)
            {
                if (msg.added)
                {
                    if (!cowAroundSpreader.TryGetValue(msg.parentId, out var cow))
                    {
                        multiplayer.LogInfo("SpaceCow: Creating for " + msg.parentId + " at " + msg.position);
                        cow = SpaceCow.CreateCow(cow1, msg.color);

                        var invAssoc = cow.body.AddComponent<InventoryAssociated>();
                        var inv = InventoriesHandler.GetInventoryById(msg.inventoryId);
                        if (inv == null)
                        {
                            inv = InventoriesHandler.CreateNewInventory(3, msg.inventoryId);
                        }
                        invAssoc.SetInventory(inv);
                        cow.inventory = inv;

                        inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("WaterBottle1"));
                        inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("astrofood"));
                        inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("MethanCapsule1"));

                        var aop = cow.body.AddComponent<SpaceCowActionOpenable>();
                        aop.inventory = inv;

                        cowAroundSpreader[msg.parentId] = cow;
                    }

                    multiplayer.LogInfo("SpaceCow: Updating position of " + msg.parentId + " at " + msg.position + ", " + msg.rotation + ", Color = " + msg.color);
                    cow.SetPosition(msg.position, msg.rotation);
                    cow.SetColor(msg.color);

                    var iws = cow.inventory.GetInsideWorldObjects();
                    multiplayer.LogInfo("          Products: " + iws.Count);
                    foreach (WorldObject wo in iws)
                    {
                        multiplayer.LogInfo("             " + DebugWorldObject(wo));
                    }
                }
                else
                {
                    if (cowAroundSpreader.TryGetValue(msg.parentId, out var cow))
                    {
                        multiplayer.LogInfo("SpaceCow: Removing of " + msg.parentId);
                        var invAssoc = cow.body.GetComponent<InventoryAssociated>();
                        if (invAssoc != null)
                        {
                            invAssoc.GetInventory().GetInsideWorldObjects().Clear();
                        }
                        if (cow.inventory != null)
                        {
                            InventoriesHandler.DestroyInventory(cow.inventory.GetId());
                        }
                        cow.Destroy();
                        cowAroundSpreader.Remove(msg.parentId);
                    }
                }
            }
        }
    }
}
