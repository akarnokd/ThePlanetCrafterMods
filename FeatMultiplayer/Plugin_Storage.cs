using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FeatMultiplayer.MessageTypes;

[assembly: InternalsVisibleTo("XTestPlugins")]

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static readonly List<Action> storageOnClientDataReady = new List<Action>();

        static void StorageRestoreClient(ClientConnection cc)
        {
            LogInfo("Restoring storage for client " + cc.clientName);
            var wo = WorldObjectsHandler.GetWorldObjectViaId(cc.shadowEquipmentWorldObjectId);
            cc.storage.Clear();
            StorageHelper.StorageDecodeData(wo.GetText(), cc.storage);
            LogInfo("Restoring storage for client " + cc.clientName + " - Done");
        }

        static void StorageSaveClient(ClientConnection cc)
        {
            LogInfo("Persisting storage for client " + cc.clientName);
            var wo = WorldObjectsHandler.GetWorldObjectViaId(cc.shadowEquipmentWorldObjectId);
            wo.SetText(StorageHelper.StorageEncodeData(cc.storage));
            LogInfo("Persisting storage for client " + cc.clientName + " - Done");
        }

        static void StorageNotifyClient(ClientConnection cc)
        {
            LogInfo("Sending storage to client " + cc.clientName);
            var msg = new MessageUpdateAllStorage();

            foreach (var kv in cc.storage)
            {
                LogInfo("    " + kv.Key + " = " + kv.Value);
                msg.storage.Add(kv.Key, kv.Value);
            }

            cc.Send(msg);
            cc.Signal();
            LogInfo("  Done");
        }

        static void SetAndSendDataToHost(string key, string value)
        {
            var h = _towardsHost;
            if (h != null)
            {
                LogInfo("UpdateStorage: " + key + " = " + value);
                h.storage[key] = value;
                h.Send(new MessageUpdateStorage
                {
                    key = key,
                    value = value
                });
                h.Signal();
            }
            else
            {
                ThrowMissingHostConnection();                
            }
        }

        static void ThrowMissingHostConnection()
        {
            throw new InvalidOperationException("No connection to host (yet). Please register for data ready notification via " + nameof(Plugin.apiClientRegisterDataReady));
        }

        static void ReceiveMessageUpdateStorage(MessageUpdateStorage mus)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                mus.sender.SetData(mus.key, mus.value);
                StorageSaveClient(mus.sender);
            }
        }

        static void ReceiveMessageUpdateAllStorage(MessageUpdateAllStorage muas)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("ReceiveMessageUpdateAllStorage - Begin");
                var h = _towardsHost;
                h.storage.Clear();
                foreach (var kv in muas.storage)
                {
                    LogInfo("    " + kv.Key + " = " + kv.Value);
                    h.storage.Add(kv.Key, kv.Value);
                }

                LogInfo("ReceiveMessageUpdateAllStorage - Notify listeners (" + storageOnClientDataReady.Count + ")");
                // notify about the storage info being available now
                foreach (var a in storageOnClientDataReady)
                {
                    try
                    {
                        a();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                }

                LogInfo("ReceiveMessageUpdateAllStorage - Done");
            }
        }
    }

    /// <summary>
    /// Encode and decode strings and dictionaries by considering
    /// restricted characters in keys and values with respect to
    /// save file format and network message format.
    /// </summary>
    internal class StorageHelper
    {
        internal static string StorageEncodeData(Dictionary<string, string> storage)
        {
            StringBuilder sb = new();

            foreach (var kv in storage)
            {
                if (sb.Length != 0)
                {
                    sb.Append(',');
                }
                sb.Append(StorageEncodeString(kv.Key));
                sb.Append(',');
                sb.Append(StorageEncodeString(kv.Value));
            }

            return sb.ToString();
        }

        internal static void StorageDecodeData(string data, Dictionary<string, string> storage)
        {
            if (data == null || data.Length == 0 || data == "...")
            {
                return;
            }

            var parts = data.Split(',');
            for (int i = 0; i < parts.Length; i += 2)
            {
                string key = StorageDecodeString(parts[i]);
                string value = StorageDecodeString(parts[i + 1]);

                storage.Add(key, value);
            }
        }

        /// <summary>
        /// Replaces pipes (|), at signs (@), newlines (\n), returns (\r),
        /// backslashes (\), equal signs (=) and commas (,) with an escape indicator because
        /// in saves and messages, these are restricted characters.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static string StorageEncodeString(string s)
        {
            StringBuilder sb = new();
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '|')
                {
                    sb.Append('\\');
                    sb.Append('p');
                }
                else if (c == '@')
                {
                    sb.Append('\\');
                    sb.Append('a');
                }
                else if (c == '\n')
                {
                    sb.Append('\\');
                    sb.Append('n');
                }
                else if (c == '\r')
                {
                    sb.Append('\\');
                    sb.Append('r');
                }
                else if (c == '\\')
                {
                    sb.Append('\\');
                    sb.Append('b');
                }
                else if (c == ',')
                {
                    sb.Append('\\');
                    sb.Append('c');
                }
                else if (c == '=')
                {
                    sb.Append('\\');
                    sb.Append('e');
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Replaces \p with pipe (|), \a with @, \n with newline, \r with return,
        /// \b with backslash, \c with comma.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        internal static string StorageDecodeString(string s)
        {
            StringBuilder sb = new();

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\')
                {
                    if (i < s.Length - 1)
                    {
                        c = s[++i];
                        if (c == 'p')
                        {
                            sb.Append('|');
                        }
                        else if (c == 'a')
                        {
                            sb.Append('@');
                        }
                        else if (c == 'n')
                        {
                            sb.Append('\n');
                        }
                        else if (c == 'r')
                        {
                            sb.Append('\r');
                        }
                        else if (c == 'b')
                        {
                            sb.Append('\\');
                        }
                        else if (c == 'c')
                        {
                            sb.Append(',');
                        }
                        else if (c == 'e')
                        {
                            sb.Append('=');
                        }
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
