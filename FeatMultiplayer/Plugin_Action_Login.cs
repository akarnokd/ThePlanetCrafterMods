using BepInEx;
using BepInEx.Bootstrap;
using FeatMultiplayer.MessageTypes;
using LibCommon;
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

                if (users.Length != passwords.Length)
                {
                    LogError("The number of listed users and passwords in the config must match! U-" + users.Length + " != P-" + passwords.Length);
                }

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

                            cc.Send(CreateWelcome());
                            cc.Signal();
                            // FIXME 0.9.x introduced a lot more game mode settings.
                            SendGameMode(cc);

                            lastFullSync = Time.realtimeSinceStartup;
                            SendFullState();
                            SendDroneTargets();
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
                            LogWarning("User already logged in: " + ml.user);
                            NotifyUser("User already logged in: " + ml.user);
                        }
                    }
                }

                LogWarning("User login failed: " + ml.user);
                cc.Send(EAccessDenied);
                cc.Signal();
                cc.Disconnect();
            }
        }

        static MessagePlayerWelcome CreateWelcome()
        {
            var msg = new MessagePlayerWelcome();
            msg.multiplayerModVersion = new Version(PluginInfo.PLUGIN_VERSION);

            foreach (var pi in Chainloader.PluginInfos)
            {
                msg.modVersions[pi.Key] = pi.Value.Metadata.Version;
            }
            msg.hostDisplayName = hostDisplayName.Value;
            return msg;
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
            if (string.IsNullOrEmpty(mpw.hostDisplayName)) {
                avatar.SetName("< Host >");
            } else
            {
                avatar.SetName(mpw.hostDisplayName);
            }
            playerAvatars[""] = avatar; // host is always ""
            NotifyUserFromBackground("Joined the host.");
            firstTerraformSync = true;
            SendHost(new MessagePlayerColor { playerName = "", color = myColor }, true);
            var h = _towardsHost;
            if (h != null)
            {
                h.loginSuccess = true;
            }

            CheckMods(mpw);
        }

        static void CheckMods(MessagePlayerWelcome mpw)
        {
            if (new Version(PluginInfo.PLUGIN_VERSION).CompareTo(mpw.multiplayerModVersion) != 0)
            {
                var str = "Multiplayer mod version mismatch!\n\n- Host's version: <color=yellow>"
                    + mpw.multiplayerModVersion + "</color>\n- Your version: <color=yellow>" + PluginInfo.PLUGIN_VERSION
                    + "</color>\n\nPlease make sure you run the same version.";
                NotifyUser(str, 60);
                LogWarning(str);
            }
            else
            {
                List<string> diff = new();
                foreach (var mod in mpw.modVersions)
                {
                    if (Chainloader.PluginInfos.TryGetValue(mod.Key, out var info))
                    {
                        if (info.Metadata.Version.CompareTo(mod.Value) != 0)
                        {
                            var str = "- " + mod.Key + "\n    Host's version: <color=yellow>" + mod.Value + "</color>, Your version: <color=yellow>" + info.Metadata.Version + "</color>";
                            if (diff.Count == 5)
                            {
                                diff.Add("...");
                            }
                            else
                            if (diff.Count < 5)
                            {
                                diff.Add(str);
                            }
                        }
                    }
                }

                if (diff.Count != 0)
                {
                    var str2 = "Warning! Mod version mismatch(es) detected!\n\n"
                        + string.Join("\n", diff)
                        + "\n\nPlease make sure you run the same version(s).";
                    NotifyUser(str2, 60);
                    LogWarning(str2);
                }
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
