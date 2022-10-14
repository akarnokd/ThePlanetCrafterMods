using BepInEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {

        /// <summary>
        /// List of functions to call with a non-standard message to be parsed and submitted to the UI.
        /// </summary>
        static readonly List<Func<int, string, ConcurrentQueue<object>, bool>> messageParsers = new();

        /// <summary>
        /// List of functions to handle the message object on the UI thread.
        /// </summary>
        static readonly List<Func<object, bool>> messageReceivers = new();

        static bool TryMessageParsers(int clientId, string str)
        {
            foreach (var f in messageParsers)
            {
                try
                {
                    if (f(clientId, str, receiveQueue))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
            return false;
        }

        static bool TryMessageReceivers(object o)
        {
            foreach (var f in messageReceivers)
            {
                try
                {
                    if (f(o))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
            return false;
        }

        static void DispatchMessageString(string s)
        {
            if (s == "Welcome")
            {
                ReceiveWelcome();
            }
            else if (s == "Disconnected")
            {
                ReceiveDisconnected();
            }
        }

        static void NetworkParseMessage(string message)
        {
            if (MessagePlayerPosition.TryParse(message, out var mpp))
            {
                receiveQueue.Enqueue(mpp);
            }
            else
            if (MessageLogin.TryParse(message, out var ml))
            {
                LogInfo("Login attempt: " + ml.user);
                receiveQueue.Enqueue(ml);
            }
            else
            if (MessageAllObjects.TryParse(message, out var mc))
            {
                //LogInfo(message);
                receiveQueue.Enqueue(mc);
            }
            else
            if (MessageMined.TryParse(message, out var mm1))
            {
                receiveQueue.Enqueue(mm1);
            }
            else
            if (MessageInventoryAdded.TryParse(message, out var mim))
            {
                receiveQueue.Enqueue(mim);
            }
            else
            if (MessageInventoryRemoved.TryParse(message, out var mir))
            {
                receiveQueue.Enqueue(mir);
            }
            else
            if (MessageInventories.TryParse(message, out var minv))
            {
                receiveQueue.Enqueue(minv);
            }
            else
            if (MessagePlaceConstructible.TryParse(message, out var mpc))
            {
                receiveQueue.Enqueue(mpc);
            }
            else
            if (MessageUpdateWorldObject.TryParse(message, out var mc1))
            {
                receiveQueue.Enqueue(mc1);
            }
            else
            if (MessagePanelChanged.TryParse(message, out var mpc1))
            {
                receiveQueue.Enqueue(mpc1);
            }
            else
            if (MessageSetTransform.TryParse(message, out var mst))
            {
                receiveQueue.Enqueue(mst);
            }
            else
            if (MessageDropWorldObject.TryParse(message, out var mdwo))
            {
                receiveQueue.Enqueue(mdwo);
            }
            else
            if (MessageUnlocks.TryParse(message, out var mu))
            {
                receiveQueue.Enqueue(mu);
            }
            else
            if (MessageTerraformState.TryParse(message, out var mts))
            {
                receiveQueue.Enqueue(mts);
            }
            else
            if (MessageUpdateText.TryParse(message, out var mut1))
            {
                receiveQueue.Enqueue(mut1);
            }
            else
            if (MessageUpdateColor.TryParse(message, out var muc1))
            {
                receiveQueue.Enqueue(muc1);
            }
            else
            if (MessageMicrochipUnlock.TryParse(message, out var mmu))
            {
                receiveQueue.Enqueue(mmu);
            }
            else
            if (MessageSortInventory.TryParse(message, out var msi))
            {
                receiveQueue.Enqueue(msi);
            }
            else
            if (MessageGrab.TryParse(message, out var mg))
            {
                receiveQueue.Enqueue(mg);
            }
            else
            if (MessageCraft.TryParse(message, out var mc2))
            {
                receiveQueue.Enqueue(mc2);
            }
            else
            if (MessageCraftWorld.TryParse(message, out var mcw))
            {
                receiveQueue.Enqueue(mcw);
            }
            else
            if (MessageUpdateGrowth.TryParse(message, out var mug))
            {
                receiveQueue.Enqueue(mug);
            }
            else
            if (MessageInventorySpawn.TryParse(message, out var mis))
            {
                receiveQueue.Enqueue(mis);
            }
            else
            if (MessageInventorySize.TryParse(message, out var mis2))
            {
                receiveQueue.Enqueue(mis2);
            }
            else
            if (MessageLaunch.TryParse(message, out var ml2)) 
            {
                receiveQueue.Enqueue(ml2);
            }
            else
            if (MessageAsteroidSpawn.TryParse(message, out var mas))
            {
                receiveQueue.Enqueue(mas);
            }
            else
            if (MessageGrowAdd.TryParse(message, out var mga))
            {
                receiveQueue.Enqueue(mga);
            }
            else
            if (MessageGrowRemove.TryParse(message, out var mgr))
            {
                receiveQueue.Enqueue(mgr);
            }
            else
            if (MessageMeteorEvent.TryParse(message, out var mme))
            {
                receiveQueue.Enqueue(mme);
            }
            else
            if (MessageTime.TryParse(message, out var mt))
            {
                receiveQueue.Enqueue(mt);
            }
            else
            if (MessageDeconstruct.TryParse(message, out var md))
            {
                receiveQueue.Enqueue(md);
            }
            else
            if (MessageGameMode.TryParse(message, out var mgm))
            {
                receiveQueue.Enqueue(mgm);
            }
            else
            if (MessageDeath.TryParse(message, out var mdt))
            {
                receiveQueue.Enqueue(mdt);
            }
            else
            if (MessageMessages.TryParse(message, out var mm))
            {
                receiveQueue.Enqueue(mm);
            }
            else
            if (MessageMessageAdd.TryParse(message, out var mma))
            {
                receiveQueue.Enqueue(mma);
            }
            else
            if (MessageTerrainLayers.TryParse(message, out var mtl))
            {
                receiveQueue.Enqueue(mtl);
            }
            else
            if (MessageGeneticsAction.TryParse(message, out var mga1))
            {
                receiveQueue.Enqueue(mga1);
            } 
            else
            if (MessageSetLinkedGroups.TryParse(message, out var mslg))
            {
                receiveQueue.Enqueue(mslg);
            }
            else
            if (MessageMovePlayer.TryParse(message, out var mmp))
            {
                receiveQueue.Enqueue(mmp);
            }
            else
            if (message == "ENoClientSlot" && updateMode == MultiplayerMode.CoopClient)
            {
                NotifyUserFromBackground("Host full");
            }
            else
            if (message == "EAccessDenied" && updateMode == MultiplayerMode.CoopClient)
            {
                NotifyUserFromBackground("Host access denied (check user and password settings)");
            }
            else
            if (message == "Welcome" && updateMode == MultiplayerMode.CoopClient)
            {
                receiveQueue.Enqueue("Welcome");
            }
            else
            if (!TryMessageParsers(1, message)) // FIXME use the proper client id once manyplayer is supported
            {
                LogInfo("ParseMessage: Unknown message?\r\n" + message);
            }
            // TODO other messages
        }

        void UIDispatchMessage(object message)
        {
            if (otherPlayer == null)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (message is NotifyUserMessage num)
                    {
                        NotifyUser(num.message, num.duration);
                    }
                    else
                    if (message is MessageLogin ml)
                    {
                        ReceiveLogin(ml);
                    }
                    else
                    {
                        LogWarning("MessageLogin not yet received: " + message);
                    }
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    if (message is NotifyUserMessage num)
                    {
                        NotifyUser(num.message, num.duration);
                    } 
                    else
                    if (message is string s)
                    {
                        DispatchMessageString(s);
                    }
                    else
                    {
                        LogWarning("Welcome not yet received: " + message);
                    }
                }
                return;
            }
            switch (message)
            {
                case NotifyUserMessage num:
                    {
                        NotifyUser(num.message, num.duration);
                        break;
                    }
                case MessagePlayerPosition mpp:
                    {
                        ReceivePlayerLocation(mpp);
                        break;
                    }
                case MessageLogin ml:
                    {
                        ReceiveLogin(ml);
                        break;
                    }
                case MessageAllObjects mc:
                    {
                        ReceiveMessageAllObjects(mc);
                        break;
                    }
                case MessageMined mm1:
                    {
                        ReceiveMessageMined(mm1);
                        break;
                    }
                case MessageInventoryAdded mia:
                    {
                        ReceiveMessageInventoryAdded(mia);
                        break;
                    }
                case MessageInventoryRemoved mir:
                    {
                        ReceiveMessageInventoryRemoved(mir);
                        break;
                    }
                case MessageInventories minv:
                    {
                        ReceiveMessageInventories(minv);
                        break;
                    }
                case MessagePlaceConstructible mpc:
                    {
                        ReceiveMessagePlaceConstructible(mpc);
                        break;
                    }
                case MessageUpdateWorldObject mc1:
                    {
                        ReceiveMessageUpdateWorldObject(mc1);
                        break;
                    }
                case MessagePanelChanged mpc1:
                    {
                        ReceiveMessagePanelChanged(mpc1);
                        break;
                    }
                case MessageSetTransform mst:
                    {
                        ReceiveMessageSetTransform(mst);
                        break;
                    }
                case MessageDropWorldObject mdwo:
                    {
                        ReceiveMessageDropWorldObject(mdwo);
                        break;
                    }
                case MessageUnlocks mu:
                    {
                        ReceiveMessageUnlocks(mu);
                        break;
                    }
                case MessageTerraformState mts:
                    {
                        ReceiveTerraformState(mts);
                        break;
                    }
                case MessageUpdateText mut1:
                    {
                        ReceiveMessageUpdateText(mut1);
                        break;
                    }
                case MessageUpdateColor muc:
                    {
                        ReceiveMessageUpdateColor(muc);
                        break;
                    }
                case MessageMicrochipUnlock mmu:
                    {
                        ReceiveMessageMicrochipUnlock(mmu);
                        break;
                    }
                case MessageSortInventory msi:
                    {
                        ReceiveMessageSortInventory(msi);
                        break;
                    }
                case MessageGrab mg:
                    {
                        ReceiveMessageGrab(mg);
                        break;
                    }
                case MessageCraft mc2:
                    {
                        ReceiveMessageCraft(mc2);
                        break;
                    }
                case MessageCraftWorld mcw:
                    {
                        ReceiveMessageCraftWorld(mcw);
                        break;
                    }
                case MessageUpdateGrowth mug:
                    {
                        ReceiveMessageUpdateGrowth(mug);
                        break;
                    }
                case MessageInventorySpawn mis:
                    {
                        ReceiveMessageInventorySpawn(mis);
                        break;
                    }
                case MessageInventorySize mis2:
                    {
                        ReceiveMessageInventorySize(mis2);
                        break;
                    }
                case MessageLaunch ml:
                    {
                        ReceiveMessageLaunch(ml);
                        break;
                    }
                case MessageAsteroidSpawn mas:
                    {
                        ReceiveMessageAsteroidSpawn(mas);
                        break;
                    }
                case MessageGrowAdd mga:
                    {
                        ReceiveMessageGrowAdd(mga);
                        break;
                    }
                case MessageGrowRemove mgr:
                    {
                        ReceiveMessageGrowRemove(mgr);
                        break;
                    }
                case MessageMeteorEvent mme:
                    {
                        ReceiveMessageMeteorEvent(mme);
                        break;
                    }
                case MessageTime mt:
                    {
                        ReceiveMessageTime(mt);
                        break;
                    }
                case MessageDeconstruct md:
                    {
                        ReceiveMessageDeconstruct(md);
                        break;
                    }
                case MessageGameMode mgm:
                    {
                        ReceiveMessageGameMode(mgm);
                        break;
                    }
                case MessageDeath mdt:
                    {
                        ReceiveMessageDeath(mdt);
                        break;
                    }
                case MessageMessages mm:
                    {
                        ReceiveMessageMessages(mm);
                        break;
                    }
                case MessageMessageAdd mma:
                    {
                        ReceiveMessageMessageAdd(mma);
                        break;
                    }
                case MessageTerrainLayers mtl:
                    {
                        ReceiveMessageTerrainLayers(mtl);
                        break;
                    }
                case MessageGeneticsAction mga1:
                    {
                        ReceiveMessageGeneticsAction(mga1);
                        break;
                    }
                case MessageSetLinkedGroups mslg:
                    {
                        ReceiveMessageSetLinkedGroups(mslg);
                        break;
                    }
                case MessageMovePlayer mmp:
                    {
                        ReceiveMessageMovePlayer(mmp);
                        break;
                    }
                case string s:
                    {
                        DispatchMessageString(s);
                        break;
                    }
                default:
                    {
                        if (!TryMessageReceivers(message))
                        {
                            LogWarning("DispatchMessage: Unsupported message " + message);
                        }
                        break;
                    }
                    // TODO dispatch on message type
            }
        }
    }
}
