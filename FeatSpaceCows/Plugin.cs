// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Linq;
using BepInEx.Configuration;
using LibCommon;
using Unity.Netcode;

namespace FeatSpaceCows
{
    [BepInPlugin(modFeatSpaceCowsGuid, "(Feat) Space Cows", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatSpaceCowsGuid = "akarnokd.theplanetcraftermods.featspacecows";
        
        const string funcAddRemoveSpaceCow = "SpaceCowAddRemove";

        static Plugin me;

        internal static ManualLogSource logger;

        static ConfigEntry<bool> debugMode;

        static Texture2D cow1;

        static readonly Dictionary<int, SpaceCow> cowAroundSpreader = [];

        static readonly float productionSpeed = 120;

        static readonly float animalUnitsPerTick = 60;

        static readonly int shadowContainerId = 5000;

        static Coroutine cowChecker;

        static AccessTools.FieldRef<WorldObjectsHandler, HashSet<WorldObject>> fWorldObjectsHandlerItemsPickablesWorldObjects;

        static bool oncePerSave;

        private void Awake()
        {
            me = this;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Config.Bind("General", "Enabled", true, "Enable this mod?").Value)
            {
                debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

                logger = Logger;

                Assembly me = Assembly.GetExecutingAssembly();
                string dir = Path.GetDirectoryName(me.Location);

                cow1 = LoadPNG(Path.Combine(dir, "SpaceCow1.png"));

                fWorldObjectsHandlerItemsPickablesWorldObjects = AccessTools.FieldRefAccess<WorldObjectsHandler, HashSet<WorldObject>>("_itemsPickablesWorldObjects");

                LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
                var h = Harmony.CreateAndPatchAll(typeof(Plugin));

                ModNetworking.Init(modFeatSpaceCowsGuid, logger);
                ModNetworking.Patch(h);
                ModNetworking._debugMode = debugMode.Value;
                ModNetworking.RegisterFunction(funcAddRemoveSpaceCow, OnAddRemoveSpaceCow);

                ModPlanetLoaded.Patch(h, modFeatSpaceCowsGuid, _ => PlanetLoader_HandleDataAfterLoad());
            } 
            else
            {
                Logger.LogInfo($"Plugin is disabled via config.");
            }
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new(300, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static void Log(string s)
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
                str += ", " + wo.GetPlanetHash();
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
                Log("Player is not ready");
                return;
            }
            if (NetworkManager.Singleton?.IsHost ?? true)
            {
                Log("Total SpaceCow Count = " + cowAroundSpreader.Count);
                HashSet<int> found = [];
                List<WorldObject> allSpreaders = [];
                foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
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
                        CreateCow(wo);
                    }
                }

                SendAddSpaceCowAll();

                foreach (var id in new List<KeyValuePair<int, SpaceCow>>(cowAroundSpreader))
                {
                    if (!found.Contains(id.Key))
                    {
                        Log("GrassSpreader1 no longer exist, removing SpaceCow @ " + id.Key);
                        RemoveCow(id.Value);
                        cowAroundSpreader.Remove(id.Key);
                    }
                }
            }
            /*
            else
            {
                Log("Multiplayer: we run on the client.");
            }
            */
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

        void CreateCow(WorldObject wo)
        {
            Log("Adding SpaceCow around " + DebugWorldObject(wo));
            SpaceCow cow = SpaceCow.CreateCow(cow1, Color.white);
            cow.parent = wo;

            SetupInventory(cow);

            cowAroundSpreader[wo.GetId()] = cow;
            SendAddSpaceCow(cow);

        }

        void RemoveCow(SpaceCow cow)
        {
            var invAssoc = cow.body.GetComponent<InventoryAssociated>();
            invAssoc?.GetInventory(InventoriesHandler.Instance.RemoveAndDestroyAllItemsFromInventory);

            SendRemoveSpaceCow(cow);
            
            if (cow.inventory != null)
            {
                InventoriesHandler.Instance.DestroyInventory(cow.inventory.GetId(), false);
            }
            cow.Destroy(me);
        }

