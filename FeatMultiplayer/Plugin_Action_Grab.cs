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
        /// On the client, we need to intercept this and instead of grabbing, ask the host to grab it for us and
        /// put it into our inventory.
        /// 
        /// Relying on just the inventory change doesn't work because a full sync may arrive between the client grab
        /// and host confirm of grabbing
        /// </summary>
        /// <param name="__instance">The component that points to the WorldObject we need</param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGrabable), "Grab")]
        static bool ActionGrabable_Grab(ActionGrabable __instance)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                var spi = __instance.GetComponent<OutsideGrowerSpawnInfo>();
                if (spi != null)
                {
                    LogInfo("Request Grab Outside " + spi.machineId + ", " + spi.spawnId);
                    Send(new MessageGrowRemove()
                    {
                        machineId = spi.machineId,
                        spawnId = spi.spawnId,
                    });
                    Signal();
                }
                else
                {
                    var woa = __instance.GetComponent<WorldObjectAssociated>();

                    if (woa != null)
                    {
                        var wo = woa.GetWorldObject();
                        var mg = new MessageGrab()
                        {
                            id = wo.GetId()
                        };
                        LogInfo("Request Grab " + DebugWorldObject(wo));
                        Send(mg);
                        Signal();
                    }
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
            LogInfo("ReceiveMessageGrab: " + mg.id);
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (worldObjectById.TryGetValue(mg.id, out var wo))
                {
                    if (wo.GetIsPlaced())
                    {
                        Inventory inv = InventoriesHandler.GetInventoryById(shadowInventoryId);

                        if (inv.AddItem(wo))
                        {
                            wo.SetDontSaveMe(false);
                            if (TryGetGameObject(wo, out var go))
                            {
                                var ag = go.GetComponent<ActionGrabable>();
                                Grabed g = ag?.grabedEvent;

                                UnityEngine.Object.Destroy(go);
                                TryRemoveGameObject(wo);

                                LogInfo("ReceiveMessageGrab: Client.Grab " + mg.id);
                                Send(mg);
                                Signal();

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
                            }
                        }
                    }
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


                        LogInfo("ReceiveMessageGrab: Grabbed " + DebugWorldObject(wo));

                        Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer()
                        .AddInformation(2.5f, Readable.GetGroupName(wo.GetGroup()),
                            DataConfig.UiInformationsType.InInventory, wo.GetGroup().GetImage());
                    }
                    else
                    {
                        LogWarning("ReceiveMessageGrab: No GameObject found for " + DebugWorldObject(wo));
                    }
                }
            }
        }
    }
}
