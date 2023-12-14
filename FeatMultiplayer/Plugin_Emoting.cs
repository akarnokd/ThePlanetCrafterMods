using BepInEx;
using BepInEx.Configuration;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
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
using UnityEngine.UI;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {

        static GameObject emoteCanvas;

        static readonly string[] emoteWheelSlots = new[]
        {
            "help",
            "hello",
            "yes",
            null,
            "comehere",
            null,
            "no",
            null
        };
        static readonly string[] emoteWheelSlotNames = new[]
        {
            "Help",
            "Hello",
            "Yes",
            null,
            "Come Here",
            null,
            "No",
            null
        };

        static readonly GameObject[] emoteWheelSlotBackgrounds = new GameObject[8];

        static Color emoteDefaultBackgroundColor = new Color(0, 0, 0, 0.95f);
        static Color emoteSelectedBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.95f);

        static GameObject emoteClose;

        static void HandleEmoting()
        {
            var ap = GetPlayerMainController();
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (ap == null || wh == null)
            {
                return;
            }

            if (!wh.GetHasUiOpen() && emoteCanvas != null)
            {
                DestroyEmoteCanvas();
                return;
            }

            if (emoteAction.WasPressedThisFrame() && emoteCanvas != null)
            {
                DestroyEmoteCanvas();
                return;
            }

            var radius = Screen.height / 4.0f;
            var panelSizes = radius * 2 / 3;

            if (emoteCanvas != null)
            {
                // track mouse movement
                var mp = Mouse.current.position.ReadValue();
                var relativeToScreenCenter = new Vector2(mp.x - Screen.width / 2, mp.y - Screen.height / 2);

                var mouseRadiusSqr = relativeToScreenCenter.sqrMagnitude;
                var minRadiusSqr = radius * radius / 4;

                if (mouseRadiusSqr >= minRadiusSqr) {
                    var mouseAngle = Mathf.Atan2(relativeToScreenCenter.y, relativeToScreenCenter.x);
                    if (mouseAngle < 0)
                    {
                        mouseAngle += 2 * Mathf.PI;
                    }
                    var unitAngle = 2 * Mathf.PI / emoteWheelSlotBackgrounds.Length;
                    for (int i = 0; i < emoteWheelSlotBackgrounds.Length; i++)
                    {
                        var go = emoteWheelSlotBackgrounds[i];
                        var e = emoteWheelSlots[i];
                        if (go != null)
                        {
                            var minAngle = i * unitAngle - unitAngle / 2;
                            var maxAngle = i * unitAngle + unitAngle / 2;


                            bool selected = false;
                            var colorMode = emoteDefaultBackgroundColor;
                            if (!string.IsNullOrEmpty(e))
                            {
                                if (i == 0)
                                {
                                    if ((Mathf.PI * 2 + minAngle <= mouseAngle && mouseAngle < 2 * Math.PI)
                                        || 0 <= mouseAngle && mouseAngle < maxAngle)
                                    {
                                        colorMode = emoteSelectedBackgroundColor;
                                        selected = true;
                                    }
                                }
                                else if (minAngle <= mouseAngle && mouseAngle < maxAngle)
                                {
                                    colorMode = emoteSelectedBackgroundColor;
                                    selected = true;
                                }
                            }
                            go.GetComponent<Image>().color = colorMode;

                            if (selected && Mouse.current.leftButton.wasPressedThisFrame)
                            {
                                if (!string.IsNullOrEmpty(e))
                                {
                                    SendEmote(e);
                                    DestroyEmoteCanvas();
                                }
                            }
                        }
                    }
                    if (emoteClose != null)
                    {
                        emoteClose.GetComponent<Image>().color = emoteDefaultBackgroundColor;
                    }
                }
                else
                {
                    foreach (var go in emoteWheelSlotBackgrounds)
                    {
                        if (go != null)
                        {
                            go.GetComponent<Image>().color = emoteDefaultBackgroundColor;
                        }
                    }
                    emoteClose.GetComponent<Image>().color = emoteSelectedBackgroundColor;

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        DestroyEmoteCanvas();
                    }
                }

                return;
            }

            if (!emoteAction.WasPressedThisFrame() || wh.GetHasUiOpen())
            {
                return;
            }

            LogInfo("Creating MultiplayerEmoteCanvas");
            emoteCanvas = new GameObject("MultiplayerEmoteCanvas");
            var c = emoteCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            //ourCanvas.transform.SetParent(canvas.transform);
            emoteCanvas.transform.SetAsLastSibling();
            c.sortingOrder = 110;

            for (int i = 0; i < emoteWheelSlots.Length; i++)
            {
                var pos = new Vector3(Mathf.Cos(2 * Mathf.PI * i / emoteWheelSlots.Length) * radius, Mathf.Sin(2 * Mathf.PI * i / emoteWheelSlots.Length) * radius, 0);

                LogInfo("  Creating slot " + i + " background");
                var go = new GameObject("MultiplayerEmoteCanvas-Slot-" + i + "-Background");
                go.transform.SetParent(emoteCanvas.transform);
                var img = go.AddComponent<Image>();
                img.color = emoteDefaultBackgroundColor;

                var rect = go.GetComponent<RectTransform>();
                rect.localPosition = pos;
                rect.sizeDelta = new Vector2(panelSizes, panelSizes);

                emoteWheelSlotBackgrounds[i] = go;

                string e = emoteWheelSlots[i];
                if (!string.IsNullOrEmpty(e))
                {
                    LogInfo("  Creating slot " + i);
                    LogInfo("    Creating slot " + i + " text");

                    var emoteName = new GameObject("MultiplayerEmoteCanvas-Slot-" + i + "-Name");
                    emoteName.transform.SetParent(emoteCanvas.transform);

                    var txt = emoteName.AddComponent<Text>();
                    txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    txt.text = emoteWheelSlotNames[i];
                    txt.color = new Color(1f, 1f, 1f, 1f);
                    txt.fontSize = (int)fontSize.Value;
                    txt.resizeTextForBestFit = false;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.alignment = TextAnchor.MiddleCenter;
                    rect = txt.GetComponent<RectTransform>();
                    rect.localPosition = pos - new Vector3(0, panelSizes / 2 - fontSize.Value / 2, 0);

                    LogInfo("    Creating slot " + i + " icon");
                    var emoteIcon = new GameObject("MultiplayerEmoteCanvas-Slot-" + i + "-Icon");
                    emoteIcon.transform.SetParent(emoteCanvas.transform);
                    var imgr = emoteIcon.AddComponent<Image>();
                    imgr.sprite = emoteSprites[e][0];
                    imgr.color = Color.white;
                    rect = emoteIcon.GetComponent<RectTransform>();
                    rect.localPosition = pos + new Vector3(0, fontSize.Value / 2f, 0);
                    rect.sizeDelta = new Vector2(panelSizes - fontSize.Value, panelSizes - fontSize.Value);

                    LogInfo("  Creating slot " + i + " done");
                }
                else
                {
                    LogInfo("  Creating slot " + i + " <- empty");
                }

            }

            {
                LogInfo("  Creating close background");
                emoteClose = new GameObject("MultiplayerEmoteCanvas-CloseEmoteBackground");
                emoteClose.transform.SetParent(emoteCanvas.transform);

                var img = emoteClose.AddComponent<Image>();
                img.color = emoteDefaultBackgroundColor;

                var rect = img.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(0, 0, 0);
                rect.sizeDelta = new Vector2(radius * 2 / 3, radius * 2 / 3);

                LogInfo("  Creating close text");

                var closeEmoteText = new GameObject("MultiplayerEmoteCanvas-CloseEmote");
                closeEmoteText.transform.SetParent(emoteCanvas.transform);

                var txt = closeEmoteText.AddComponent<Text>();
                txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.text = "Close";
                txt.color = new Color(1f, 1f, 1f, 1f);
                txt.fontSize = (int)fontSize.Value;
                txt.resizeTextForBestFit = false;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.alignment = TextAnchor.MiddleCenter;
                rect = txt.GetComponent<RectTransform>();
                rect.localPosition = new Vector3(0, 0, 0);
            }

            LogInfo("  Showing cursor");
            Cursor.visible = true;
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.TextInput;
            LogInfo("  Done");
        }

        static void DestroyEmoteCanvas()
        {
            Destroy(emoteCanvas);
            emoteCanvas = null;
            emoteClose = null;
            Array.Clear(emoteWheelSlotBackgrounds, 0, emoteWheelSlotBackgrounds.Length);
            //Cursor.visible = false;
            var wh = Managers.GetManager<WindowsHandler>();
            AccessTools.FieldRefAccess<WindowsHandler, DataConfig.UiType>(wh, "openedUi") = DataConfig.UiType.Null;
        }

        static void EmoteSetup()
        {
            AddEmote("hello");
            AddEmote("yes");
            AddEmote("no");
            AddEmote("comehere");
            AddEmote("help");

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
            var f = Path.Combine(resourcesPath, "emote_" + id + ".png");
            if (!File.Exists(f))
            {
                LogError("Could not find emote " + f);
                return;
            }
            var tex = LoadPNG(f);

            int h = tex.height;
            int strips = tex.width / h;
            LogInfo("Loading " + id + " (" + tex.width + " x " + h + ", " + strips + " strips)");

            int x = 0;
            List<Sprite> sprites = new();
            for (int i = 0; i < strips; i++)
            {
                sprites.Add(Sprite.Create(tex, new Rect(x, 0, h, h), new Vector2(0.5f, 0.5f)));

                x += h;
            }
            emoteSprites[id] = sprites;
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
