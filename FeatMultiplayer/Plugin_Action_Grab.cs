using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game calls it when the player grabs a grabable object (self-dropped resources, food, etc.).
        /// 
        /// On the host, we need to intercept it so that once the item is grabbed, we can update the client about it.
        /// 
        /// On the client, we need to intercept this and instead of grabbing, ask the host to grab it for us and
        /// put it into our inventory.
        /// 
        /// Relying on just the inventory change doesn't work because a full sync may arrive between the client grab
        /// and host confirm of grabbing
        /// </summary>
        /// <param name="__instance">The component that points to the WorldObject we need</param>
        /// <param name="___itemWorldDisplayer">To hide the item world displayer</param>
        /// <param name="___playerSource">For the backpack and for playing the grab audio.</param>
        /// <returns>false in multiplayer, true in singleplayer</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "Grab")]
        static bool ActionGrabable_Grab(ActionGrabable __instance, 
            PlayerMainController ___playerSource, 
            ItemWorldDislpayer ___itemWorldDisplayer)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                var spi = __instance.GetComponent<OutsideGrowerSpawnInfo>();
                if (spi != null)
                {
                    ___itemWorldDisplayer.Hide();
                    ___playerSource.GetPlayerAudio().PlayGrab();

                    LogInfo("Request Grab Outside " + spi.machineId + ", " + spi.spawnId);
                    SendHost(new MessageGrowRemove()
                    {
                        machineId = spi.machineId,
                        spawnId = spi.spawnId,
                    }, true);
                }
                else
                {
                    var woa = __instance.GetComponent<WorldObjectAssociated>();

                    if (woa != null)
                    {
                        var wo = woa.GetWorldObject();

                        var mg = new MessageGrab()
                        {
                            id = wo.GetId(),
                            groupId = wo.GetGroup().GetId()
                        };
                        LogInfo("Request Grab " + DebugWorldObject(wo));
                        SendHost(mg, true);
                    }
                }

                return false;
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var woa = __instance.GetComponent<WorldObjectAssociated>();

                if (woa != null)
                {
                    var wo = woa.GetWorldObject();
                    ___playerSource.GetPlayerBackpack().GetInventory().AddItem(wo);
                    wo.SetDontSaveMe(_dontSaveMe: false);
                    SendWorldObjectToClients(wo, false);

                    ___playerSource.GetPlayerAudio().PlayGrab();
                    ___itemWorldDisplayer.Hide();
                    var grabedEvent = __instance.grabedEvent;
                    __instance.grabedEvent = null;
                    grabedEvent?.Invoke(wo);

                    Destroy(__instance.gameObject);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// If true, the event handler caller was the client.
        /// </summary>
        static bool clientGrabCallback;

        static void ReceiveMessageGrab(MessageGrab mg)
        {
            //LogInfo("ReceiveMessageGrab: " + mg.id);
            if (updateMode == MultiplayerMode.CoopHost)
            {
                worldObjectById.TryGetValue(mg.id, out var wo);
                if (wo == null && WorldObjectsIdHandler.IsWorldObjectFromScene(mg.id))
                {
                    Group g = GroupsHandler.GetGroupViaId(mg.groupId);
                    if (g != null)
                    {
                        LogInfo("ReceiveMessageGrab: Creating scene item " + mg.id + ", " + mg.groupId);
                        wo = WorldObjectsHandler.CreateNewWorldObject(g, mg.id);
                        SendWorldObjectToClients(wo, false);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageGrab: Unknown group " + mg.id + ", " + mg.groupId);
                    }
                }
                if (wo != null)
                {
                    Inventory inv = mg.sender.shadowBackpack;

                    if (inv.AddItem(wo))
                    {
                        LogInfo("ReceiveMessageGrab: Confirm Grab " + DebugWorldObject(wo));
                        mg.sender.Send(mg);
                        mg.sender.Signal();

                        // this should delete the object from client's views
                        SendWorldObjectToClients(wo, false);

                        if (TryGetGameObject(wo, out var go))
                        {
                            var ag = go.GetComponent<ActionGrabable>();
                            Grabed g = null;
                            if (ag != null)
                            {
                                g = ag.grabedEvent;
                                ag.grabedEvent = null;
                            }

                            TryRemoveGameObject(wo);
                            Destroy(go);

                            if (g != null)
                            {
                                clientGrabCallback = true;
                                try
                                {
                                    g.Invoke(wo);
                                }
                                finally
                                {
                                    clientGrabCallback = false;
                                }
                            }
                            LogInfo("ReceiveMessageGrab: GameObject destroy success " + DebugWorldObject(wo));
                        }
                        else
                        {
                            LogWarning("ReceiveMessageGrab: No GameObject found for " + DebugWorldObject(wo));
                        }
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageGrab: WorldObject not found " + mg.id + ", " + mg.groupId);
                }
            }
            else
            {
                if (worldObjectById.TryGetValue(mg.id, out var wo))
                {
                    if (TryGetGameObject(wo, out var go))
                    {
                        Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();
                        GetPlayerMainController().GetPlayerAudio().PlayGrab();

                        UnityEngine.Object.Destroy(go);
                        TryRemoveGameObject(wo);


                        LogInfo("ReceiveMessageGrab: Grab Success " + DebugWorldObject(wo));

                        Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer()
                        .AddInformation(2.5f, Readable.GetGroupName(wo.GetGroup()),
                            DataConfig.UiInformationsType.InInventory, wo.GetGroup().GetImage());
                    }
                    else
                    {
                        LogWarning("ReceiveMessageGrab: No GameObject found for " + DebugWorldObject(wo));
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageGrab: WorldObject not found " + mg.id + ", " + mg.groupId);
                }
            }
        }
    }
}
