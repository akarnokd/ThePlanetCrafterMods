using BepInEx;
using HarmonyLib;
using HSVPicker;
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
        /// The vanilla game uses a color picker component which uses a callback for when the color changed, then updates the
        /// the associated WorldObjectColor on its GameObject (which in turn updates the WorldObject::color).
        /// 
        /// We override this notification callback with our own.
        /// 
        /// On the host, we update the WorldObjectColor, then notify the client about the new color.
        /// 
        /// On the client, we don't change local color, but notify the host to change it for us.
        /// </summary>
        /// <param name="___colorPicker">The color picker object to replace the notification callbacks on.</param>
        /// <param name="_worldObjectColor">The associated WorldObjectColor that knows how to change colors on the GameObject and WorldObject.</param>
        /// <returns>True in singleplayer, false in multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowColorPicker), nameof(UiWindowColorPicker.SetColorWorldObject))]
        static bool UiWindowColorPicker_SetColorWorldObject(ColorPicker ___colorPicker,
            WorldObjectColor _worldObjectColor)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                ___colorPicker.onValueChanged.RemoveAllListeners();
                ___colorPicker.onValueChanged.AddListener(color =>
                {
                    color.a = 1f;
                    // Do not change the client but wait for the server update
                    if (updateMode != MultiplayerMode.CoopClient)
                    {
                        _worldObjectColor.SetColor(color);
                    }

                    WorldObject wo = (WorldObject)worldObjectColorWorldObject.GetValue(_worldObjectColor);
                    var mut = new MessageUpdateColor()
                    {
                        id = wo.GetId(),
                        color = color
                    };

                    if (updateMode == MultiplayerMode.CoopHost)
                    {
                        SendAllClients(mut, true);
                    }
                    else
                    {
                        SendHost(mut, true);
                    }
                });
                return false;
            }
            return true;
        }

        static void ReceiveMessageUpdateColor(MessageUpdateColor muc)
        {
            if (worldObjectById.TryGetValue(muc.id, out var wo))
            {
                wo.SetColor(muc.color);
                if (TryGetGameObject(wo, out var go) && go != null)
                {
                    var woc = go.GetComponentInChildren<WorldObjectColor>();
                    if (woc != null)
                    {
                        woc.SetColor(muc.color);

                        // Signal back the client immediately
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            SendAllClients(muc, true);
                        }
                    }
                }
            }
        }

    }
}
