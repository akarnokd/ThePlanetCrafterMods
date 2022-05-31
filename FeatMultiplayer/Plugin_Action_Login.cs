using BepInEx;
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
        static void ReceiveLogin(MessageLogin ml)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                string[] users = hostAcceptName.Value.Split(',');
                string[] passwords = hostAcceptPassword.Value.Split(',');

                for (int i = 0; i < Math.Min(users.Length, passwords.Length); i++)
                {
                    if (users[i] == ml.user && passwords[i] == ml.password)
                    {
                        LogInfo("User login success: " + ml.user);
                        NotifyUser("User joined: " + ml.user);
                        otherPlayer?.Destroy();
                        Color color = Color.white;
                        try
                        {
                            color = MessageHelper.StringToColor(clientColor.Value);
                        }
                        catch (Exception)
                        {

                        }
                        PrepareHiddenChests();
                        otherPlayer = PlayerAvatar.CreateAvatar(color, false);
                        Send("Welcome\n");
                        Signal();
                        lastFullSync = Time.realtimeSinceStartup;
                        SendFullState();
                        return;
                    }
                }

                LogInfo("User login failed: " + ml.user);
                _sendQueue.Enqueue(EAccessDenied);
                Signal();
            }
        }

        static void ReceiveWelcome()
        {
            otherPlayer?.Destroy();
            Color color = Color.white;
            try
            {
                color = MessageHelper.StringToColor(hostColor.Value);
            }
            catch (Exception)
            {

            }
            otherPlayer = PlayerAvatar.CreateAvatar(color, true);
            NotifyUserFromBackground("Joined the host.");
        }

        static void ReceiveDisconnected()
        {
            LogInfo("Disconnected");
            otherPlayer?.Destroy();
            otherPlayer = null;
            clientConnected = false;
            _sendQueue.Clear();
            receiveQueue.Clear();
            if (updateMode == MultiplayerMode.CoopHost)
            {
                NotifyUserFromBackground("Client disconnected");
            }
            else
            {
                NotifyUserFromBackground("Host disconnected");
            }
        }
    }
}
