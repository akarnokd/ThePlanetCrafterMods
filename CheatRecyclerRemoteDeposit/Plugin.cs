using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;
using System.Collections;
using System.Linq;

namespace CheatRecyclerRemoteDeposit
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatrecyclerremotedeposit", "(Cheat) Recyclers Deposit Into Remote Containers", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<string> defaultDepositAlias;

        static ConfigEntry<string> customDepositAliases;

        static ConfigEntry<int> autoRecyclePeriod;

        static ConfigEntry<int> maxRange;

        static readonly Dictionary<string, string> aliasMap = new();

        static Func<string> mpGetMode;

        static Action<int, WorldObject> mpSendWorldObject;

        static bool isAutomaticCall;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loading!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug mode with detailed logging (chatty!)");
            defaultDepositAlias = Config.Bind("General", "DefaultDepositAlias", "*Recycled", "The name of the container to deposit resources not explicity mentioned in CustomDepositAliases.");
            customDepositAliases = Config.Bind("General", "CustomDepositAliases", "", "Comma separated list of resource_id:alias to deposit into such named containers");
            autoRecyclePeriod = Config.Bind("General", "AutoRecyclePeriod", 5, "How often to auto-recycle, seconds. Zero means no auto-recycle.");
            maxRange = Config.Bind("General", "MaxRange", 20, "The maximum range to look for containers within. Zero means unlimited range.");

            ParseAliasConfig();

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Found " + modFeatMultiplayerGuid + ", the mod will be inactive on the client side");

                mpGetMode = GetApi<Func<string>>(pi, "apiGetMultiplayerMode");
                mpSendWorldObject = GetApi<Action<int, WorldObject>>(pi, "apiSendWorldObject");
            }
            else
            {
                Logger.LogInfo("Not Found " + modFeatMultiplayerGuid);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));

            Logger.LogInfo($"Plugin is loaded!");
        }

        static void ParseAliasConfig()
        {
            var aliasesStr = customDepositAliases.Value.Trim();
            if (aliasesStr.Length > 0)
            {
                var parts = aliasesStr.Split(',');
                foreach (var alias in parts )
                {
                    var idname = alias.Split(':');
                    if (idname.Length == 2)
                    {
                        aliasMap[idname[0]] = idname[1].ToLowerInvariant();
                    }
                }
            }
        }

        static IEnumerable<Inventory> FindInventoryFor(string gid, Vector3 center)
        {
            if (!aliasMap.TryGetValue(gid, out var name))
            {
                name = defaultDepositAlias.Value.ToLowerInvariant();
            }

            var range = maxRange.Value;

            foreach (var wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                var wot = wo.GetText();
                if (wot != null && wot.ToLowerInvariant().Contains(name))
                {
                    var xyz = wo.GetPosition();
                    if (Vector3.Distance(xyz, center) < range || range == 0)
                    {
                        var inv = InventoriesHandler.GetInventoryById(wo.GetLinkedInventoryId());
                        if (inv != null)
                        {
                            yield return inv;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionRecycle), nameof(ActionRecycle.OnAction))]
        static bool ActionRecycle_OnAction(ActionRecycle __instance, Collider ___craftSpawn)
        {
            if (modEnabled.Value)
            {
                if (mpGetMode != null && mpGetMode() == "CoopClient")
                {
                    return false;
                }
                var woMachine = __instance.GetComponentInParent<WorldObjectAssociated>().GetWorldObject();
                var pos = woMachine.GetPosition();
                Log("Handling Recycler " + woMachine.GetId() + " at " + pos);


                Inventory inventory = __instance.GetComponentInParent<InventoryAssociated>().GetInventory();
                if (inventory == null || inventory.GetInsideWorldObjects().Count == 0)
                {
                    Log("  Inventory empty");
                    return false;
                }
                WorldObject worldObject = inventory.GetInsideWorldObjects()[0];
                List<Group> ingredientsGroupInRecipe = worldObject.GetGroup().GetRecipe().GetIngredientsGroupInRecipe();

                Log("Recycling: " + worldObject.GetId() + " - " + worldObject.GetGroup().GetId());

                if (ingredientsGroupInRecipe.Count == 0)
                {
                    Log("  Failure - item has no recipe");
                    return false;
                }
                if (((GroupItem)worldObject.GetGroup()).GetCantBeRecycled())
                {
                    Log("  Failure - item marked as not recyclable");
                    return false;
                }

                Log("  Recipe: " + string.Join(", ", ingredientsGroupInRecipe.Select(g => g.GetId())));

                foreach (Group group in ingredientsGroupInRecipe)
                {
                    var wo = WorldObjectsHandler.CreateNewWorldObject(group);
                    if (mpGetMode != null && mpGetMode() == "CoopHost" && mpSendWorldObject != null)
                    {
                        mpSendWorldObject(0, wo);
                    }

                    Log("    Ingredient: " + wo.GetId() + " - " + group.GetId());

                    bool deposited = false;
                    bool foundInventory = false;

                    foreach (var inv in FindInventoryFor(group.GetId(), pos))
                    {
                        foundInventory = true;
                        if (inv.AddItem(wo))
                        {
                            Log("      Deposited into " + inv.GetId());
                            deposited = true;
                            break;
                        }
                    }

                    if (!deposited)
                    {
                        if (foundInventory) 
                        {
                            Log("      Failure - all target inventories full");
                            if (!isAutomaticCall)
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Recycler: All target inventories are full!");
                            }
                        }
                        else
                        {
                            Log("      Failure - no designated container found");
                            if (!isAutomaticCall)
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Recycler: No designated container found!");
                            }
                        }
                        WorldObjectsHandler.DropOnFloor(wo, ___craftSpawn.transform.position);
                    }
                }

                inventory.RemoveItem(worldObject, true);

                __instance.GetComponent<ActionnableInteractive>()?.OnActionInteractive();

                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionRecycle), "Start")]
        static void ActionRecylce_Start(ActionRecycle __instance)
        {
            if (modEnabled.Value)
            {
                var t = autoRecyclePeriod.Value;
                if (t > 0)
                {
                    __instance.StartCoroutine(AutoRecycle_Loop(__instance, t));
                }
            }
        }

        static IEnumerator AutoRecycle_Loop(ActionRecycle __instance, int t)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(t);
                if (mpGetMode == null || mpGetMode() != "CoopClient")
                {
                    isAutomaticCall = true;
                    try
                    {
                        __instance.OnAction();
                    }
                    finally
                    {
                        isAutomaticCall = false;
                    }
                }
            }
        }

        private static T GetApi<T>(BepInEx.PluginInfo pi, string name)
        {
            var fi = AccessTools.Field(pi.Instance.GetType(), name);
            if (fi == null)
            {
                throw new NullReferenceException("Missing field " + pi.Instance.GetType() + "." + name);
            }
            return (T)fi.GetValue(null);
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }
    }
}
