using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibCommon
{
    /// <summary>
    /// Stores all the installed mod names and versions in a hidden Iron object's text.
    /// </summary>
    internal class SaveModInfo
    {
        const int storeInWorldObjectId = 90_000_000;

        internal static void Patch(Harmony harmony)
        {
            harmony.PatchAll(typeof(SaveModInfo));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            var sb = new StringBuilder();

            foreach (var pi in Chainloader.PluginInfos)
            {
                if (sb.Length > 0)
                {
                    sb.Append(";");
                }
                sb.Append(pi.Key).Append("=").Append(pi.Value.Metadata.Version);
            }

            var wo = WorldObjectsHandler.GetWorldObjectViaId(storeInWorldObjectId);
            if (wo == null)
            {
                var gr = GroupsHandler.GetGroupViaId("Iron");
                if (gr == null)
                {
                    return;
                }
                wo = WorldObjectsHandler.CreateNewWorldObject(gr, storeInWorldObjectId);
                wo.SetDontSaveMe(false);
            }
            wo.SetText(sb.ToString().Replace("@", " ").Replace("|", " "));
        }
    }
}
