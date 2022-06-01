using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game calls ActionSendInSpace::OnAction when the player presses
        /// the button. It locates the rocket's world object, then locates the game object
        /// which links to that world object, attaches a MachineRocket component, ignites
        /// it, shakes the camera, triggers the appropriate meteor event, then
        /// accounts the rocket.
        /// 
        /// On the client, we send a launch request to via the rocket's world object id.
        /// 
        /// On the host, we need to confirm the launch request and notify the client
        /// to play out the launch itself
        /// </summary>
        /// <param name="__instance">The instance to call any associated ActionnableInteractive components</param>
        /// <param name="___locationGameObject">The object that holds the location info on the platform's rocket spawn position</param>
        /// <param name="___hudHandler">To notify the player there is nothing to launch.</param>
        /// <returns>False in multiplayer, true in singleplayer</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionSendInSpace), nameof(ActionSendInSpace.OnAction))]
        static bool ActionSendInSpace_OnAction(
            ActionSendInSpace __instance,
            GameObject ___locationGameObject, BaseHudHandler ___hudHandler)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (___locationGameObject != null)
                {
                    var rocketWo = WorldObjectsHandler.WorldObjectAtPosition(
                        ___locationGameObject.transform.position);

                    if (rocketWo != null)
                    {
                        LogInfo("ActionSendInSpace_OnAction: Launch " + DebugWorldObject(rocketWo));
                        Send(new MessageLaunch() { rocketId = rocketWo.GetId() });
                        Signal();
                    }
                    else
                    {
                        ___hudHandler.DisplayCursorText("UI_nothing_to_launch_in_space", 2f, "");
                    }
                } else
                {
                    Destroy(__instance);
                    ___hudHandler.DisplayCursorText("UI_nothing_to_launch_in_space", 2f, "");
                }
                return false;
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___locationGameObject != null)
                {
                    LaunchRocket(__instance, ___locationGameObject, true);
                }
                else
                {
                    Destroy(__instance);
                }
                return false;
            }
            return true;
        }

        static void LaunchRocket(ActionSendInSpace __instance, GameObject ___locationGameObject,
            bool notify)
        {
            var rocketWo = WorldObjectsHandler.WorldObjectAtPosition(
                    ___locationGameObject.transform.position);
            Collider[] colliders = Physics.OverlapBox(
                ___locationGameObject.transform.position,
                ___locationGameObject.transform.localScale * 100f);

            foreach (Collider collider in colliders)
            {
                GameObject go = collider.gameObject;
                if (go.transform.position == ___locationGameObject.transform.position)
                {
                    WorldObjectAssociated woa = go.GetComponent<WorldObjectAssociated>();
                    if (woa != null && woa.GetWorldObject() == rocketWo)
                    {
                        LogInfo("LaunchRocket: Launch " + DebugWorldObject(rocketWo));
                        if (notify)
                        {
                            Send(new MessageLaunch() { rocketId = rocketWo.GetId() });
                            Signal();
                        }

                        var machineRocket = go.GetComponent<MachineRocket>();
                        if (machineRocket == null)
                        {
                            machineRocket = go.AddComponent<MachineRocket>();
                        }
                        machineRocket.Ignite();

                        if (__instance.GetComponent<ActionnableInteractive>() != null)
                        {
                            __instance.GetComponent<ActionnableInteractive>().OnActionInteractive();
                        }

                        Managers.GetManager<MeteoHandler>().SendSomethingInSpace(rocketWo.GetGroup());
                        GetPlayerMainController().GetPlayerCameraShake().SetShaking(true, 0.06f, 0.0035f);
                        actionSendInSpaceHandleRocketMultiplier.Invoke(__instance, new object[] { rocketWo });
                    }
                }
            }
        }

        static void ReceiveMessageLaunch(MessageLaunch ml)
        {
            if (worldObjectById.TryGetValue(ml.rocketId, out var rocketWo))
            {
                if (TryGetGameObject(rocketWo, out var rocketGo))
                {
                    var machineRocket = rocketGo.GetComponent<MachineRocket>();
                    if (machineRocket == null)
                    {
                        machineRocket = rocketGo.AddComponent<MachineRocket>();
                    }
                    if (updateMode == MultiplayerMode.CoopHost)
                    {
                        Send(new MessageLaunch() { rocketId = rocketWo.GetId() });
                        Signal();
                    }
                    machineRocket.Ignite();
                    GetPlayerMainController().GetPlayerCameraShake().SetShaking(true, 0.06f, 0.0035f);
                    LogInfo("ReceiveMessageLaunch: Launch " + DebugWorldObject(rocketWo));
                }
                else
                {
                    LogWarning("ReceiveMessageLaunch: No rocket GameObject = " + DebugWorldObject(rocketWo));
                }
            }
            else
            {
                LogWarning("ReceiveMessageLaunch: Unknown rocketId " + ml.rocketId);
            }
        }
    }
}
