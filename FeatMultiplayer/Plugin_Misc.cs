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
        static void NotifyUser(string message, float duration = 5f)
        {
            Managers.GetManager<BaseHudHandler>()?.DisplayCursorText("", duration, message);
        }

        static void NotifyUserFromBackground(string message, float duration = 5f)
        {
            var msg = new NotifyUserMessage
            {
                message = message,
                duration = duration
            };
            _receiveQueue.Enqueue(msg);
        }

        static void ToggleConsumption()
        {
            slowdownConsumption.Value = !slowdownConsumption.Value;
            LogInfo("SlowdownConsumption: " + slowdownConsumption.Value);

            ResetGaugeConsumptions();
        }
    }
}
