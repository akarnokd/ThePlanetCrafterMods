using BepInEx;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
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

        static void SignalOtherPlayers(ClientConnection cc)
        {
            // tell everyone else this player joined
            foreach (var conn in _clientConnections.Values)
            {
                if (conn.id != cc.id)
                {
                    // successfully joined clients
                    if (conn.clientName != null)
                    {
                        conn.Send(new MessagePlayerJoined { playerName = cc.clientName });
                        conn.Signal();

                        cc.Send(new MessagePlayerJoined { playerName = conn.clientName });
                    }
                }
            }
            cc.Signal();
        }

        static void ReceiveMessagePlayerJoined(MessagePlayerJoined mpj)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }

            if (playerAvatars.ContainsKey(mpj.playerName))
            {
                LogWarning("Player already joined? " + mpj.playerName);
                return;
            }

            Color color = Color.white;
            try
            {
                color = MessageHelper.StringToColor(clientColor.Value);
            }
            catch (Exception)
            {

            }

            LogInfo("Player joined: " + mpj.playerName);
            var avatar = PlayerAvatar.CreateAvatar(color, false, GetPlayerMainController());
            avatar.SetName(mpj.playerName);
            playerAvatars[mpj.playerName] = avatar;
        }

        static void ReceiveMessagePlayerLeft(MessagePlayerLeft mpl)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }

            if (playerAvatars.TryGetValue(mpl.playerName, out var playerAvatar))
            {
                LogInfo("Player left: " + mpl.playerName);
                playerAvatar.Destroy();
                playerAvatars.Remove(mpl.playerName);
            }
        }

        static void ReceiveMessagePlayerColor(MessagePlayerColor mpc)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (playerAvatars.TryGetValue(mpc.sender.clientName, out var playerAvatar))
                {
                    playerAvatar.SetColor(mpc.color);
                }
                mpc.playerName = mpc.sender.clientName;
                SendAllClientsExcept(mpc.sender.id, mpc, true);
            }
            else
            {
                if (playerAvatars.TryGetValue(mpc.playerName, out var playerAvatar))
                {
                    playerAvatar.SetColor(mpc.color);
                }
            }
        }
    }
}
