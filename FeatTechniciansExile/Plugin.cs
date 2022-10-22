using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using System.IO.Compression;
using System.Text;
using System.Collections;
using UnityEngine;
using MijuTools;
using BepInEx.Bootstrap;
using UnityEngine.InputSystem;
using System.Reflection;
using UnityEngine.UI;
using TMPro;

namespace FeatTechniciansExile
{
    [BepInPlugin("akarnokd.theplanetcraftermods.feattechniciansexile", "(Feat) Technicians Exile", "0.1.0.0")]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        internal static Texture2D technicianFront;
        internal static Texture2D technicianBack;

        static Func<string> multiplayerMode;

        // /tp 1990 76 1073
        static Vector3 technicianDropLocation = new Vector3(1995, 75.7f, 1073);

        static TechnicianAvatar avatar;

        static readonly int technicianWorldObjectIdStart = 70000;

        static WorldObject escapePod;
        static WorldObject livingPod;
        static WorldObject desk;
        static WorldObject bed;
        static WorldObject screen;
        static WorldObject grower;
        static WorldObject collector;
        static WorldObject flower;
        static WorldObject chair;
        static WorldObject chest;
        static WorldObject solar;
        static readonly List<WorldObject> baseComponents = new();

        static Vector3 technicianLocation1;
        static Quaternion technicianRotation1;

        static Vector3 technicianLocation2;
        static Quaternion technicianRotation2;

        static Vector3 technicianLocation3;
        static Quaternion technicianRotation3;

        enum QuestPhase
        {
            Not_Started,
            Arrival,
            Initial_Help,
            Base_Setup,
            Operating
        }

        static QuestPhase questPhase = QuestPhase.Not_Started;

        static bool ingame;

        static Asteroid asteroid;

        static FieldInfo asteroidHasCrashed;

        static readonly List<ConversationEntry> conversationHistory = new();

        static readonly Dictionary<string, DialogChoice> dialogChoices = new();