        void SetupInventory(SpaceCow cow)
        {
            var invAssoc = cow.body.AddComponent<InventoryAssociated>();
            var mapping = LoadCowInventoryMapping();
            if (mapping.TryGetValue(cow.parent.GetId(), out var invId))
            {
                Inventory inv = InventoriesHandler.Instance.GetInventoryById(invId);
                if (inv != null)
                {
                    Log("       Using existing inventory: " + inv.GetId());
                    LinkInventory(inv, false);
                    return;
                }
            }

            InventoriesHandler.Instance.CreateNewInventory(3, 0, 0, null, null, inv => LinkInventory(inv, true));

            void LinkInventory(Inventory inv, bool save)
            {
                Log("       Creating inventory: " + inv.GetId());

                invAssoc.SetInventory(inv);
                cow.inventory = inv;

                inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("WaterBottle1"));
                inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("astrofood"));
                inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("MethanCapsule1"));

                var aop = cow.body.AddComponent<SpaceCowActionOpenable>();
                aop.inventory = inv;

                if (save)
                {
                    mapping[cow.parent.GetId()] = inv.GetId();
                    SaveCowInventoryMapping(mapping);
                }

                cow.cowGrowthCoroutine = me.StartCoroutine(SpaceCowGeneratorLoop(productionSpeed, cow, inv));
                cow.cowVisibleCoroutine = me.StartCoroutine(SpaceCowVisibilityLoop(1f, cow));
            }
        }

        Dictionary<int, int> LoadCowInventoryMapping()
        {
            Dictionary<int, int> result = [];

            var wo = EnsureHiddenContainer();

            var txt = wo.GetText();
            if (txt != null && txt.Length != 0)
            {
                foreach (var kwPair in txt.Split(';'))
                {
                    var kv = kwPair.Split(',');
                    if (kv.Length >= 2 && int.TryParse(kv[0], out var id) && int.TryParse(kv[1], out var inv))
                    {
                        result[id] = inv;
                    }
                }
            }
            //logger.LogInfo("~ Cow Inventory Mapping: " + string.Join(";", result.Select(e => e.Key + "," + e.Value)));

            return result;
        }
        void SaveCowInventoryMapping(Dictionary<int, int> mapping)
        {
            var wo = EnsureHiddenContainer();
            wo.SetText(string.Join(";", mapping.Select(e => e.Key + "," + e.Value)));
        }

        IEnumerator SpaceCowGeneratorLoop(float delay, SpaceCow cow, Inventory inv)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(delay);
                SpaceCowGenerate(cow, inv);
            }
        }

        IEnumerator SpaceCowVisibilityLoop(float delay, SpaceCow cow)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(delay);
                if (cow.body == null)
                {
                    yield break;
                }
                var currentPlanetHash = 0;
                var pl = Managers.GetManager<PlanetLoader>();
                if (pl != null)
                {
                    var cp = pl.GetCurrentPlanetData();
                    if (cp != null)
                    {
                        currentPlanetHash = cp.GetPlanetHash();
                    }
                }
                var newState = cow.parent.GetPlanetHash() == currentPlanetHash;

                if (newState != cow.body.activeSelf)
                {
                    Log("Cow " + cow.parent.GetId() + " on planet " + cow.parent.GetPlanetHash()  + " visibility changed to " + newState);
                    if (newState)
                    {
                        cow.placementFailed = true;
                    }
                }

                cow.body.SetActive(newState);
                if (cow.placementFailed && (NetworkManager.Singleton?.IsServer ?? true))
                {
                    TryPlaceCow(cow);
                }
            }
        }

        void TryPlaceCow(SpaceCow cow)
        {
            var wo = cow.parent;
            List<WorldObject> allSpreaders = [];
            var currentPlanetHash = 0;
            var pl = Managers.GetManager<PlanetLoader>();
            if (pl != null)
            {
                var cp = pl.GetCurrentPlanetData(); 
                if (cp != null)
                {
                    currentPlanetHash = cp.GetPlanetHash();
                }
            }

            foreach (var wo1 in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                if (wo1.GetGroup().GetId() == "GrassSpreader1" && wo1.GetPlanetHash() == currentPlanetHash)
                {
                    allSpreaders.Add(wo1);
                }
            }

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
                Vector3 downward = new(0, -1, 0);
                float scanDown = 10f;
                int ignoredLayers = ~LayerMask.GetMask([.. GameConfig.commonIgnoredLayers, GameConfig.layerWaterName]);

                if (Physics.Raycast(new Ray(positionCandidateAbove, downward), out var raycastHit, scanDown, ignoredLayers))
                {
                    var lookTowards = (wo.GetPosition() - raycastHit.point).normalized;
                    var rot = Quaternion.LookRotation(lookTowards) * Quaternion.Euler(0, 90, 0);

                    cow.SetPosition(raycastHit.point, rot);
                    Log("SpaceCow at " + raycastHit.point + " radius " + positionRng.magnitude + " for " + DebugWorldObject(wo));
                    cow.placementFailed = false;
                    return;
                }
            }
            Log("Adding SpaceCow around failed, no valid spawn position found " + DebugWorldObject(wo));
        }

        void SpaceCowGenerate(SpaceCow cow, Inventory inv)
        {
            Log("SpaceCow Generator for " + DebugWorldObject(cow.parent));

            if (inv.GetInsideWorldObjects().Count == 0)
            {
                Log("         Depositing products");
                AddToInventory(inv, "WaterBottle1");
                AddToInventory(inv, "astrofood");
                AddToInventory(inv, "MethanCapsule1");
            }
            AddToWorldUnit(cow.parent.GetPlanetHash(), DataConfig.WorldUnitType.Animals, DataConfig.WorldUnitType.Biomass, DataConfig.WorldUnitType.Terraformation);

            // Drones can't access a cow because it is not a WorldObject
            // Instead, once we detect the cow's inventory has supply settings
            // We drop items onto the floor so drones would pick them up outside the cow.
            foreach (var supplyGroup in inv.GetLogisticEntity().GetSupplyGroups())
            {
                foreach (var content in new List<WorldObject>(inv.GetInsideWorldObjects()))
                {
                    if (content.GetGroup() == supplyGroup)
                    {
                        InventoriesHandler.Instance.RemoveItemFromInventory(content, inv, false, success =>
                        {
                            if (success)
                            {
                                var inst = WorldObjectsHandler.Instance;

                                content.SetPlanetHash(cow.parent.GetPlanetHash());

                                inst.DropOnFloor(content,
                                    cow.body.transform.position
                                    + cow.body.transform.forward * (UnityEngine.Random.value < 0.5f ? -2 : 2)
                                    + new Vector3(0, 0, 1), dropSound: false);

                                fWorldObjectsHandlerItemsPickablesWorldObjects(inst).Add(content);
                            }
                        });
                    }
                }
            }
        }

        void AddToWorldUnit(int planetHash, params DataConfig.WorldUnitType[] wut)
        {
            var pl = Managers.GetManager<PlanetLoader>();
            if (pl == null)
            {
                return;
            }
            var planet = pl.planetList.GetPlanetFromIdHash(planetHash);
            if (planet == null)
            {
                return;
            }
            var wuh = Managers.GetManager<WorldUnitsHandler>();
            if (wuh == null)
            {
                return;
            }
            foreach (var w in wut)
            {
                var wu = wuh.GetUnit(w, planet.id);

                var before = wu.GetValue();
                var after = before + animalUnitsPerTick;

                Log("         Producing WorldUnit(" + w + "): " + before + " -> " + after);

                wu.SetCurrentTotalValue((float)after);
            }
        }

        void AddToInventory(Inventory inv, string groupId)
        {
            var gr = GroupsHandler.GetGroupViaId(groupId);
            if (NetworkManager.Singleton?.IsServer ?? false)
            {
                InventoriesHandler.Instance.AddItemToInventory(gr, inv, (success, id) =>
                {
                    if (!success && id != 0)
                    {
                        WorldObjectsHandler.Instance.DestroyWorldObject(id);
                    }
                });
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
                    uiWindowContainer.SetSettingsData(null, 0, DataConfig.UiSettingType.Null);
                }
            }

            public override void OnHover()
            {
                _hudHandler.DisplayCursorText("UI_Open", 0f, "Space Cow");
            }
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            if (!oncePerSave)
            {
                oncePerSave = true;
            }
            if (cowChecker != null)
            {
                me.StopCoroutine(cowChecker);
                cowChecker = null;
            }
            cowChecker = me.StartCoroutine(me.SpreaderCheckerLoop(2f));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static void SavedDataHandler_SetAndGetWorldObjects(List<JsonableWorldObject> __result)
        {
            HashSet<int> woIdsToHide = [];
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
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            oncePerSave = false;
            if (cowChecker != null)
            {
                me.StopCoroutine(cowChecker);
                cowChecker = null;
            }
            Log("Clearing Cows = " + cowAroundSpreader.Count);
            foreach (var cow in cowAroundSpreader.Values)
            {
                cow.Destroy(me);
            }
            cowAroundSpreader.Clear();
            Log("                Done");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }


        static WorldObject EnsureHiddenContainer()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(shadowContainerId);
            if (wo == null)
            {
                wo = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container2"), shadowContainerId);
                wo.SetText("");
            }
            wo.SetDontSaveMe(false);
            return wo;
        }

        // -------------------------------------------------------------------------

        void SendAddSpaceCow(SpaceCow cow)
        {
            if (NetworkManager.Singleton?.IsServer ?? false) {
                var msg = new MessageAddRemoveSpaceCow
                {
                    parentId = cow.parent.GetId(),
                    inventoryId = cow.inventory.GetId(),
                    position = cow.rawPosition,
                    rotation = cow.rawRotation,
                    color = cow.color,
                    added = true,
                    visible = cow.body.activeSelf
                };

                Log("SpaceCow: Sending New " + msg.parentId + " on planet " + cow.parent.GetPlanetHash() + " (" + cow.parent.GetPosition() + ")");
                ModNetworking.SendAllClients(funcAddRemoveSpaceCow, msg.ToString());
            }
        }

        void SendAddSpaceCowAll()
        {
            if (NetworkManager.Singleton?.IsServer ?? false
                && cowAroundSpreader.Count != 0)
            {
                foreach (var cow in cowAroundSpreader.Values)
                {
                    var msg = new MessageAddRemoveSpaceCow
                    {
                        parentId = cow.parent.GetId(),
                        inventoryId = cow.inventory.GetId(),
                        position = cow.rawPosition,
                        rotation = cow.rawRotation,
                        color = cow.color,
                        added = true,
                        visible = cow.body.activeSelf
                    };

                    Log("SpaceCow: Sending " + msg.parentId + " on planet " + cow.parent.GetPlanetHash() + " (" + cow.parent.GetPosition() + ")");

                    ModNetworking.SendAllClients(funcAddRemoveSpaceCow, msg.ToString());
                }
            }
        }

        void SendRemoveSpaceCow(SpaceCow cow)
        {
            if (NetworkManager.Singleton?.IsServer ?? false)
            {
                var msg = new MessageAddRemoveSpaceCow
                {
                    parentId = cow.parent.GetId(),
                    inventoryId = cow.inventory.GetId()
                };

                Log("SpaceCow: Removing at " + msg.parentId + " on planet " + cow.parent.GetPlanetHash() + " (" + cow.parent.GetPosition() + ")");

                ModNetworking.SendAllClients(funcAddRemoveSpaceCow, msg.ToString());
            }
        }

        void OnAddRemoveSpaceCow(ulong sender, string parameters)
        {
            if (MessageAddRemoveSpaceCow.TryParse(parameters, out var msg))
            {
                ReceiveMessageAddRemoveSpaceCow(msg);
            }
            else
            {
                Log("Invalid or unknown message: " + parameters);
            }
        }

        void ReceiveMessageAddRemoveSpaceCow(MessageAddRemoveSpaceCow msg)
        {
            bool isServer = NetworkManager.Singleton?.IsServer ?? true;
            Log("ReceiveMessageAddRemoveSpaceCow: isServer = " + isServer);
            if (!isServer)
            {
                Log("ReceiveMessageAddRemoveSpaceCow: " + msg.ToString());
                if (msg.added)
                {
                    if (!cowAroundSpreader.TryGetValue(msg.parentId, out var cow))
                    {
                        Log("ReceiveMessageAddRemoveSpaceCow: Creating for " + msg.parentId + " at " + msg.position);
                        cow = SpaceCow.CreateCow(cow1, msg.color);
                        cow.parent = WorldObjectsHandler.Instance.GetWorldObjectViaId(msg.parentId);
                        if (cow.parent == null)
                        {
                            Log("ReceiveMessageAddRemoveSpaceCow: Unable to find parent on client: " + msg.parentId);
                        }

                        var invAssoc = cow.body.AddComponent<InventoryAssociated>();

                        void onInventory(Inventory inv)
                        {
                            Log("ReceiveMessageAddRemoveSpaceCow: Preparing inventory " + inv.GetId() + " for " + msg.parentId);
                            invAssoc.SetInventory(inv);
                            cow.inventory = inv;

                            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("WaterBottle1"));
                            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("astrofood"));
                            inv.AddAuthorizedGroup(GroupsHandler.GetGroupViaId("MethanCapsule1"));

                            var aop = cow.body.AddComponent<SpaceCowActionOpenable>();
                            aop.inventory = inv;
                        }

                        var invExist = InventoriesHandler.Instance.GetInventoryById(msg.inventoryId);
                        if (invExist == null)
                        {
                            InventoriesHandler.Instance.GetInventoryById(msg.inventoryId, onInventory);
                        }
                        else
                        {
                            Log("ReceiveMessageAddRemoveSpaceCow: Inventory Found: " + invExist.GetId());
                            onInventory(invExist);
                        }

                        cowAroundSpreader[msg.parentId] = cow;
                    }

                    Log("ReceiveMessageAddRemoveSpaceCow: Updating position of " 
                        + msg.parentId + " at " + msg.position + ", " + msg.rotation + ", Color = " 
                        + msg.color + ", Visible: " + msg.visible);
                    cow.SetPosition(msg.position, msg.rotation);
                    cow.SetColor(msg.color);
                    cow.body.SetActive(msg.visible);

                    if (cow.inventory != null)
                    {
                        var iws = cow.inventory.GetInsideWorldObjects();
                        Log("          Products: " + iws.Count);
                        foreach (WorldObject wo in iws)
                        {
                            Log("             " + DebugWorldObject(wo));
                        }
                        // keep it up to date
                        InventoriesHandler.Instance.GetInventoryById(msg.inventoryId, _ => { });                    }
                }
                else
                {
                    if (cowAroundSpreader.TryGetValue(msg.parentId, out var cow))
                    {
                        logger.LogInfo("ReceiveMessageAddRemoveSpaceCow: Removing of " + msg.parentId);
                        cow.Destroy(me);
                        cowAroundSpreader.Remove(msg.parentId);
                    }
                }
            }
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            ModNetworking._debugMode = debugMode.Value;
        }
    }
}
