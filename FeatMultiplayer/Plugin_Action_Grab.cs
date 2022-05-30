﻿using BepInEx;
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

                return false;
            }
            return true;
        }

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
                                UnityEngine.Object.Destroy(go);
                                TryRemoveGameObject(wo);

                                LogInfo("ReceiveMessageGrab: Client.Grab " + mg.id);
                                Send(mg);
                                Signal();
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
                        var ag = go.GetComponent<ActionGrabable>();
                        Grabed g = ag?.grabedEvent;

                        Managers.GetManager<DisplayersHandler>().GetItemWorldDislpayer().Hide();
                        UnityEngine.Object.Destroy(go);
                        TryRemoveGameObject(wo);

                        GetPlayerMainController().GetPlayerAudio().PlayGrab();
                        if (g != null)
                        {
                            g.Invoke(wo);
                        }

                        LogInfo("ReceiveMessageGrab: Grabbed " + DebugWorldObject(wo));
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