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
            playerAvatars[mpj.playerName] = PlayerAvatar.CreateAvatar(color, false, GetPlayerMainController());
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
    }
}
