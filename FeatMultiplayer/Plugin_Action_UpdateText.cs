using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game calls UiWindowTextInput::OnChangeText when the text was changed, then updates the
        /// WorldObjectText component of the GameObject (which in turn updates WorldObject::text).
        /// 
        /// On the host, we let send out the new text to the client and let the original code do the local text changes.
        /// 
        /// On the client, we send out the new text but don't let the original code to execute the text changes.
        /// Instead, the host will do it for us and notify about us.
        /// </summary>
        /// <param name="___inputField">The input field containing the current text.</param>
        /// <param name="___worldObjectText">The WorldObject text that knows about which WorldObject's text is being changed.</param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowTextInput), nameof(UiWindowTextInput.OnChangeText))]
        static bool UiWindowTextInput_OnChangeText(TMP_InputField ___inputField, WorldObjectText ___worldObjectText)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                WorldObject wo = (WorldObject)worldObjectTextWorldObject.GetValue(___worldObjectText);
                var mut = new MessageUpdateText()
                {
                    id = wo.GetId(),
                    text = ___inputField.text
                };
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(mut, true);
                }
                else
                {
                    SendHost(mut, true);
                }
                // Do not change the client but wait for the server update
                return updateMode != MultiplayerMode.CoopClient;
            }
            return true;
        }

        static void ReceiveMessageUpdateText(MessageUpdateText mut)
        {
            if (worldObjectById.TryGetValue(mut.id, out var wo))
            {
                wo.SetText(mut.text);
                if (TryGetGameObject(wo, out var go) && go != null)
                {
                    var wot = go.GetComponentInChildren<WorldObjectText>();
                    if (wot != null)
                    {
                        wot.SetText(mut.text);

                        // Signal back the client immediately
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            SendAllClients(mut, true);
                        }
                    }
                }
            }
        }
    }
}
