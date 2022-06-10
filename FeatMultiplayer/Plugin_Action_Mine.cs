using BepInEx;
using HarmonyLib;
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
        /// The vanilla game calls ActionMinable::FinishMining when the player spent enough time aiming at the resource.
        /// 
        /// We need to prefix this method because the vanilla method destroys information needed for notifying the other party,
        /// or simply crashes. Therefore, we also need to check the mining times to detect when the mining ended.
        /// </summary>
        /// <param name="__instance">The instance on which mining is performed and tracked</param>
        /// <param name="___playerSource">The player object to figure out how long it should take to mine.</param>
        /// <param name="___timeMineStarted">When the mining has started.</param>
        /// <param name="___timeMineStoped">When the mining has ended.</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionMinable), "FinishMining")]
        static void ActionMinable_FinishMining(ActionMinable __instance,
            PlayerMainController ___playerSource, float ___timeMineStarted, float ___timeMineStoped)
        {
            if (___timeMineStarted - ___timeMineStoped > ___playerSource.GetMultitool().GetMultiToolMine().GetMineTime())
            {
                WorldObjectAssociated woa = __instance.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    WorldObject worldObject = woa.GetWorldObject();
                    if (worldObject != null)
                    {
                        SendWorldObject(worldObject, false);

                        int woid = worldObject.GetId();
                        string groupId = worldObject.GetGroup().GetId();
                        LogInfo("Mined: " + woid + " of " + groupId + " at " + woa.gameObject.transform.position);
                        Send(new MessageMined() {  id = woid, groupId = groupId });
                        Signal();
                    }
                }
            }
        }

        static void ReceiveMessageMined(MessageMined mm)
        {
            LogInfo("ReceiveMessageMined: OtherPlayer mined " + mm.id + ", " + mm.groupId);

            WorldObject wo1 = WorldObjectsHandler.GetWorldObjectViaId(mm.id);

            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (wo1 == null && WorldObjectsIdHandler.IsWorldObjectFromScene(mm.id))
                {
                    var gr = GroupsHandler.GetGroupViaId(mm.groupId);
                    if (gr != null)
                    {
                        wo1 = WorldObjectsHandler.CreateNewWorldObject(gr, mm.id);
                        LogInfo("ReceiveMessageMined: Creating WorldObject for " + mm.id + ", " + mm.groupId);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageMined: Unknown group for " + mm.id + ", " + mm.groupId);
                    }
                }
            }

            if (wo1 != null)
            {
                LogInfo("ReceiveMessageMined: Hiding WorldObject " + mm.id + ", " + mm.groupId);
                wo1.ResetPositionAndRotation();
                wo1.SetDontSaveMe(false);
                SendWorldObject(wo1, false);

                if (TryGetGameObject(wo1, out var go))
                {
                    TryRemoveGameObject(wo1);
                    Destroy(go);
                }
                else
                {
                    LogWarning("ReceiveMessageMined: GameObject not found for " + mm.id + ", " + mm.groupId);
                }
                return;
            }
            else
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    LogWarning("ReceiveMessageMined: Unknown WorldObject " + mm.id + ", " + mm.groupId);
                }
            }
        }
    }
}
