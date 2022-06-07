using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static FieldInfo environmentDayNightCycleValue;

        static MessageTime hostTime;

        /// <summary>
        /// The vanilla game calls EnvironmentDayNightCycle::Start to start
        /// a coroutine that updates the environmental parameters as time progresses.
        /// 
        /// We start our own coroutine and send the current GetDayNightLerpValue from the host
        /// to the client or update the client's value from the host's message.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(EnvironmentDayNightCycle), "Start")]
        static void EnvironmentDayNightCycle_Start(EnvironmentDayNightCycle __instance)
        {
            environmentDayNightCycleValue = AccessTools.Field(typeof(EnvironmentDayNightCycle), "dayNightLerpValue");
            __instance.StartCoroutine(DayNightCycle(__instance, 0.5f));
        }

        static IEnumerator DayNightCycle(EnvironmentDayNightCycle __instance, float delay)
        {
            for (; ; )
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    Send(new MessageTime()
                    {
                        time = __instance.GetDayNightLerpValue()
                    });
                    Signal();
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (hostTime != null)
                    {
                        environmentDayNightCycleValue.SetValue(__instance, hostTime.time);
                        hostTime = null;
                    }
                }
                yield return new WaitForSeconds(delay);
            }
        }

        static void ReceiveMessageTime(MessageTime mt)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                hostTime = mt;
            }
        }
    }
}
