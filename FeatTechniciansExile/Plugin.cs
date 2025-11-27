// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using System.IO;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Reflection;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Linq;

namespace FeatTechniciansExile
{
    [BepInPlugin(modFeatTechniciansExileGuid, "(Feat) Technicians Exile", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatTechniciansExileGuid = "akarnokd.theplanetcraftermods.feattechniciansexile";

        static ManualLogSource logger;

        internal static Texture2D technicianFront;
        internal static Texture2D technicianBack;

        // /tp 1990 70 1073
        static readonly Dictionary<string, Vector3> technicianDropLocationPerPlanet = new() {
            { "",  new Vector3(1995, 68.8f, 1073) },
            { "Prime",  new Vector3(1995, 68.8f, 1073) },
            { "Humble", new Vector3(1327, 347.6f, -333) },
            { "Selenea", new Vector3(-580, 212, 525) },
            { "Aqualis", new Vector3(0, 0, 0) }
        };

        static TechnicianAvatar avatar;

        // Used to be 70000 but since 0.9.020, everything below 200M is a scene object and cant be instantiated
        static readonly int technicianWorldObjectIdStart = 700 * 1000 * 1000;

        static WorldObject escapePod;

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

        static readonly List<ConversationEntry> conversationHistory = [];

        static readonly Dictionary<string, DialogChoice> dialogChoices = [];

        static readonly Dictionary<string, Dictionary<string, string>> labels = [];

        static readonly Dictionary<string, MessageData> technicianMessageDict = [];
        static MessageData technicianMessage2;

        static TMP_FontAsset fontAsset;

        static AccessTools.FieldRef<MeteoHandler, List<MeteoEventData>> fMeteoHandlerMeteoEvents;
        static AccessTools.FieldRef<Asteroid, bool> fAsteroidHasCrashed;
        static AccessTools.FieldRef<AsteroidsHandler, Unity.Mathematics.Random> fAsteroidsHandlerRandom;
        static AccessTools.FieldRef<WindowsHandler, DataConfig.UiType> fWindowsHandlerOpenedUi;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            technicianFront = LoadPNG(Path.Combine(dir, "Technician_Front.png"));
            technicianBack = LoadPNG(Path.Combine(dir, "Technician_Back.png"));

            AddTranslation(dir, "english");
            AddTranslation(dir, "hungarian");
            AddTranslation(dir, "russian");
            AddTranslation(dir, "french");
            AddTranslation(dir, "spanish");

            PrepareDialogChoices();

            var msg1 = ScriptableObject.CreateInstance<MessageData>();
            {
                msg1.stringId = "TechniciansExile_Message_Prime";
                msg1.senderStringId = "TechniciansExile_Name";
                msg1.yearSent = "Today";
                msg1.messageType = DataConfig.MessageType.FromWorld;
            };
            var msg2 = ScriptableObject.CreateInstance<MessageData>();
            {
                msg2.stringId = "TechniciansExile_Message_Humble";
                msg2.senderStringId = "TechniciansExile_Name";
                msg2.yearSent = "Today";
                msg2.messageType = DataConfig.MessageType.FromWorld;
            };
            var msg3 = ScriptableObject.CreateInstance<MessageData>();
            {
                msg3.stringId = "TechniciansExile_Message_Selenea";
                msg3.senderStringId = "TechniciansExile_Name";
                msg3.yearSent = "Today";
                msg3.messageType = DataConfig.MessageType.FromWorld;
            };
            var msg4 = ScriptableObject.CreateInstance<MessageData>();
            {
                msg4.stringId = "TechniciansExile_Message_Aqualis";
                msg4.senderStringId = "TechniciansExile_Name";
                msg4.yearSent = "Today";
                msg4.messageType = DataConfig.MessageType.FromWorld;
            }

            technicianMessageDict[""] = msg1;
            technicianMessageDict["Prime"] = msg1;
            technicianMessageDict["Humble"] = msg2;
            technicianMessageDict["Selenea"] = msg3;
            technicianMessageDict["Aqualis"] = msg4;

            technicianMessage2 = ScriptableObject.CreateInstance<MessageData>();
            {
                technicianMessage2.stringId = "TechniciansExile_Message2";
                technicianMessage2.senderStringId = "TechniciansExile_Name";
                technicianMessage2.yearSent = "Today";
                technicianMessage2.messageType = DataConfig.MessageType.FromWorld;
            };

            fMeteoHandlerMeteoEvents = AccessTools.FieldRefAccess<MeteoHandler, List<MeteoEventData>>("_meteoEvents");
            fAsteroidHasCrashed = AccessTools.FieldRefAccess<Asteroid, bool>("hasCrashed");
            fAsteroidsHandlerRandom = AccessTools.FieldRefAccess<AsteroidsHandler, Unity.Mathematics.Random>("_random");
            fWindowsHandlerOpenedUi = AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>("_openedUi");

            try
            {
                Font osFont = null;

                foreach (var fp in Font.GetPathsToOSFonts())
                {
                    if (fp.ToLower().Contains("arial.ttf"))
                    {
                        osFont = new Font(fp);
                        break;
                    }
                }

                fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
            } 
            catch (Exception)
            {
                logger.LogWarning("Failed to create custom font, using the game's default font.");
            }

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(h, modFeatTechniciansExileGuid, PlanetLoader_HandleDataAfterLoad);
        }

        static void AddTranslation(string dir, string language)
        {
            var lines = File.ReadAllLines(Path.Combine(dir, "technician_labels_" + language + ".txt"));

            if (!labels.TryGetValue(language, out var dict))
            {
                dict = [];
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
            var tex = new Texture2D(100, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static bool IsHost()
        {
            return NetworkManager.Singleton?.IsServer ?? true;
        }

        static void PlanetLoader_HandleDataAfterLoad(PlanetLoader pl)
        {
            if (!IsHost())
            {
                return;
            }

            logger.LogInfo("Start");

            var pid = pl.GetCurrentPlanetData().id;

            technicianDropLocationPerPlanet.TryGetValue(pid, out var technicianDropLocation);

            technicianLocation1 = technicianDropLocation + new Vector3(0, -0.5f, 0);
            technicianRotation1 = Quaternion.identity * Quaternion.Euler(0, -90, 0);

            logger.LogInfo("  Creating the Technician");
            avatar = TechnicianAvatar.CreateAvatar(Color.white);
            avatar.SetPosition(technicianLocation1, technicianRotation1);

            var at = avatar.avatar.AddComponent<ActionTalk>();

            at.OnConversationStart = DoConversationStart;
            at.OnConversationChoice = DoConversationChoice;

            logger.LogInfo("  Finding the Escape Pod");
            escapePod = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart);
            if (escapePod == null)
            {
                logger.LogInfo("    Creating the Escape Pod");
                var podGroup = GroupsHandler.GetGroupViaId("EscapePod");
                if (podGroup == null)
                {
                    logger.LogError("Unable to find Group for EscapePod");
                }
                if (podGroup?.GetAssociatedGameObject() == null)
                {
                    logger.LogError("Unable to find template GameObject for EscapePod");
                }
                escapePod = WorldObjectsHandler.Instance.CreateNewWorldObject(podGroup,
                    technicianWorldObjectIdStart);

                escapePod.SetPositionAndRotation(technicianDropLocation, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                escapePod.SetDontSaveMe(false);
                // make sure this pod's chest is not default-linked to the player's landing pod
                // since the pod's chest is a scene object
                InventoriesHandler.Instance.CreateNewInventory(12, 0, 0, null, null, inv =>
                {
                    logger.LogInfo("      Inventory overridden: " + inv.GetId());
                    escapePod.SetLinkedInventoryId(inv.GetId());

                    var go = WorldObjectsHandler.Instance.InstantiateWorldObject(escapePod, false);

                    if (go == null)
                    {
                        logger.LogError("EscapePod instantiation failure");
                    }

                    SetVisibilityViaCurrentPhase();
                });
            } 
            else
            {
                // make sure its position in case of surface changes
                escapePod.SetPositionAndRotation(technicianDropLocation, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                var escapeGo = escapePod.GetGameObject();
                if (escapeGo != null)
                {
                    escapeGo.transform.position = escapePod.GetPosition();
                    escapeGo.transform.rotation = escapePod.GetRotation();
                }
            }

            ingame = true;

            MigrateFromOld();

            LoadState();
            SetVisibilityViaCurrentPhase();


            logger.LogInfo("Done");
        }

        static void CreatePod()
        {
            bool dontSaveMe = false;

            var pl = Managers.GetManager<PlanetLoader>();
            if (pl == null)
            {
                return;
            }
            var cp = pl.GetCurrentPlanetData();
            if (cp == null)
            {
                return;
            }

            var pid = cp.id;

            technicianDropLocationPerPlanet.TryGetValue(pid, out var technicianDropLocation);

            var livingPodBase = technicianDropLocation + new Vector3(0, 0, 15);

            logger.LogInfo("  Finding the Living Pod");
            var livingPod = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 1);
            if (livingPod == null)
            {
                logger.LogInfo("    Creating the Living Pod");
                livingPod = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("pod"),
                    technicianWorldObjectIdStart + 1);
                livingPod.SetPositionAndRotation(livingPodBase, Quaternion.identity * Quaternion.Euler(0, 90, 0));
                livingPod.SetPanelsId([1, 4, 1, 1, 5, 7]);
                livingPod.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(livingPod, false);
            }

            var livingPodFloor = livingPodBase + new Vector3(0, 1, 0);

            var bedPosition = livingPodFloor + new Vector3(3, 0, -1);

            logger.LogInfo("  Finding the Bed");
            var bed = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 2);
            if (bed == null)
            {
                logger.LogInfo("    Creating the Bed");
                bed = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("BedSimple"),
                    technicianWorldObjectIdStart + 2);
                bed.SetPositionAndRotation(bedPosition, Quaternion.identity * Quaternion.Euler(0, 180, 0));
                bed.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(bed, false);
            }

            var deskPosition = livingPodFloor + new Vector3(-1, 0, -3);
            logger.LogInfo("  Finding the Desk");
            var desk = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 3);
            if (desk == null)
            {
                logger.LogInfo("    Creating the Desk");
                desk = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Desktop1"),
                    technicianWorldObjectIdStart + 3);
                desk.SetPositionAndRotation(deskPosition, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                desk.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(desk, false);
            }

            logger.LogInfo("  Finding the Rockets Screen");
            var screen = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 4);
            if (screen == null)
            {
                logger.LogInfo("    Creating the Rockets Screen");
                screen = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("ScreenRockets"),
                    technicianWorldObjectIdStart + 4);
                screen.SetPositionAndRotation(deskPosition + new Vector3(-0.5f, 1f, 0.25f), Quaternion.identity * Quaternion.Euler(0, 180, 0));
                screen.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(screen, false);
            }

            logger.LogInfo("  Finding the Grower");
            var grower = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 5);
            if (grower == null)
            {
                logger.LogInfo("    Creating the Grower");
                grower = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("VegetableGrower1"),
                    technicianWorldObjectIdStart + 5);
                grower.SetPositionAndRotation(livingPodFloor + new Vector3(2, 0, 2), Quaternion.identity * Quaternion.Euler(0, -45, 0));
                grower.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(grower, false);
            }

            logger.LogInfo("  Finding the Collector");
            var collector = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 6);
            if (collector == null)
            {
                logger.LogInfo("    Creating the Collector");
                collector = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("WaterCollector1"),
                    technicianWorldObjectIdStart + 6);
                collector.SetPositionAndRotation(livingPodBase + new Vector3(0, -1, 15), Quaternion.identity);
                collector.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(collector, false);
            }

            logger.LogInfo("  Finding the Flower Pot");
            var flower = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 7);
            if (flower == null)
            {
                logger.LogInfo("    Creating the Flower Pot");
                flower = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("FlowerPot1"),
                    technicianWorldObjectIdStart + 7);
                flower.SetPositionAndRotation(livingPodFloor + new Vector3(-2, 0, 2.25f), Quaternion.identity);
                flower.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(flower, false);

                Inventory inv = InventoriesHandler.Instance.GetInventoryById(flower.GetLinkedInventoryId());
                InventoriesHandler.Instance.AddItemToInventory(GroupsHandler.GetGroupViaId("SeedGold"), inv, (success, id) =>
                {
                    if (!success && id != 0)
                    {
                        WorldObjectsHandler.Instance.DestroyWorldObject(id);
                    }
                });
            }

            logger.LogInfo("  Finding the Chair");
            var chair = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 8);
            if (chair == null)
            {
                logger.LogInfo("    Creating the Chair");
                chair = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Chair1"),
                    technicianWorldObjectIdStart + 8);
                chair.SetPositionAndRotation(deskPosition + new Vector3(1f, 0, 2), Quaternion.identity * Quaternion.Euler(0, -45, 0));
                chair.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(chair, false);
            }

            logger.LogInfo("  Finding the Chest");
            var chest = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 9);
            if (chest == null)
            {
                logger.LogInfo("    Creating the Chest");
                chest = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"),
                    technicianWorldObjectIdStart + 9);
                chest.SetPositionAndRotation(livingPodFloor + new Vector3(-0.25f, 0, 2.25f), Quaternion.identity * Quaternion.Euler(0, 90, 0));
                chest.SetDontSaveMe(dontSaveMe);
                chest.SetText("Technician #221021");
                WorldObjectsHandler.Instance.InstantiateWorldObject(chest, false);
            }

            logger.LogInfo("  Finding the Solar");
            var solar = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 10);
            if (solar == null)
            {
                logger.LogInfo("    Creating the Solar");
                solar = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("EnergyGenerator2"),
                    technicianWorldObjectIdStart + 10);
                solar.SetPositionAndRotation(livingPodFloor + new Vector3(0, 5, 0), Quaternion.identity);
                solar.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.Instance.InstantiateWorldObject(solar, false);
            }

            logger.LogInfo("  Finding the Seed");
            var seed = WorldObjectsHandler.Instance.GetWorldObjectViaId(technicianWorldObjectIdStart + 11);
            if (seed == null)
            {
                logger.LogInfo("    Creating the Seed");
                seed = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Vegetable0Seed"),
                    technicianWorldObjectIdStart + 11);
                seed.SetDontSaveMe(dontSaveMe);
            }

            Inventory growerInv = InventoriesHandler.Instance.GetInventoryById(grower.GetLinkedInventoryId());
            if (growerInv.GetInsideWorldObjects().Count == 0)
            {
                // we need to "place" the seed because AddWorldObjectToInventory only adds an existing world object
                // if it had position in the world. Weird I know.
                seed.SetPositionAndRotation(new Vector3(0.1f, 0.1f, 0.1f), Quaternion.identity);
                InventoriesHandler.Instance.AddWorldObjectToInventory(seed, growerInv, grabbed: false, success =>
                {
                    logger.LogInfo("    Seed -> Grower " + (success ? "success" : "failure"));
                });
            }

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

            technicianLocation2 = deskPosition + new Vector3(-0.25f, 0, 2);
            technicianRotation2 = Quaternion.identity * Quaternion.Euler(0, 180, 0);

            technicianLocation3 = bedPosition + new Vector3(-0.85f, -0.45f, 0);
            technicianRotation3 = Quaternion.identity * Quaternion.Euler(-90, 0, 0);
        }

        static void MigrateFromOld()
        {
            for (int i = 70000; i < 70011; i++)
            {
                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(i);
                wo?.SetDontSaveMe(true);
                if (wo != null && wo.GetId() == 70000)
                {
                    escapePod.SetText(wo.GetText());
                }
            }
        }

        void Update()
        {
            if (!ingame)
            {
                return;
            }

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

        static void PrepareDialogChoices()
        {
            AddChoice("WhoAreYou", "TechniciansExile_Dialog_1_Who_Are_You");
            AddChoice("WhatsYourName", "TechniciansExile_Dialog_1_Whats_Your_Name");
            AddChoice("NotConvict", "TechniciansExile_Dialog_1_You_Not_Convict");
            AddChoice("WhatDone", "TechniciansExile_Dialog_1_What_Done");
            AddChoice("Needs", "TechniciansExile_Dialog_1_What_Do_You_Need");
            AddChoice("Supplies", "TechniciansExile_Dialog_1_Supplies_Avail");
            AddChoice("Satellite", "TechniciansExile_Dialog_2_Task");
            AddChoice("AWhat", "TechniciansExile_Dialog_2_Terminal");
            AddChoice("Supplies2", "TechniciansExile_Dialog_2_Supplies_Avail");
            AddChoice("NicePod", "TechniciansExile_Dialog_3_Nice_Pod");
            AddChoice("Status", "TechniciansExile_Dialog_3_Status");
            AddChoice("Great", "TechniciansExile_Dialog_2_Great");
        }

        static void DoConversationStart(ActionTalk talk)
        {
            foreach (var k in dialogChoices.Keys)
            {
                logger.LogInfo(k);
            }

            talk.currentImage = avatar.avatarFront.GetComponent<SpriteRenderer>().sprite;
            if (dialogChoices["WhoAreYou"].picked)
            {
                talk.currentName = Localization.GetLocalizedString("TechniciansExile_Name");
            }
            else
            {
                talk.currentName = "???";
            }

            talk.currentHistory.Clear();
            talk.currentHistory.AddRange(conversationHistory);

            UpdateSuppliesParams();
            UpdateSupplies2Params();
            SyncChoices(talk);
        }

        static void DoConversationChoice(ActionTalk talk, DialogChoice choice)
        {
            if (choice.id == "WhoAreYou")
            {
                talk.currentName = Localization.GetLocalizedString("TechniciansExile_Name");
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                if (choice.picked)
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Identify_Again", talk);
                }
                else
                {
                    choice.picked = true;
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Identify", talk);
                }
                ShowChoice(dialogChoices["WhatsYourName"]);
                ShowChoice(dialogChoices["NotConvict"]);
                ShowChoice(dialogChoices["Needs"]);
                SyncChoices(talk);
            }
            else if (choice.id == "WhatsYourName")
            {
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                if (choice.picked)
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Name_Again", talk);
                }
                else
                {
                    choice.picked = true;
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Name", talk);
                }
            }
            else if (choice.id == "NotConvict")
            {
                choice.picked = true;
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Explain", talk);

                ShowChoice(dialogChoices["WhatDone"]);
                SyncChoices(talk);
            }
            else if (choice.id == "WhatDone")
            {
                choice.picked = true;
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Reason", talk);
            }
            else if (choice.id == "Needs")
            {
                choice.visible = false;
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_1_Supplies", talk);

                dialogChoices["WhoAreYou"].visible = false;
                ShowChoice(dialogChoices["Supplies"]);
                UpdateSuppliesParams();
                SyncChoices(talk);
            }
            else if (choice.id == "Supplies")
            {
                choice.visible = false;
                AddResponseLabel("TechniciansExile_Convict", "TechniciansExile_Dialog_1_Supplies_Given", talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_2_Job", talk);

                var inv = GetPlayerMainController().GetPlayerBackpack().GetInventory();
                var invContent = new List<WorldObject>(inv.GetInsideWorldObjects());

                int foodCounter = 0;
                int waterCounter = 0;
                for (int i = invContent.Count - 1; i >= 0; i--)
                {
                    var wo = invContent[i];
                    if (wo.GetGroup().GetId() == "astrofood" && foodCounter < 5)
                    {
                        foodCounter++;
                        InventoriesHandler.Instance.RemoveItemFromInventory(wo, inv, true);
                        NotifyRemoved(wo.GetGroup());
                    }
                    else if (wo.GetGroup().GetId() == "WaterBottle1" && waterCounter < 5)
                    {
                        waterCounter++;
                        NotifyRemoved(wo.GetGroup());
                        InventoriesHandler.Instance.RemoveItemFromInventory(wo, inv, true);
                    }
                }

                ShowChoice(dialogChoices["Satellite"]);
                SyncChoices(talk);
            }
            else if (choice.id == "Satellite")
            {
                choice.visible = false;

                AddResponseLabel("TechniciansExile_Convict", "TechniciansExile_Dialog_2_Task_Long", talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_2_Accept", talk);
                
                ShowChoice(dialogChoices["AWhat"]);
                SyncChoices(talk);
            }
            else if (choice.id == "AWhat")
            {
                choice.visible = false;
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_2_Supplies", talk);

                ShowChoice(dialogChoices["Supplies2"]);
                UpdateSupplies2Params();
                SyncChoices(talk);
            }
            else if (choice.id == "Supplies2")
            {
                choice.visible = false;
                AddResponseLabel("TechniciansExile_Convict", "TechniciansExile_Dialog_2_Supplies_Given", talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_2_How_Long", talk);

                var inv = GetPlayerMainController().GetPlayerBackpack().GetInventory();
                var invContent = new List<WorldObject>(inv.GetInsideWorldObjects());

                int alloyCounter = 0;
                for (int i = invContent.Count - 1; i >= 0; i--)
                {
                    var wo = invContent[i];
                    if (wo.GetGroup().GetId() == "Alloy" && alloyCounter < 10)
                    {
                        alloyCounter++;
                        InventoriesHandler.Instance.RemoveItemFromInventory(wo, inv, true);
                        NotifyRemoved(wo.GetGroup());
                    }
                }

                ShowChoice(dialogChoices["Great"]);
                SyncChoices(talk);
            }
            else if (choice.id == "Great")
            {
                if (questPhase == QuestPhase.Initial_Help)
                {
                    questPhase = QuestPhase.Base_Setup;
                }
                talk.DestroyTalkDialog();
                var wh = Managers.GetManager<WindowsHandler>();
                wh.CloseAllWindows();
            }
            else if (choice.id == "NicePod")
            {
                choice.visible = false;
                AddResponseLabel("TechniciansExile_Convict", "TechniciansExile_Dialog_3_Nice_Pod_Long", talk);
                AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_3_Thanks", talk);

                ShowChoice(dialogChoices["Status"]);
                SyncChoices(talk);
            }
            else if (choice.id == "Status")
            {
                AddResponseLabel("TechniciansExile_Convict", choice.labelId, talk);

                bool haveRockets = CheckRockets();
                bool haveEnergy = CheckPower();

                if (haveRockets && haveEnergy)
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_3_OK", talk);
                }
                else if (haveRockets && !haveEnergy)
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_3_Has_Satellie_No_Power", talk);
                }
                else if (!haveRockets && haveEnergy)
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_3_No_Satellite", talk);
                }
                else
                {
                    AddResponseLabel("TechniciansExile_Technician", "TechniciansExile_Dialog_3_No_Satellite_No_Power", talk);
                }

                SyncChoices(talk);
            }

            SaveState();
            talk.UpdateCurrents();
        }

        static bool CheckRockets()
        {
            foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects().Values)
            {
                if (wo.GetGroup().GetId().StartsWith("SpaceMultiplier"))
                {
                    var inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());
                    if (inv.GetInsideWorldObjects().Count != 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static bool CheckPower()
        {
            var wu = Managers.GetManager<WorldUnitsHandler>();
            var wut = wu.GetUnit(DataConfig.WorldUnitType.Energy);

            var excess = wut.GetIncreaseValuePersSec() + wut.GetDecreaseValuePersSec();
            logger.LogInfo("CheckPower " + excess);
            return excess >= 0f;
        }

        static void NotifyRemoved(Group group)
        {
            string text = Readable.GetGroupName(group);
            InformationsDisplayer informationsDisplayer = Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer();
            informationsDisplayer.AddInformation(2f, text, DataConfig.UiInformationsType.OutInventory, group.GetImage());
        }

        static void SyncChoices(ActionTalk talk)
        {
            talk.currentChoices.Clear();
            foreach (var dc in dialogChoices.Values)
            {
                if (dc.visible)
                {
                    talk.currentChoices.Add(dc);
                }
            }
        }

        static DialogChoice WithEnabled(DialogChoice dc)
        {
            dc.enabled = true;
            return dc;
        }
        static DialogChoice WithVisible(DialogChoice dc)
        {
            dc.visible = true;
            return dc;
        }

        static void UpdateSuppliesParams()
        {
            var ch = dialogChoices["Supplies"];
            ch.parameters = new object[2];

            var backpack = GetPlayerMainController().GetPlayerBackpack().GetInventory();

            int food = 0;
            int water = 0;

            foreach (var wo in backpack.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == "astrofood")
                {
                    food++;
                }
                else if (wo.GetGroup().GetId() == "WaterBottle1")
                {
                    water++;
                }
            }
            ch.parameters =
            [
                Math.Min(5, food),
                Math.Min(5, water)
            ];
            //logger.LogInfo("Food: " + food + ", Water: " + water);
            ch.enabled = food >= 5 && water >= 5;
        }
        static void UpdateSupplies2Params()
        {
            var ch = dialogChoices["Supplies2"];
            ch.parameters = new object[2];

            var backpack = GetPlayerMainController().GetPlayerBackpack().GetInventory();

            int alloy = 0;

            foreach (var wo in backpack.GetInsideWorldObjects())
            {
                if (wo.GetGroup().GetId() == "Alloy")
                {
                    alloy++;
                }
            }
            ch.parameters =
            [
                Math.Min(10, alloy),
            ];
            //logger.LogInfo("Alloy: " + alloy);
            ch.enabled = alloy >= 10;
        }

        static internal void ShowChoice(DialogChoice dc)
        {
            dc.visible = true;
            dc.enabled = true;
        }

        static internal void AddResponseLabel(string owner, string labelId, ActionTalk talk)
        {
            var resp = new ConversationEntry
            {
                owner = owner,
                labelId = labelId
            };
            conversationHistory.Add(resp);
            talk.currentHistory.Add(resp);
        }

        void CheckStartConditions()
        {
            var wu = Managers.GetManager<WorldUnitsHandler>();
            if (wu != null)
            {
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Terraformation);
                if (wut != null && wut.GetValue() >= 1100000)
                {
                    questPhase = QuestPhase.Arrival;
                    SaveState();
                    SetVisibilityViaCurrentPhase();
                }
            }
        }

        void CheckArrival()
        {
            var mh = Managers.GetManager<MeteoHandler>();
            if (mh == null)
            {
                return;
            }
            var pl = Managers.GetManager<PlanetLoader>();
            if (pl == null)
            {
                return;
            }
            var cp = pl.GetCurrentPlanetData();
            if (cp == null)
            {
                return;
            }
            var pid = cp.id;

            if (asteroid == null)
            {
                /*
                foreach (var me in mh.meteoEvents)
                {
                    logger.LogInfo("Dump meteo events: " + me.environmentVolume.name);
                }*/
                var mes = fMeteoHandlerMeteoEvents(mh);
                if (mes != null)
                {
                    MeteoEventData meteoEvent = default;

                    foreach (var me in mes)
                    {
                        var list = me.asteroidEventData?.asteroidGameObject?.GetComponent<Asteroid>()?.groupsSelected;

                        if (list != null && list.Count != 0 && list.Any(e => e.id == "PulsarQuartz"))
                        {
                            meteoEvent = me;
                            break;
                        }
                    }

                    if (meteoEvent != null)
                    {

                        logger.LogInfo("Launching arrival meteor: " + meteoEvent.environmentVolume.name);

                        mh.meteoSound.StartMeteoAudio(meteoEvent);
                        if (meteoEvent.asteroidEventData != null)
                        {
                            var selectedAsteroidEventData = Instantiate(meteoEvent.asteroidEventData);

                            var ah = Managers.GetManager<AsteroidsHandler>();

                            technicianDropLocationPerPlanet.TryGetValue(pid, out var technicianDropLocation);

                            var obj = Instantiate(
                                selectedAsteroidEventData.asteroidGameObject,
                                technicianDropLocation + new Vector3(0, 1000, 0),
                                Quaternion.identity,
                                ah.gameObject.transform
                            );
                            obj.transform.LookAt(technicianDropLocation);

                            asteroid = obj.GetComponent<Asteroid>();
                            var isr = 4 * Mathf.Max(asteroid.initialSpeedRange.x, asteroid.initialSpeedRange.y);
                            asteroid.initialSpeedRange = new Vector2(isr, isr);
                            asteroid.DefineVariables(fAsteroidsHandlerRandom(ah), false);
                            asteroid.SetLinkedAsteroidEvent(selectedAsteroidEventData);
                            asteroid.debrisDestroyTime = 15;
                            asteroid.placeAsteroidBody = false;
                            selectedAsteroidEventData.ChangeExistingAsteroidsCount(1);
                            selectedAsteroidEventData.ChangeTotalAsteroidsCount(1);
                        }
                    } 
                    else
                    {
                        questPhase = QuestPhase.Initial_Help;

                        var msh = Managers.GetManager<MessagesHandler>();
                        if (technicianMessageDict.TryGetValue(pid, out var msg))
                        {
                            msh.AddNewReceivedMessage(msg, true);
                        }

                        ShowChoice(dialogChoices["WhoAreYou"]);
                        SaveState();
                        SetVisibilityViaCurrentPhase();
                    }
                }
            }
            else
            {
                if (fAsteroidHasCrashed(asteroid))
                {
                    asteroid = null;
                    questPhase = QuestPhase.Initial_Help;

                    var msh = Managers.GetManager<MessagesHandler>();
                    if (technicianMessageDict.TryGetValue(pid, out var mess))
                    {
                        msh.AddNewReceivedMessage(mess, true);
                    }

                    ShowChoice(dialogChoices["WhoAreYou"]);
                    SaveState();
                    SetVisibilityViaCurrentPhase();
                }
            }
            
        }

        void CheckInitialHelp()
        {
            // mostly dialogue
        }

        void CheckBaseSetup()
        {
            var pl = Managers.GetManager<PlanetLoader>();
            if (pl == null)
            {
                return;
            }
            var cp = pl.GetCurrentPlanetData();
            if (cp == null)
            {
                return;
            }

            var pid = cp.id;
            technicianDropLocationPerPlanet.TryGetValue(pid, out var technicianDropLocation);

            var pm = GetPlayerMainController();
            if (Vector3.Distance(pm.transform.position, technicianDropLocation) >= 300)
            {

                questPhase = QuestPhase.Operating;
                ForceResetWorldUnits();
                ShowChoice(dialogChoices["NicePod"]);
                SaveState();
                SetVisibilityViaCurrentPhase();
                var mh = Managers.GetManager<MessagesHandler>();
                mh.AddNewReceivedMessage(technicianMessage2, true);
            }
        }

        static void ForceResetWorldUnits()
        {
            foreach (var wo in Managers.GetManager<WorldUnitsHandler>().GetAllPlanetUnits())
            {
                wo.ForceResetValues();
            }
        }

        void CheckOperating()
        {
            var mgr = Managers.GetManager<EnvironmentDayNightCycle>();
            if (mgr == null)
            {
                return;
            }
            var time = mgr.GetDayNightLerpValue();

            if (time >= 0.8)
            {
                avatar.SetPosition(technicianLocation3, technicianRotation3);
            }
            else
            {
                avatar.SetPosition(technicianLocation2, technicianRotation2);
            }
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

        static void SetVisibilityViaCurrentPhase()
        {
            logger.LogInfo("Escape Pod GameObject " + (escapePod.GetGameObject() != null));
            switch (questPhase)
            {
                case QuestPhase.Not_Started:
                case QuestPhase.Arrival:
                    {
                        avatar.SetVisible(false);
                        escapePod.GetGameObject()?.SetActive(false);
                        break;
                    }
                case QuestPhase.Initial_Help:
                    {
                        avatar.SetVisible(true);
                        avatar.SetPosition(technicianLocation1, technicianRotation1);
                        escapePod.GetGameObject()?.SetActive(true);
                        break;
                    }
                case QuestPhase.Base_Setup:
                    {
                        avatar.SetVisible(true);
                        escapePod.GetGameObject()?.SetActive(true);
                        break;
                    }
                case QuestPhase.Operating:
                    {
                        avatar.SetVisible(true);
                        avatar.SetPosition(technicianLocation2, technicianRotation2);
                        escapePod.GetGameObject()?.SetActive(true);
                        CreatePod();
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
                            ForceResetWorldUnits();
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
                                    dc.picked = "1" == pars[3];
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
            if (owner == "TechniciansExile_Convict")
            {
                return "#00FF00";
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

            string text = sb.ToString();

            escapePod.SetText(text);
            // FIXME notify clients about the state change
        }

        static void AddChoice(string id, string labelId)
        {
            dialogChoices[id] = new DialogChoice
            {
                id = id,
                labelId = labelId
            };
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            ingame = false;
            questPhase = QuestPhase.Not_Started;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MessagesHandler), "Init")]
        static void MessagesHandler_Init(Dictionary<int, MessageData> ____allAvailableMessages)
        {
            foreach (var msg in technicianMessageDict.Values)
            {
                ____allAvailableMessages.TryAdd(msg.GetMessageHashCode(), msg);
            }
            ____allAvailableMessages.TryAdd(technicianMessage2.GetMessageHashCode(), technicianMessage2);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldUnit), "SetIncreaseAndDecreaseForWorldObjects")]
        static void WorldUnit_SetIncreaseAndDecreaseForWorldObjects(
            WorldUnit __instance,
            bool __result, 
            DataConfig.WorldUnitType ____unitType,
            ref float ____increaseValuePerSec)
        {
            if (__result && questPhase == QuestPhase.Operating)
            {
                if (GameConfig.spaceGlobalMultipliersGroupIds.TryGetValue(____unitType, out var gid))
                {
                    foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
                    {
                        if (wo.GetGroup().GetId() == gid)
                        {
                            ____increaseValuePerSec *= 1.1f;
                        }
                    }
                    __instance.SetDirty(true);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowRockets), "SetRocketsDisplay")]
        static void UiWindowRockets_SetRocketsDisplay(
            GridLayoutGroup ___gridGenerationRockets,
            List<GroupData> ___rocketsGenerationGroups)
        {
            if (questPhase == QuestPhase.Operating)
            {
                List<WorldUnit> allWorldUnits = Managers.GetManager<WorldUnitsHandler>().GetAllPlanetUnits();
                var lines = ___gridGenerationRockets.GetComponentsInChildren<UiGroupLine>();

                logger.LogInfo("Rocket lines: " + lines.Length);
                foreach (var ln in lines)
                {
                    logger.LogInfo("  " + ln.labelText.text);
                }

                foreach (var gd in ___rocketsGenerationGroups)
                {
                    logger.LogInfo("  group " + gd.id);

                    var groupViaId = GroupsHandler.GetGroupViaId(gd.id);
                    var allWorldObjectsOfGroup = WorldObjectsHandler.Instance.GetObjectInWorldObjectsCount(gd, false);
                    logger.LogInfo("  wo count " + allWorldObjectsOfGroup);
                    if (allWorldObjectsOfGroup != 0)
                    {
                        foreach (WorldUnit worldUnit in allWorldUnits)
                        {
                            DataConfig.WorldUnitType unitType = worldUnit.GetUnitType();
                            var scaling = ((GroupItem)groupViaId).GetGroupUnitMultiplier(unitType);
                            if (scaling != 0f)
                            {
                                scaling *= 11f;
                                scaling /= 10f;
                                logger.LogInfo("  unit " + unitType + " " + scaling + " * ");
                                var unitLabel = Readable.GetWorldUnitLabel(unitType);

                                for (int j = 0; j < lines.Length; j++)
                                {
                                    var ln = lines[j];
                                    if (ln.labelText.text.StartsWith(unitLabel))
                                    {
                                        ln.labelText.text = unitLabel
                                            + " +" + (scaling * allWorldObjectsOfGroup).ToString() + "%";
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.CloseAllWindows))]
        static bool WindowsHandler_CloseAllWindows()
        {
            // by default, Enter toggles any UI. prevent this while our console is open
            if (avatar == null)
            {
                return true;
            }
            var a = avatar.avatar;
            if (a == null)
            {
                return true;
            }
            var c = a.GetComponent<ActionTalk>();
            if (c == null)
            {
                return true;
            }
            return c.conversationDialogCanvas == null;
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
                        break;
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrowerVegetationHarvestable), "OnInventoryModified")]
        static bool Patch_MachineGrowerVegetationHarvestable_OnInventoryModified(Inventory ____secondInventory)
        {
            return ____secondInventory != null;
        }

        internal class ConversationEntry
        {
            internal string owner;
            internal string labelId;
        }

        internal class DialogChoice
        {
            internal string id;
            internal string labelId;
            internal object[] parameters;
            internal bool visible;
            internal bool picked;
            internal bool enabled;
            internal bool highlighted;
        }

        internal class ActionTalk : Actionnable
        {
            internal GameObject conversationDialogCanvas;

            readonly List<GameObject> conversationHistoryLines = [];

            readonly List<GameObject> dialogChoiceLines = [];

            GameObject conversationPicture;

            GameObject conversationName;

            internal Sprite currentImage;

            internal string currentName;

            internal readonly List<DialogChoice> currentChoices = [];

            internal readonly List<ConversationEntry> currentHistory = [];

            internal Action<ActionTalk> OnConversationStart;

            internal Action<ActionTalk, DialogChoice> OnConversationChoice;

            internal int historyScrollOffset;

            public void Update()
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

                    var mp = Mouse.current.position.ReadValue();

                    for (int i = 0; i < currentChoices.Count; i++)
                    {
                        var ch = currentChoices[i];
                        var txt = dialogChoiceLines[i].GetComponent<TextMeshProUGUI>();
                        var rect = dialogChoiceLines[i].GetComponent<RectTransform>();

                        if (ch.enabled)
                        {
                            var t = txt.text;
                            if (IsWithin(rect, mp)) {
                                ch.highlighted = true;
                                ColorChoice(i, ch, txt);

                                if (Mouse.current.leftButton.wasPressedThisFrame)
                                {
                                    ch.highlighted = false;
                                    historyScrollOffset = 0;
                                    OnConversationChoice?.Invoke(this, ch);
                                    break;
                                }
                            }
                            else
                            {
                                ch.highlighted = false;
                                ColorChoice(i, ch, txt);
                            }
                        }
                    }
                }
            }

            static bool IsWithin(RectTransform rect, Vector2 mouse)
            {
                var lp = rect.localPosition;
                lp.x += Screen.width / 2 - rect.sizeDelta.x / 2;
                lp.y += Screen.height / 2 - rect.sizeDelta.y / 2;

                return mouse.x >= lp.x && mouse.y >= lp.y
                    && mouse.x <= lp.x + rect.sizeDelta.x && mouse.y <= lp.y + rect.sizeDelta.y;
            }

            internal void DestroyTalkDialog()
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
                conversationDialogCanvas = null;
            }

            public override void OnAction()
            {
                if (!(NetworkManager.Singleton?.IsServer ?? false))
                {
                    _hudHandler.DisplayCursorText("TechniciansExile_Talk_HostOnly", 5f);
                    return;
                }

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
                text.font = fontAsset;
                text.fontSize = fontSize;
                text.horizontalAlignment = HorizontalAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
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
                    tmp.font = fontAsset;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
                    tmp.font = fontAsset;
                    tmp.fontSize = fontSize;
                    tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
                fWindowsHandlerOpenedUi(wh) = DataConfig.UiType.TextInput;

                _hudHandler.CleanCursorTextIfCode("TechniciansExile_Talk");
                logger.LogInfo("Done OnAction");
            }

            internal void UpdateCurrents()
            {
                if (conversationDialogCanvas == null)
                {
                    return;
                }
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

                int j = 0;
                for (int i = currentHistory.Count - 1 - historyScrollOffset; i >= 0; i--)
                {
                    var che = currentHistory[i];
                    var txt = Localization.GetLocalizedString(che.labelId);

                    var lines = txt.Split(["<br>"], StringSplitOptions.None);

                    for (int k = lines.Length - 1; k >= 0; k--)
                    {
                        conversationHistoryLines[j].GetComponent<TextMeshProUGUI>().text = "<margin=1em>" + lines[k];

                        if (++j == conversationHistoryLines.Count)
                        {
                            break;
                        }
                    }

                    if (j == conversationHistoryLines.Count)
                    {
                        break;
                    }

                    var oc = GetColorForOwner(che.owner);
                    conversationHistoryLines[j].GetComponent<TextMeshProUGUI>().text = "<color=" + oc + ">"
                        + Localization.GetLocalizedString(che.owner);

                    if (++j == conversationHistoryLines.Count)
                    {
                        break;
                    }
                }

                j = 0;
                for (int i = 0; i < currentChoices.Count && j < dialogChoiceLines.Count; i++)
                {
                    ColorChoice(i, currentChoices[i], dialogChoiceLines[i].GetComponent<TextMeshProUGUI>());
                }
            }

            void ColorChoice(int i, DialogChoice choice, TextMeshProUGUI tmp)
            {
                var txt = Localization.GetLocalizedString(choice.labelId);
                if (choice.parameters != null && choice.parameters.Length != 0)
                {
                    txt = string.Format(txt, choice.parameters);
                }

                txt = (i + 1) + ".     " + txt;

                if (!choice.enabled)
                {
                    txt = "<color=#FF8080>" + txt;
                }
                else if (choice.highlighted)
                {
                    txt = "<color=#FFFF00>" + txt;
                }
                else if (choice.picked)
                {
                    txt = "<color=#808080>" + txt;
                }

                tmp.text = txt;
            }

            public override void OnHover()
            {
                _hudHandler.DisplayCursorText("TechniciansExile_Talk", 0f);
                base.OnHover();
            }

            public override void OnHoverOut()
            {
                _hudHandler.CleanCursorTextIfCode("TechniciansExile_Talk");

                base.OnHoverOut();
            }
        }
    }
}