        static readonly Dictionary<string, Dictionary<string, string>> labels = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);
            }
            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            technicianFront = LoadPNG(Path.Combine(dir, "Technician_Front.png"));
            technicianBack = LoadPNG(Path.Combine(dir, "Technician_Back.png"));

            asteroidHasCrashed = AccessTools.Field(typeof(Asteroid), "hasCrashed");

            AddTranslation(dir, "english");
            AddTranslation(dir, "hungarian");

            PrepareDialogChoices();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void AddTranslation(string dir, string language)
        {
            var lines = File.ReadAllLines(Path.Combine(dir, "technician_labels_" + language + ".txt"));

            if (!labels.TryGetValue(language, out var dict))
            {
                dict = new();
                labels[language] = dict;
            }

            foreach (var line in lines)
            {
                if (!line.StartsWith("#") && line.Contains("="))
                {
                    string[] parts = line.Split('=');
                    dict[parts[0]] = parts[1];
                }
            }
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(100, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            bool dontSaveMe = true;

            logger.LogInfo("Start");

            baseComponents.Clear();

            logger.LogInfo("  Finding the Escape Pod");
            escapePod = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart);
            if (escapePod == null)
            {
                logger.LogInfo("    Creating the Escape Pod");
                escapePod = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("EscapePod"), 
                    technicianWorldObjectIdStart);
                escapePod.SetPositionAndRotation(technicianDropLocation, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                escapePod.SetDontSaveMe(false);
                WorldObjectsHandler.InstantiateWorldObject(escapePod, false);
            }

            var livingPodBase = technicianDropLocation + new Vector3(0, 0, 15);

            logger.LogInfo("  Finding the Living Pod");
            livingPod = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 1);
            if (livingPod == null)
            {
                logger.LogInfo("    Creating the Living Pod");
                livingPod = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("pod"), 
                    technicianWorldObjectIdStart + 1);
                livingPod.SetPositionAndRotation(livingPodBase, Quaternion.identity * Quaternion.Euler(0, 90, 0));
                livingPod.SetPanelsId(new List<int> { 1, 4, 1, 1, 5, 7 });
                livingPod.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(livingPod, false);
            }

            var livingPodFloor = livingPodBase + new Vector3(0, 1, 0);

            var bedPosition = livingPodFloor + new Vector3(3, 0, -1);

            logger.LogInfo("  Finding the Bed");
            bed = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 2);
            if (bed == null)
            {
                logger.LogInfo("    Creating the Bed");
                bed = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("BedSimple"), 
                    technicianWorldObjectIdStart + 2);
                bed.SetPositionAndRotation(bedPosition, Quaternion.identity * Quaternion.Euler(0, 180, 0));
                bed.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(bed, false);
            }

            var deskPosition = livingPodFloor + new Vector3(-1, 0, -3);
            logger.LogInfo("  Finding the Desk");
            desk = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 3);
            if (desk == null)
            {
                logger.LogInfo("    Creating the Desk");
                desk = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Desktop1"), 
                    technicianWorldObjectIdStart + 3);
                desk.SetPositionAndRotation(deskPosition, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                desk.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(desk, false);
            }

            logger.LogInfo("  Finding the Rockets Screen");
            screen = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 4);
            if (screen == null)
            {
                logger.LogInfo("    Creating the Rockets Screen");
                screen = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("ScreenRockets"),
                    technicianWorldObjectIdStart + 4);
                screen.SetPositionAndRotation(deskPosition + new Vector3(-0.5f, 1f, 0.25f), Quaternion.identity * Quaternion.Euler(0, 180, 0));
                screen.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(screen, false);
            }

            logger.LogInfo("  Finding the Grower");
            grower = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 5);
            if (grower == null)
            {
                logger.LogInfo("    Creating the Grower");
                grower = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("VegetableGrower1"),
                    technicianWorldObjectIdStart + 5);
                grower.SetPositionAndRotation(livingPodFloor + new Vector3(2, 0, 2), Quaternion.identity * Quaternion.Euler(0, -45, 0));
                grower.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(grower, false);
            }

            logger.LogInfo("  Finding the Collector");
            collector = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 6);
            if (collector == null)
            {
                logger.LogInfo("    Creating the Collector");
                collector = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("WaterCollector1"),
                    technicianWorldObjectIdStart + 6);
                collector.SetPositionAndRotation(livingPodBase + new Vector3(0, -1, 15), Quaternion.identity);
                collector.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(collector, false);
            }

            logger.LogInfo("  Finding Flower Pot");
            flower = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 7);
            if (flower == null)
            {
                logger.LogInfo("    Creating the Flower Pot");
                flower = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("FlowerPot1"),
                    technicianWorldObjectIdStart + 7);
                flower.SetPositionAndRotation(livingPodFloor + new Vector3(-2, 0, 2.25f), Quaternion.identity);
                flower.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(flower, false);

                Inventory inv = InventoriesHandler.GetInventoryById(flower.GetLinkedInventoryId());
                WorldObject flowerSeed = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("SeedGold"));
                if (!inv.AddItem(flowerSeed))
                {
                    WorldObjectsHandler.DestroyWorldObject(flowerSeed);
                }
            }

            logger.LogInfo("  Finding Chair");
            chair = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 8);
            if (chair == null)
            {
                logger.LogInfo("    Creating the Chair");
                chair = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Chair1"),
                    technicianWorldObjectIdStart + 8);
                chair.SetPositionAndRotation(deskPosition + new Vector3(1f, 0, 2), Quaternion.identity * Quaternion.Euler(0, -45, 0));
                chair.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(chair, false);
            }

            logger.LogInfo("  Finding Chest");
            chest = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 9);
            if (chest == null)
            {
                logger.LogInfo("    Creating the Chest");
                chest = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"),
                    technicianWorldObjectIdStart + 9);
                chest.SetPositionAndRotation(livingPodFloor + new Vector3(-0.25f, 0, 2.25f), Quaternion.identity * Quaternion.Euler(0, 90, 0));
                chest.SetDontSaveMe(dontSaveMe);
                chest.SetText("Technician #221021");
                WorldObjectsHandler.InstantiateWorldObject(chest, false);
            }

            logger.LogInfo("  Finding Solar");
            solar = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 10);
            if (solar == null)
            {
                logger.LogInfo("    Creating the Solar");
                solar = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("EnergyGenerator2"),
                    technicianWorldObjectIdStart + 10);
                solar.SetPositionAndRotation(livingPodFloor + new Vector3(0, 5, 0), Quaternion.identity);
                solar.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(solar, false);
            }

            technicianLocation1 = technicianDropLocation + new Vector3(0, -0.5f, 0);
            technicianRotation1 = Quaternion.identity * Quaternion.Euler(0, -90, 0);

            technicianLocation2 = deskPosition + new Vector3(-0.25f, 0, 2);
            technicianRotation2 = Quaternion.identity * Quaternion.Euler(0, 180, 0);

            technicianLocation3 = bedPosition + new Vector3(-0.85f, -0.45f, 0);
            technicianRotation3 = Quaternion.identity * Quaternion.Euler(-90, 0, 0);

            logger.LogInfo("  Creating the Technician");
            avatar = TechnicianAvatar.CreateAvatar(Color.white);
            avatar.SetPosition(technicianLocation1, technicianRotation1);

            var at = avatar.avatar.AddComponent<ActionTalk>();

            at.OnConversationStart = DoConversationStart;
            at.OnConversationChoice = DoConversationChoice;

            logger.LogInfo("  Limiting object interactions");
            Destroy(livingPod.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(desk.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(bed.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(screen.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(grower.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(grower.GetGameObject().GetComponentInChildren<ActionnableInteractive>());
            Destroy(grower.GetGameObject().GetComponentInChildren<ActionOpenable>());
            Destroy(collector.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(collector.GetGameObject().GetComponentInChildren<ActionnableInteractive>());
            Destroy(collector.GetGameObject().GetComponentInChildren<ActionOpenable>());
            Destroy(flower.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(flower.GetGameObject().GetComponentInChildren<ActionnableInteractive>());
            Destroy(flower.GetGameObject().GetComponentInChildren<ActionOpenable>());
            Destroy(chair.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(chest.GetGameObject().GetComponentInChildren<ActionnableInteractive>());
            Destroy(chest.GetGameObject().GetComponentInChildren<ActionOpenable>());
            Destroy(chest.GetGameObject().GetComponentInChildren<ActionDeconstructible>());
            Destroy(chest.GetGameObject().GetComponentInChildren<ActionOpenUi>());
            Destroy(solar.GetGameObject().GetComponentInChildren<ActionDeconstructible>());

            baseComponents.Add(livingPod);
            baseComponents.Add(desk);
            baseComponents.Add(bed);
            baseComponents.Add(screen);
            baseComponents.Add(grower);
            baseComponents.Add(collector);
            baseComponents.Add(flower);
            baseComponents.Add(chair);
            baseComponents.Add(chest);
            baseComponents.Add(solar);

            LoadState();
            SetVisibilityViaCurrentPhase();

            ingame = true;
            logger.LogInfo("Done");
        }

        void Update()
        {
            if (!ingame)
            {
                return;
            }

            var player = GetPlayerMainController();

            switch (questPhase)
            {
                case QuestPhase.Not_Started:
                    {
                        CheckStartConditions();
                        break;
                    }
                case QuestPhase.Arrival:
                    {
                        CheckArrival();
                        break;
                    }
                case QuestPhase.Initial_Help:
                    {
                        CheckInitialHelp();
                        break;
                    }
                case QuestPhase.Base_Setup:
                    {
                        CheckBaseSetup();
                        break;
                    }
                case QuestPhase.Operating:
                    {
                        CheckOperating();
                        break;
                    }
            }
        }

        static void DoConversationStart(ActionTalk talk)
        {
            talk.currentImage = avatar.avatarFront.GetComponent<SpriteRenderer>().sprite;
            talk.currentName = "???";
        }

        static void DoConversationChoice(ActionTalk talk, DialogChoice choice)
        {

        }

        void CheckStartConditions()
        {
            var wu = Managers.GetManager<WorldUnitsHandler>();
            var wut = wu.GetUnit(DataConfig.WorldUnitType.Terraformation);
            if (wut.GetValue() >= 0/* 1100000 */)
            {
                questPhase = QuestPhase.Arrival;
                SaveState();
                SetVisibilityViaCurrentPhase();
            }
        }

        void CheckArrival()
        {
            if (asteroid == null)
            {
                var mh = Managers.GetManager<MeteoHandler>();
                /*
                foreach (var me in mh.meteoEvents)
                {
                    logger.LogInfo("Dump meteo events: " + me.environmentVolume.name);
                }*/
                var meteoEvent = mh.meteoEvents[9];
                logger.LogInfo("Launching arrival meteor: " + meteoEvent.environmentVolume.name);

                mh.meteoSound.StartMeteoAudio(meteoEvent);
                if (meteoEvent.asteroidEventData != null)
                {
                    var selectedAsteroidEventData = UnityEngine.Object.Instantiate(meteoEvent.asteroidEventData);

                    var ah = Managers.GetManager<AsteroidsHandler>();

                    GameObject obj = Instantiate(
                        selectedAsteroidEventData.asteroidGameObject,
                        technicianDropLocation + new Vector3(0, 1000, 0),
                        Quaternion.identity,
                        ah.gameObject.transform
                    );
                    obj.transform.LookAt(technicianDropLocation);
                    
                    asteroid = obj.GetComponent<Asteroid>();
                    asteroid.SetLinkedAsteroidEvent(selectedAsteroidEventData);
                    asteroid.debrisDestroyTime = 5;
                    asteroid.placeAsteroidBody = false;
                    selectedAsteroidEventData.ChangeExistingAsteroidsCount(1);
                    selectedAsteroidEventData.ChangeTotalAsteroidsCount(1);
                }
            } 
            else
            {
                if ((bool)asteroidHasCrashed.GetValue(asteroid))
                {
                    asteroid = null;
                    questPhase = QuestPhase.Initial_Help;
                    SaveState();
                    SetVisibilityViaCurrentPhase();
                }
            }
        }

        void CheckInitialHelp()
        {

        }

        void CheckBaseSetup()
        {

        }

        void CheckOperating()
        {

        }
            
        internal static PlayerMainController GetPlayerMainController()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                return p.GetActivePlayerController();
            }
            return null;
        }

        static void HideInventory(WorldObject wo)
        {
            var allInvs = InventoriesHandler.GetAllInventories();
            for (int i = 0; i < allInvs.Count; i++)
            {
                var inv = allInvs[i];
                if (inv.GetId() == wo.GetLinkedInventoryId())
                {
                    allInvs.RemoveAt(i);
                    break;
                }
            } 
        }

        static void SetVisibilityViaCurrentPhase()
        {
            switch (questPhase)
            {
                case QuestPhase.Not_Started:
                case QuestPhase.Arrival:
                    {
                        avatar.SetVisible(false);
                        escapePod.GetGameObject().SetActive(false);

                        foreach (var wo in baseComponents)
                        {
                            wo.GetGameObject().SetActive(false);
                        }
                        break;
                    }
                case QuestPhase.Initial_Help:
                    {
                        avatar.SetVisible(true);
                        escapePod.GetGameObject().SetActive(true);

                        foreach (var wo in baseComponents)
                        {
                            wo.GetGameObject().SetActive(false);
                        }
                        break;
                    }
                case QuestPhase.Base_Setup:
                    {
                        avatar.SetVisible(true);
                        escapePod.GetGameObject().SetActive(true);

                        foreach (var wo in baseComponents)
                        {
                            wo.GetGameObject().SetActive(false);
                        }
                        break;
                    }
                case QuestPhase.Operating:
                    {
                        avatar.SetVisible(true);
                        escapePod.GetGameObject().SetActive(true);

                        foreach (var wo in baseComponents)
                        {
                            wo.GetGameObject().SetActive(true);
                        }
                        break;
                    }
            }
        }

        static void LoadState()
        {
            conversationHistory.Clear();
            foreach (var dc in dialogChoices.Values)
            {
                dc.visible = false;
                dc.enabled = false;
                dc.picked = false;
            }

            string state = escapePod.GetText();
            if (string.IsNullOrEmpty(state))
            {
                questPhase = QuestPhase.Not_Started;
            }
            else
            {
                string[] parts = state.Split(';');

                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2)
                    {
                        logger.LogWarning("Invalid state key=value pair: " + part);
                    }
                    else
                    {
                        if (kv[0] == "QuestPhase")
                        {
                            questPhase = (QuestPhase)int.Parse(kv[1]);
                        }
                        else if (kv[0] == "HistoryEntry")
                        {
                            string[] pars = kv[1].Split(',');

                            if (pars.Length != 2)
                            {
                                logger.LogWarning("Invalid HistoryEntry format: " + part);
                            }
                            else
                            {
                                conversationHistory.Add(new ConversationEntry
                                {
                                    owner = pars[0],
                                    labelId = pars[1]
                                });
                            }
                        }
                        else if (kv[0] == "DialogChoice")
                        {
                            string[] pars = kv[1].Split(',');

                            if (pars.Length != 4)
                            {
                                logger.LogWarning("Invalid DialogChoice format: " + part);
                            }
                            else
                            {
                                if (dialogChoices.TryGetValue(pars[0], out var dc))
                                {
                                    dc.visible = "1" == pars[1];
                                    dc.enabled = "1" == pars[2];
                                    dc.enabled = "1" == pars[3];
                                }
                                else
                                {
                                    logger.LogWarning("Unknown DialogChoice: " + part);
                                }
                            }
                        }
                        else
                        {
                            logger.LogWarning("Unknown state key=value pair:" + part);
                        }
                    }
                }
            }
        }

        static string GetColorForOwner(string owner)
        {
            if (owner == "player")
            {
                return "#FFFFFF";
            }
            return "#FFFF00";
        }

        static void SaveState()
        {
            StringBuilder sb = new();
            sb.Append("QuestPhase=").Append((int)questPhase);

            if (conversationHistory.Count != 0)
            {
                foreach (var ch in conversationHistory)
                {
                    sb.Append(";HistoryEntry=").Append(ch.owner).Append(',').Append(ch.labelId);
                }
            }
            if (dialogChoices.Count != 0)
            {
                foreach (var dc in dialogChoices.Values) 
                {
                    sb.Append(";DialogChoice=").Append(dc.id)
                        .Append(',').Append(dc.visible ? 1 : 0)
                        .Append(',').Append(dc.enabled ? 1 : 0)
                        .Append(',').Append(dc.picked ? 1 : 0);
                }
            }

            escapePod.SetText(sb.ToString());
        }

        static void PrepareDialogChoices()
        {

        }

        static void AddChoice(string labelId)
        {
            dialogChoices[labelId] = new DialogChoice
            {
                    labelId = labelId
            };
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            ingame = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
                Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            foreach (var kv in labels)
            {
                if (___localizationDictionary.TryGetValue(kv.Key, out var dict)) {
                    foreach (var kv2 in kv.Value)
                    {
                        dict[kv2.Key] = kv2.Value;
                    }
                }
            }
        }


        class ConversationEntry
        {
            internal string owner;
            internal string labelId;
        }

        class DialogChoice
        {
            internal string id;
            internal string labelId;
            internal object[] parameters;
            internal bool visible;
            internal bool picked;
            internal bool enabled;
        }

        class ActionTalk : Actionnable
        {
            GameObject conversationDialogCanvas;

            readonly List<GameObject> conversationHistoryLines = new();

            readonly List<GameObject> dialogChoiceLines = new();

            GameObject conversationPicture;

            GameObject conversationName;

            internal Sprite currentImage;

            internal string currentName;

            internal readonly List<DialogChoice> currentChoices = new();

            internal readonly List<ConversationEntry> currentHistory = new();

            internal Action<ActionTalk> OnConversationStart;

            internal Action<ActionTalk, DialogChoice> OnConversationChoice;

            internal int historyScrollOffset;

            void Update()
            {
                var wh = Managers.GetManager<WindowsHandler>();
                if (conversationDialogCanvas != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
                {
                    DestroyTalkDialog();

                    wh.CloseAllWindows();
                    return;
                }

                if (!wh.GetHasUiOpen() && conversationDialogCanvas != null) 
                {
                    DestroyTalkDialog();
                    return;
                }

                if (conversationDialogCanvas != null)
                {
                    var ms = Mouse.current.scroll.ReadValue();
                    if (ms.y != 0)
                    {
                        int delta = 1;
                        if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
                        {
                            delta = 3;
                        }
                        if (ms.y > 0)
                        {
                            historyScrollOffset = Math.Min(currentHistory.Count - 1, historyScrollOffset + delta);
                        }
                        else
                        {
                            historyScrollOffset = Math.Max(0, historyScrollOffset - delta);
                        }
                        UpdateCurrents();
                    }
                }
            }

            void DestroyTalkDialog()
            {
                foreach (var go in conversationHistoryLines)
                {
                    Destroy(go);
                }
                conversationHistoryLines.Clear();

                foreach (var go in dialogChoiceLines)
                {
                    Destroy(go);
                }
                dialogChoiceLines.Clear();

                Destroy(conversationDialogCanvas);
            }

            public override void OnAction()
            {
                logger.LogInfo("Creating Canvas");
                conversationDialogCanvas = new GameObject("TechniciansExileConversationDialogCanvas");
                var c = conversationDialogCanvas.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.transform.SetAsLastSibling();

                var pw = Screen.width * 0.6f;
                var ph = Screen.height * 0.65f;

                logger.LogInfo("Creating background");
                var background = new GameObject("TechniciansExileConversationDialogCanvas-Background");
                background.transform.SetParent(conversationDialogCanvas.transform);

                var img = background.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.95f);

                var rect = background.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(0, 0);
                rect.sizeDelta = new Vector2(pw, ph);

                logger.LogInfo("Creating Picture");
                conversationPicture = new GameObject("TechniciansExileConversationDialogCanvas-Picture");
                conversationPicture.transform.SetParent(conversationDialogCanvas.transform);

                img = conversationPicture.AddComponent<Image>();
                rect = img.GetComponent<RectTransform>();
                var imagePos = new Vector3(-pw / 2 + pw / 12 + pw / 24, ph / 2 - pw / 12 - pw / 24, 0);
                rect.localPosition = imagePos;
                rect.sizeDelta = new Vector2(pw / 6, pw / 6);

                int maxChoices = 6;
                int maxHistory = 15;

                var fontSize = (int)Mathf.Floor(ph * 0.9f / (maxChoices + maxHistory));

                logger.LogInfo("Creating Name");
                conversationName = new GameObject("TechniciansExileConversationDialogCanvas-Name");
                conversationName.transform.SetParent(conversationDialogCanvas.transform);

                var text = conversationName.AddComponent<TextMeshProUGUI>();
                text.fontSize = fontSize;
                text.horizontalAlignment = HorizontalAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.richText = true;
                rect = text.GetComponent<RectTransform>();
                rect.localPosition = imagePos - new Vector3(0, ph / 6 + fontSize / 2, 0);
                rect.sizeDelta = new Vector2(pw / 4, fontSize * 3 / 2);

                var historyWidth = pw * 3 / 4;
                var choiceWidth = pw * 11 / 12;
                var historyX = (pw - historyWidth) / 2;
                var historyH = maxHistory * fontSize;
                var historyY = ph / 2 - historyH;
                var choicesY = historyY - 2 * fontSize;

                logger.LogInfo("Creating History Entries");
                for (int i = 0; i < maxHistory; i++)
                {
                    var he = new GameObject("TechniciansExileConversationDialogCanvas-History-" + i);
                    he.transform.SetParent(conversationDialogCanvas.transform);

                    var tmp = he.AddComponent<TextMeshProUGUI>();
                    tmp.text = "example..." + i;
                    tmp.fontSize = fontSize;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    tmp.enableWordWrapping = false;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.richText = true;

                    rect = he.GetComponent<RectTransform>();
                    rect.localPosition = new Vector3(historyX, historyY, 0);
                    rect.sizeDelta = new Vector2(historyWidth, fontSize);

                    conversationHistoryLines.Add(he);

                    historyY += fontSize;

                    logger.LogInfo("  Entry #" + i);
                }

                logger.LogInfo("Creating Separator");

                var sep = new GameObject("TechniciansExileConversationDialogCanvas-Separator");
                sep.transform.SetParent(conversationDialogCanvas.transform);

                img = sep.AddComponent<Image>();
                img.color = Color.white;

                var choiceX = 0;

                rect = img.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(choiceX, choicesY + fontSize);
                rect.sizeDelta = new Vector2(pw, 2);

                logger.LogInfo("Creating Choices");
                for (int i = 0; i < maxChoices; i++)
                {
                    var he = new GameObject("TechniciansExileConversationDialogCanvas-Choices-" + i);
                    he.transform.SetParent(conversationDialogCanvas.transform);

                    var tmp = he.AddComponent<TextMeshProUGUI>();
                    tmp.text = "choice..." + i;
                    tmp.fontSize = fontSize;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    tmp.enableWordWrapping = false;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.richText = true;

                    rect = he.GetComponent<RectTransform>();
                    rect.localPosition = new Vector3(choiceX, choicesY, 0);
                    rect.sizeDelta = new Vector2(choiceWidth, fontSize);

                    choicesY -= fontSize;

                    dialogChoiceLines.Add(he);

                    logger.LogInfo("  Choice #" + i);
                }

                logger.LogInfo("Invoke OnConversationStart");

                OnConversationStart?.Invoke(this);

                logger.LogInfo("Invoke UpdateCurrents");

                UpdateCurrents();

                logger.LogInfo("Pin dialog to WindowsHandler");
                Cursor.visible = true;
                var wh = Managers.GetManager<WindowsHandler>();
                AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.TextInput;

                hudHandler.CleanCursorTextIfCode("TechniciansExile_Talk");
                logger.LogInfo("Done OnAction");
            }

            void UpdateCurrents()
            {
                conversationPicture.GetComponent<Image>().sprite = currentImage;
                conversationName.GetComponent<TextMeshProUGUI>().text = currentName ?? "???";

                foreach (var he in conversationHistoryLines)
                {
                    he.GetComponent<TextMeshProUGUI>().text = "";
                }

                foreach (var he in dialogChoiceLines)
                {
                    he.GetComponent<TextMeshProUGUI>().text = "";
                }


            }

            public override void OnHover()
            {
                hudHandler.DisplayCursorText("TechniciansExile_Talk", 0f);
                base.OnHover();
            }

            public override void OnHoverOut()
            {
                hudHandler.CleanCursorTextIfCode("TechniciansExile_Talk");

                base.OnHoverOut();
            }
        }
    }
}
