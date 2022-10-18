using BepInEx;
using BepInEx.Configuration;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static void EmoteSetup()
        {
            AddEmote("hello");

            EnsureKeyboard(emoteKey);
            emoteAction = new InputAction(name: "Emote", binding: emoteKey.Value);
            emoteAction.Enable();
        }

        static void EnsureKeyboard(ConfigEntry<string> configEntry)
        {
            var str = configEntry.Value;
            if (!str.StartsWith("<Keyboard>/"))
            {
                configEntry.Value = "<Keyboard>/" + str;
            }
        }

        static void AddEmote(string id)
        {
            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);
            var tex = LoadPNG(Path.Combine(dir, "emote_" + id + ".png"));

            int h = tex.height;
            int strips = tex.width / h;
            LogInfo("Loading " + id + " (" + tex.width + " x " + h + ", " + strips + " strips");

            int x = 0;
            List<Sprite> sprites = new();
            for (int i = 0; i < strips; i++)
            {
                sprites.Add(Sprite.Create(tex, new Rect(x, 0, h, h), new Vector2(0.5f, 0.5f)));

                x += h;
            }
            emoteSprites[id] = sprites;
        }

        static void HandleEmoting()
        {
            var ap = GetPlayerMainController();
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (ap == null || wh == null || wh.GetHasUiOpen())
            {
                return;
            }

            if (emoteAction.WasPressedThisFrame())
            {
                SendEmote("hello"); // For now
            }
        }

        static void SendEmote(string id)
        {
            LogInfo("SendEmote: " + id);
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var msg = new MessageEmote { playerName = "", emoteId = id }; // playerName doesn't matter here
                SendAllClients(msg, true);
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                var msg = new MessageEmote { playerName = "", emoteId = id }; // playerName doesn't matter here
                SendHost(msg, true);
            }
        }

        static void ReceiveMessageEmote(MessageEmote mee)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                mee.playerName = mee.sender.clientName;
                SendAllClientsExcept(mee.sender.id, mee, true);
            }
            if (playerAvatars.TryGetValue(mee.playerName, out var playerAvatar))
            {
                if (emoteSprites.TryGetValue(mee.emoteId, out var sprites))
                {
                    LogInfo("ReceiveMessageEmote : " + mee.playerName + ", " + mee.emoteId);
                    playerAvatar.Emote(sprites, 0.25f, 3);
                }
                else
                {
                    LogWarning("Unknown emote request " + mee.emoteId + " from " + mee.playerName);
                }
            }
            else
            {
                LogWarning("Unknown player for emote request " + mee.playerName + " (" + mee.emoteId + ")");
            }
        }
    }
}
