using BepInEx;
using FeatMultiplayer.MessageTypes;
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
        static string SanitizeUserName(string userName)
        {
            StringBuilder sb = new();
            for (int i = 0; i < userName.Length; i++)
            {
                var c = userName[i];
                if (c == ';' || c == '|' || c == '@' || c == '"')
                {
                    c = ' ';
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        static void ReceiveLogin(MessageLogin ml)
        {
            var cc = ml.sender;
            if (updateMode == MultiplayerMode.CoopHost)
            {
                string[] users = hostAcceptName.Value.Split(',');
                string[] passwords = hostAcceptPassword.Value.Split(',');

                for (int i = 0; i < Math.Min(users.Length, passwords.Length); i++)
                {
                    if (users[i] == ml.user && passwords[i] == ml.password)
                    {
                        cc.clientName = SanitizeUserName(ml.user);

                        if (!playerAvatars.ContainsKey(cc.clientName))
                        {
                            LogInfo("User login success: " + ml.user);
                            NotifyUser("User joined: " + ml.user);
                            Color color = Color.white;
                            try
                            {
                                color = MessageHelper.StringToColor(clientColor.Value);
                            }
                            catch (Exception)
                            {

                            }


                            PrepareShadowInventories(cc);
                            var avatar = PlayerAvatar.CreateAvatar(color, false, GetPlayerMainController());
                            avatar.SetName(cc.clientName);
                            playerAvatars[cc.clientName] = avatar;

                            cc.Send("Welcome\n");
                            cc.Signal();
                            cc.Send(new MessageGameMode()
                            {
                                modeIndex = (int)GameSettingsHandler.GetGameMode()
                            });
                            cc.Signal();

                            lastFullSync = Time.realtimeSinceStartup;
                            SendFullState();
                            SendTerrainLayers();
                            LaunchMeteorEventAfterLogin();
                            SendMessages();
                            SendStoryEvents();
                            SignalOtherPlayers(cc);

                            SendSavedPlayerPosition(cc);
                            return;
                        }
                        else
                        {
                            LogInfo("User already logged in: " + ml.user);
                            NotifyUser("User already logged in: " + ml.user);
                        }
                    }
                }

                LogInfo("User login failed: " + ml.user);
                cc.Send(EAccessDenied);
                cc.Signal();
                cc.Disconnect();
            }
        }

        static void ReceiveWelcome(MessagePlayerWelcome mpw)
        {
            Color color = Color.white;
            Color myColor = Color.white;
            try
            {
                color = MessageHelper.StringToColor(hostColor.Value);
                myColor = MessageHelper.StringToColor(clientColor.Value);
            }
            catch (Exception)
            {

            }
            var avatar = PlayerAvatar.CreateAvatar(color, true, GetPlayerMainController());
            avatar.SetName("< Host >");
            playerAvatars[""] = avatar; // host is always ""
            NotifyUserFromBackground("Joined the host.");
            firstTerraformSync = true;
            SendHost(new MessagePlayerColor { playerName = "", color = myColor }, true);
            var h = _towardsHost;
            if (h != null)
            {
                h.loginSuccess = true;
            }
        }

        static void ReceiveMessageClientDisconnected(MessageClientDisconnected mcd)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var clientName = mcd.sender.clientName;
                if (clientName != null)
                {
                    if (playerAvatars.TryGetValue(clientName, out var avatar))
                    {
                        LogInfo("Client disconnected: " + clientName);
                        avatar.Destroy();
                        playerAvatars.Remove(clientName);

                        var msg = new MessagePlayerLeft();
                        msg.playerName = clientName;

                        SendAllClients(msg, true);
                    }
                }
            } else
            {
                NotifyUserFromBackground("Host disconnected.\n\nIf you get this message right after joining the host,\ncheck if your username and password matches with the hosts' settings.", 30);
                DestroyAvatars();
            }
        }

        static void ReceiveMessageHostDisconnected(MessageDisconnected md)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                NotifyUserFromBackground("Host disconnected.\n\nIf you get this message right after joining the host,\ncheck if your username and password matches with the hosts' settings.", 30);
            }
            else
            {
                NotifyUserFromBackground("A client left the game: " + md.clientName);
            }
            DestroyAvatars();
        }
    }
}
