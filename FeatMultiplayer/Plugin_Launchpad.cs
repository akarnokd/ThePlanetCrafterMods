using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections;
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
                if (___locationGameObject == null)
                {
                    Destroy(__instance);
                }
                if (TryFindRocket(___locationGameObject.transform.position, 1, out var rocketWo))
                {
                    LogInfo("ActionSendInSpace_OnAction: Request Launch " + DebugWorldObject(rocketWo));
                    Send(new MessageLaunch() { rocketId = rocketWo.GetId() });
                    Signal();
                }
                else
                {
                    ___hudHandler.DisplayCursorText("UI_nothing_to_launch_in_space", 2f, "");
                }
                return false;
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___locationGameObject == null)
                {
                    Destroy(__instance);
                }
                LaunchRocket(__instance, ___locationGameObject, true);
                return false;
            }
            return true;
        }

        static bool TryFindRocket(Vector3 around, float radius, out WorldObject rocketWo)
        {
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (Vector3.Distance(wo.GetPosition(), around) < radius && wo.GetGroup().GetId().StartsWith("Rocket"))
                {
                    rocketWo = wo;
                    return true;
                }
            }
            rocketWo = null;
            return false;
        }

        static void LaunchRocket(ActionSendInSpace __instance, GameObject ___locationGameObject,
            bool notify)
        {
            if (TryFindRocket(___locationGameObject.transform.position, 1, out var rocketWo))
            {
                if (TryGetGameObject(rocketWo, out var rocketGo))
                {
                    LogInfo("LaunchRocket: Launch " + DebugWorldObject(rocketWo));
                    if (notify)
                    {
                        Send(new MessageLaunch() { rocketId = rocketWo.GetId() });
                        Signal();
                    }

                    var machineRocket = rocketGo.GetComponent<MachineRocket>();
                    if (machineRocket == null)
                    {
                        machineRocket = rocketGo.AddComponent<MachineRocket>();
                    }
                    machineRocket.Ignite();

                    if (__instance.GetComponent<ActionnableInteractive>() != null)
                    {
                        __instance.GetComponent<ActionnableInteractive>().OnActionInteractive();
                    }

                    Managers.GetManager<MeteoHandler>().SendSomethingInSpace(rocketWo.GetGroup());
                    GetPlayerMainController().GetPlayerCameraShake().SetShaking(true, 0.06f, 0.0035f);
                    HandleRocketMultiplier(rocketWo);

                    machineRocket.StartCoroutine(RocketLaunchTracker(rocketWo, machineRocket));
                }
                else
                {
                    LogWarning("LaunchRocket:  Can't find the GameObject of " + DebugWorldObject(rocketWo));
                }
            }
            else
            {
                LogWarning("LaunchRocket: Can't find any rocket around " + ___locationGameObject.transform.position);
            }
        }

        /// <summary>
        /// After launching a rocket, hide the WorldObject after this amount of time and
        /// notify the client.
        /// </summary>
        static float hideRocketDelay = 41f;
        static float updateWorldObjectPositionDelay = 0.5f;

        static IEnumerator RocketLaunchTracker(WorldObject rocketWo, MachineRocket rocket)
        {
            float until = Time.time + hideRocketDelay;
            for (; ; )
            {
                if (until > Time.time)
                {
                    yield return new WaitForSeconds(updateWorldObjectPositionDelay);
                    rocketWo.SetPositionAndRotation(rocket.transform.position, rocket.transform.rotation);
                }
                else
                {
                    break;
                }
            }

            LogInfo("RocketLaunchTracker:   Orbit reached: " + DebugWorldObject(rocketWo));
            rocketWo.ResetPositionAndRotation();
            SendWorldObject(rocketWo, false);
        }

        /// <summary>
        /// We ignore the default ActionSendIntoSpace::HandleRocketMultiplier so it doesn't
        /// mess with the shadow rocket inventory nothing uses.
        /// </summary>
        /// <param name="rocketWo">The rocket used to find out what world unit it affects</param>
        static void HandleRocketMultiplier(WorldObject rocketWo)
        {
            foreach (WorldUnit worldUnit in Managers.GetManager<WorldUnitsHandler>().GetAllWorldUnits())
            {
                DataConfig.WorldUnitType unitType = worldUnit.GetUnitType();
                if (((GroupItem)rocketWo.GetGroup()).GetGroupUnitMultiplier(unitType) != 0f)
                {
                    Managers.GetManager<WorldUnitsHandler>().GetUnit(unitType).ForceResetValues();
                }
            }
        }

        /// <summary>
        /// After the iginite sequence, we tell the system to ignore collisions between
        /// the rocket and the player, so they can't knock them off course.
        /// We don't want to sync their phyiscs
        /// </summary>
        /// <param name="___rigidbody">The body of the rocket.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineRocket), nameof(MachineRocket.Ignite))]
        static void MachineRocket_Ignite(Rigidbody ___rigidbody)
        {
            Physics.IgnoreCollision(___rigidbody.GetComponent<Collider>(), GetPlayerMainController().GetComponent<Collider>());
        }

        static float rocketShakeDistance = 100f;

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
                    PlayerMainController pm = GetPlayerMainController();
                    if (Vector3.Distance(pm.transform.position, rocketWo.GetPosition()) < rocketShakeDistance) 
                        {
                        pm.GetPlayerCameraShake().SetShaking(true, 0.06f, 0.0035f);
                    }
                    LogInfo("ReceiveMessageLaunch: Launch " + DebugWorldObject(rocketWo));
                    if (updateMode == MultiplayerMode.CoopHost)
                    {
                        Managers.GetManager<MeteoHandler>().SendSomethingInSpace(rocketWo.GetGroup());

                        HandleRocketMultiplier(rocketWo);

                        machineRocket.StartCoroutine(RocketLaunchTracker(rocketWo, machineRocket));
                    }
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
