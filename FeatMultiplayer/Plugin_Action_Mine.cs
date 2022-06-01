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
                        LogInfo("Mined: " + worldObject.GetId() + " of " + worldObject.GetGroup().GetId() + " at " + worldObject.GetPosition());
                        Send(new MessageMined() {  id = worldObject.GetId() });
                        Signal();
                    }
                }
            }
        }

        static void ReceiveMessageMined(MessageMined mm)
        {
            LogInfo("ReceiveMessageMined: OtherPlayer mined " + mm.id);

            WorldObject wo1 = WorldObjectsHandler.GetWorldObjectViaId(mm.id);
            if (wo1 != null && wo1.GetIsPlaced())
            {
                LogInfo("ReceiveMessageMined: Hiding WorldObject " + mm.id);
                wo1.ResetPositionAndRotation();
                wo1.SetDontSaveMe(false);

                if (TryGetGameObject(wo1, out var go))
                {
                    TryRemoveGameObject(wo1);
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    LogWarning("ReceiveMessageMined: GameObject not found");
                }
                return;
            }
        }
    }
}
