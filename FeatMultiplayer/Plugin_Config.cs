using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<int> port;
        static ConfigEntry<int> networkFrequency;
        static ConfigEntry<int> fullSyncDelay;
        static ConfigEntry<int> smallSyncDelay;
        static ConfigEntry<bool> streamerMode;

        static ConfigEntry<bool> hostMode;
        static ConfigEntry<bool> useUPnP;
        static ConfigEntry<string> hostServiceAddress;
        static ConfigEntry<string> hostAcceptName;
        static ConfigEntry<string> hostAcceptPassword;
        static ConfigEntry<string> hostColor;
        static ConfigEntry<int> hostLogLevel;
        static ConfigEntry<int> maxClients;
        static ConfigEntry<string> hostDisplayName;

        // client side properties
        static ConfigEntry<string> hostAddress;
        static ConfigEntry<string> clientName;
        static ConfigEntry<string> clientPassword;
        static ConfigEntry<string> clientColor;
        static ConfigEntry<int> clientLogLevel;


        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> slowdownConsumption;
        internal static ConfigEntry<int> playerNameFontSize;
        static ConfigEntry<string> emoteKey;
        static InputAction emoteAction;

        static ConfigEntry<string> playerLocatorKey;
        static InputAction playerLocatorAction;

        internal static Texture2D astronautFront;
        internal static Texture2D astronautBack;

        internal static Texture2D astronautFrontHost;
        internal static Texture2D astronautBackHost;

        internal static readonly Dictionary<string, List<Sprite>> emoteSprites = new();

        static readonly object logLock = new object();

        internal static string resourcesPath;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            theLogger = Logger;

            port = Config.Bind("General", "Port", 22526, "The port where the host server is running.");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size used");
            networkFrequency = Config.Bind("General", "Frequency", 20, "The frequency of checking the network for messages.");
            fullSyncDelay = Config.Bind("General", "SyncDelay", 3000, "Delay between full sync from the host to the client, in milliseconds");
            smallSyncDelay = Config.Bind("General", "SyncDelaySmall", 500, "Delay between small sync from the host to the client, in milliseconds");
            slowdownConsumption = Config.Bind("General", "SlowdownConsumption", false, "Slows down health/food/water consumption rate");
            playerNameFontSize = Config.Bind("General", "PlayerNameFontSize", 20, "Font size used to display the player's names above their avatar.");
            emoteKey = Config.Bind("General", "EmoteKey", "G", "The key to bring up the emote wheel.");
            playerLocatorKey = Config.Bind("General", "PlayerLocatorKey", "H", "Toggle the overlay that shows the other players' location");

            hostMode = Config.Bind("Host", "Host", false, "If true, loading a save will also host it as a multiplayer game.");
            useUPnP = Config.Bind("Host", "UseUPnP", false, "If behind NAT, use UPnP to manually map the HostPort to the external IP address?");
            hostAcceptName = Config.Bind("Host", "Name", "Buddy,Dude", "Comma separated list of client names the host will accept.");
            hostAcceptPassword = Config.Bind("Host", "Password", "password,wordpass", "Comma separated list of the plaintext(!) passwords accepted by the host, in pair with the Host/Name list.");
            hostColor = Config.Bind("Host", "Color", "1,1,1,1", "The color of the host avatar as comma-separated RGBA floats");
            hostServiceAddress = Config.Bind("Host", "ServiceAddress", "default", "The local IP address the host would listen, '' for auto address, 'default' for first IPv4 local address, 'defaultv6' for first IPv6 local address");
            hostLogLevel = Config.Bind("Host", "LogLevel", 2, "0 - debug+, 1 - info+, 2 - warning+, 3 - error");
            maxClients = Config.Bind("Host", "MaxClients", 4, "Number of clients that can join at a time");
            hostDisplayName = Config.Bind("Host", "DisplayName", "", "The name to display for the clients. If empty, <Host> is displayed");

            hostAddress = Config.Bind("Client", "HostAddress", "", "The IP address where the Host can be located from the client.");
            clientName = Config.Bind("Client", "Name", "Buddy,Dude", "The list of client names to join with.");
            clientPassword = Config.Bind("Client", "Password", "password,wordpass", "The plaintext(!) password presented to the host when joining their game.");
            clientColor = Config.Bind("Client", "Color", "0.75,0.75,1,1", "The color of the client avatar as comma-separated RGBA floats");
            clientLogLevel = Config.Bind("Client", "LogLevel", 2, "0 - debug+, 1 - info+, 2 - warning+, 3 - error");

            streamerMode = Config.Bind("General", "StreamerMode", false, "Hides the IP addresses in the main menu.");

            Assembly me = Assembly.GetExecutingAssembly();
            resourcesPath = Path.GetDirectoryName(me.Location);

            astronautFront = LoadPNG(Path.Combine(resourcesPath, "Astronaut_Front.png"));
            astronautBack = LoadPNG(Path.Combine(resourcesPath, "Astronaut_Back.png"));

            astronautFrontHost = LoadPNG(Path.Combine(resourcesPath, "Astronaut_Front_Host.png"));
            astronautBackHost = LoadPNG(Path.Combine(resourcesPath, "Astronaut_Back_Host.png"));

            InitReflectiveAccessors();
            
            TryInstallMachineOverrides();

            ApiSetup();

            EmoteSetup();

            OverlaySetup();

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(100, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static FieldInfo worldUnitCurrentTotalValue;
        static FieldInfo worldUnitsPositioningWorldUnitsHandler;
        static FieldInfo worldUnitsPositioningHasMadeFirstInit;
        static FieldInfo playerMultitoolCanUseLight;
        static FieldInfo worldObjectTextWorldObject;
        static FieldInfo worldObjectColorWorldObject;
        static MethodInfo sectorSceneLoaded;
        static MethodInfo actionSendInSpaceHandleRocketMultiplier;
        static FieldInfo machineGrowerIfLinkedGroupHasEnergy;
        static FieldInfo machineGrowerIfLinkedGroupWorldObject;
        static MethodInfo machineGrowerIfLinkedGroupSetInteractiveStatus;
        static FieldInfo uiWindowGroupSelectorWorldObject;
        static FieldInfo worldUnitsPositioningHandlerAllWorldUnitPositionings;
        /// <summary>
        /// PlayerEquipment.hasCleanConstructionChip
        /// </summary>
        static FieldInfo playerEquipmentHasCleanConstructionChip;
        /// <summary>
        /// PlayerEquipment.hasCompassChip
        /// </summary>
        static FieldInfo playerEquipmentHasCompassChip;

        static FieldInfo meteoHandlerMeteoEvents;

        static FieldInfo playerEquipmentHasDeconstructT2;

        static MethodInfo droneSetClosestAvailableDroneStation;

        static AccessTools.FieldRef<UiWindowContainer, Inventory> uiWindowContainerRightInventory;
        static AccessTools.FieldRef<Inventory, InventoryDisplayer> inventoryDisplayer;
        static MethodInfo logisticSelectorSetListsDisplay;
        static AccessTools.FieldRef<PlayerLarvaeAround, int> playerLarvaeAroundNoLarvaeZoneEntered;

        static void InitReflectiveAccessors()
        {
            worldUnitCurrentTotalValue = AccessTools.Field(typeof(WorldUnit), "currentTotalValue");
            worldUnitsPositioningWorldUnitsHandler = AccessTools.Field(typeof(WorldUnitPositioning), "worldUnitsHandler");
            worldUnitsPositioningHasMadeFirstInit = AccessTools.Field(typeof(WorldUnitPositioning), "hasMadeFirstInit");
            playerMultitoolCanUseLight = AccessTools.Field(typeof(PlayerMultitool), "canUseLight");
            worldObjectTextWorldObject = AccessTools.Field(typeof(WorldObjectText), "worldObject");
            worldObjectColorWorldObject = AccessTools.Field(typeof(WorldObjectColor), "worldObject");

            sectorSceneLoaded = AccessTools.Method(typeof(Sector), "SceneLoaded", new Type[] { typeof(AsyncOperation) });

            actionSendInSpaceHandleRocketMultiplier = AccessTools.Method(typeof(ActionSendInSpace), "HandleRocketMultiplier", new Type[] { typeof(WorldObject) });

            machineGrowerIfLinkedGroupHasEnergy = AccessTools.Field(typeof(MachineGrowerIfLinkedGroup), "hasEnergy");
            machineGrowerIfLinkedGroupWorldObject = AccessTools.Field(typeof(MachineGrowerIfLinkedGroup), "worldObject"); ;
            machineGrowerIfLinkedGroupSetInteractiveStatus = AccessTools.Method(typeof(MachineGrowerIfLinkedGroup), "SetInteractiveStatus", new Type[] { typeof(bool), typeof(bool) });

            if (Chainloader.PluginInfos.TryGetValue(modCheatInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "GetStackCount", new Type[] { typeof(List<WorldObject>) });
                getStackCount = AccessTools.MethodDelegate<Func<List<WorldObject>, int>>(mi, null);

                stackSize = (ConfigEntry<int>)AccessTools.Field(pi.Instance.GetType(), "stackSize").GetValue(null);

                var getMultiplayerModeField = AccessTools.Field(pi.Instance.GetType(), "getMultiplayerMode");
                getMultiplayerModeField.SetValue(pi.Instance, new Func<string>(GetMultiplayerMode));
            }

            uiWindowGroupSelectorWorldObject = AccessTools.Field(typeof(UiWindowGroupSelector), "worldObject");

            playerEquipmentHasCleanConstructionChip = AccessTools.Field(typeof(PlayerEquipment), "hasCleanConstructionChip");
            playerEquipmentHasCompassChip = AccessTools.Field(typeof(PlayerEquipment), "hasCompassChip");

            worldUnitsPositioningHandlerAllWorldUnitPositionings = AccessTools.Field(typeof(WorldUnitPositioningHandler), "allWorldUnitPositionings");

            meteoHandlerMeteoEvents = AccessTools.Field(typeof(MeteoHandler), "meteoEvents");

            playerEquipmentHasDeconstructT2 = AccessTools.Field(typeof(PlayerEquipment), "hasDeconstructT2");

            droneSetClosestAvailableDroneStation = AccessTools.Method(typeof(Drone), "SetClosestAvailableDroneStation");

            uiWindowContainerRightInventory = AccessTools.FieldRefAccess<UiWindowContainer, Inventory>("inventoryRight");

            inventoryDisplayer = AccessTools.FieldRefAccess<Inventory, InventoryDisplayer>("inventoryDisplayer");
            logisticSelectorSetListsDisplay = AccessTools.Method(typeof(LogisticSelector), "SetListsDisplay");

            playerLarvaeAroundNoLarvaeZoneEntered = AccessTools.FieldRefAccess<PlayerLarvaeAround, int>("noLarvaeZoneEntered");
        }

    }
}
