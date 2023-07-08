using BepInEx;
using FeatMultiplayer.MessageTypes;
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
                    if (f(clientId, str, _receiveQueue))
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

        static void NetworkParseMessage(string message, ClientConnection cc)
        {
            if (MessagePlayerPosition.TryParse(message, out var mpp))
            {
                Receive(cc, mpp);
            }
            else
            if (MessageLogin.TryParse(message, out var ml))
            {
                LogInfo("Login attempt: " + ml.user);
                Receive(cc, ml);
            }
            else
            if (MessagePlayerWelcome.TryParse(message, out var mw) && updateMode == MultiplayerMode.CoopClient)
            {
                Receive(cc, mw);
            }
            else
            if (MessageAllObjects.TryParse(message, out var mc))
            {
                //LogInfo(message);
                Receive(cc, mc);
            }
            else
            if (MessageMined.TryParse(message, out var mm1))
            {
                Receive(cc, mm1);
            }
            else
            if (MessageInventoryAdded.TryParse(message, out var mim))
            {
                Receive(cc, mim);
            }
            else
            if (MessageInventoryRemoved.TryParse(message, out var mir))
            {
                Receive(cc, mir);
            }
            else
            if (MessageInventories.TryParse(message, out var minv))
            {
                Receive(cc, minv);
            }
            else
            if (MessagePlaceConstructible.TryParse(message, out var mpc))
            {
                Receive(cc, mpc);
            }
            else
            if (MessageUpdateWorldObject.TryParse(message, out var mc1))
            {
                Receive(cc, mc1);
            }
            else
            if (MessagePanelChanged.TryParse(message, out var mpc1))
            {
                Receive(cc, mpc1);
            }
            else
            if (MessageSetTransform.TryParse(message, out var mst))
            {
                Receive(cc, mst);
            }
            else
            if (MessageDropWorldObject.TryParse(message, out var mdwo))
            {
                Receive(cc, mdwo);
            }
            else
            if (MessageUnlocks.TryParse(message, out var mu))
            {
                Receive(cc, mu);
            }
            else
            if (MessageTerraformState.TryParse(message, out var mts))
            {
                Receive(cc, mts);
            }
            else
            if (MessageUpdateText.TryParse(message, out var mut1))
            {
                Receive(cc, mut1);
            }
            else
            if (MessageUpdateColor.TryParse(message, out var muc1))
            {
                Receive(cc, muc1);
            }
            else
            if (MessageMicrochipUnlock.TryParse(message, out var mmu))
            {
                Receive(cc, mmu);
            }
            else
            if (MessageSortInventory.TryParse(message, out var msi))
            {
                Receive(cc, msi);
            }
            else
            if (MessageGrab.TryParse(message, out var mg))
            {
                Receive(cc, mg);
            }
            else
            if (MessageCraft.TryParse(message, out var mc2))
            {
                Receive(cc, mc2);
            }
            else
            if (MessageCraftWorld.TryParse(message, out var mcw))
            {
                Receive(cc, mcw);
            }
            else
            if (MessageUpdateGrowth.TryParse(message, out var mug))
            {
                Receive(cc, mug);
            }
            else
            if (MessageInventorySpawn.TryParse(message, out var mis))
            {
                Receive(cc, mis);
            }
            else
            if (MessageInventorySize.TryParse(message, out var mis2))
            {
                Receive(cc, mis2);
            }
            else
            if (MessageLaunch.TryParse(message, out var ml2)) 
            {
                Receive(cc, ml2);
            }
            else
            if (MessageAsteroidSpawn.TryParse(message, out var mas))
            {
                Receive(cc, mas);
            }
            else
            if (MessageGrowAdd.TryParse(message, out var mga))
            {
                Receive(cc, mga);
            }
            else
            if (MessageGrowRemove.TryParse(message, out var mgr))
            {
                Receive(cc, mgr);
            }
            else
            if (MessageMeteorEvent.TryParse(message, out var mme))
            {
                Receive(cc, mme);
            }
            else
            if (MessageTime.TryParse(message, out var mt))
            {
                Receive(cc, mt);
            }
            else
            if (MessageDeconstruct.TryParse(message, out var md))
            {
                Receive(cc, md);
            }
            else
            if (MessageGameMode.TryParse(message, out var mgm))
            {
                Receive(cc, mgm);
            }
            else
            if (MessageDeath.TryParse(message, out var mdt))
            {
                Receive(cc, mdt);
            }
            else
            if (MessageMessages.TryParse(message, out var mm))
            {
                Receive(cc, mm);
            }
            else
            if (MessageMessageAdd.TryParse(message, out var mma))
            {
                Receive(cc, mma);
            }
            else
            if (MessageTerrainLayers.TryParse(message, out var mtl))
            {
                Receive(cc, mtl);
            }
            else
            if (MessageGeneticsAction.TryParse(message, out var mga1))
            {
                Receive(cc, mga1);
            } 
            else
            if (MessageSetLinkedGroups.TryParse(message, out var mslg))
            {
                Receive(cc, mslg);
            }
            else
            if (MessageMovePlayer.TryParse(message, out var mmp))
            {
                Receive(cc, mmp);
            }
            else
            if (MessagePlayerJoined.TryParse(message, out var mpj))
            {
                Receive(cc, mpj);
            }
            else
            if (MessagePlayerLeft.TryParse(message, out var mpl))
            {
                Receive(cc, mpl);
            }
            else
            if (MessagePlayerColor.TryParse(message, out var mpc2))
            {
                Receive(cc, mpc2);
            }
            else
            if (MessageUpdateStorage.TryParse(message, out var mus))
            {
                Receive(cc, mus);
            }
            else
            if (MessageUpdateAllStorage.TryParse(message, out var muas))
            {
                Receive(cc, muas);
            }
            else
            if (MessageEmote.TryParse(message, out var mee))
            {
                Receive(cc, mee);
            }
            else
            if (MessageStoryEvents.TryParse(message, out var mse))
            {
                Receive(cc, mse);
            }
            else
            if (MessageUpdateSupplyDemand.TryParse(message, out var musd))
            {
                Receive(cc, musd);
            }
            else
            if (MessageDronePosition.TryParse(message, out var mdp))
            {
                Receive(cc, mdp);
            }
            else
            if (MessageDroneStats.TryParse(message, out var mds))
            {
                Receive(cc, mds);
            }
            else
            if (MessageLaunchTrade.TryParse(message, out var mlt))
            {
                Receive(cc, mlt);
            }
            else
            if (MessageConsume.TryParse(message, out var mcs))
            {
                Receive(cc, mcs);
            }
            else
            if (MessageDeconstructPanel.TryParse(message, out var mdp1))
            {
                Receive(cc, mdp1);
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
            if (!TryMessageParsers(cc.id, message))
            {
                LogInfo("ParseMessage: Unknown message?\r\n" + message);
            }
            // TODO other messages
        }

        void UIDispatchMessage(object message)
        {
            if (message is NotifyUserMessage num1)
            {
                NotifyUser(num1.message, num1.duration);
                return;
            }
            if (message is MessageClientDisconnected mcd)
            {
                ReceiveMessageClientDisconnected(mcd);
                return;
            }
            if (updateMode == MultiplayerMode.CoopHost 
                && message is MessageBase mb 
                && mb.sender.clientName == null)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    if (message is MessageLogin ml)
                    {
                        ReceiveLogin(ml);
                    }
                    else
                    {
                        LogWarning("MessageLogin not yet received: " + message);
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
                case MessagePlayerJoined mpj:
                    {
                        ReceiveMessagePlayerJoined(mpj);
                        break;
                    }
                case MessagePlayerLeft mpl:
                    {
                        ReceiveMessagePlayerLeft(mpl);
                        break;
                    }
                case MessagePlayerWelcome mpw:
                    {
                        ReceiveWelcome(mpw);
                        break;
                    }
                case MessagePlayerColor mpc1:
                    {
                        ReceiveMessagePlayerColor(mpc1);
                        break;
                    }
                case MessageDisconnected md:
                    {
                        ReceiveMessageHostDisconnected(md);
                        break;
                    }
                case MessageUpdateStorage mus:
                    {
                        ReceiveMessageUpdateStorage(mus);
                        break;
                    }
                case MessageUpdateAllStorage muas:
                    {
                        ReceiveMessageUpdateAllStorage(muas);
                        break;
                    }
                case MessageEmote mee:
                    {
                        ReceiveMessageEmote(mee);
                        break;
                    }
                case MessageStoryEvents mse:
                    {
                        ReceiveMessageStoryEvents(mse);
                        break;
                    }
                case MessageDronePosition mdp:
                    {
                        ReceiveMessageDronePosition(mdp);
                        break;
                    }
                case MessageUpdateSupplyDemand musd:
                    {
                        ReceiveMessageUpdateSupplyDemand(musd);
                        break;
                    }
                case MessageDroneStats mds:
                    {
                        ReceiveMessageDroneStats(mds);
                        break;
                    }
                case MessageLaunchTrade mlt:
                    {
                        ReceiveMessageLaunchTrade(mlt);
                        break;
                    }
                case MessageConsume mcs:
                    {
                        ReceiveMessageConsume(mcs);
                        break;
                    }
                case MessageDeconstructPanel mdp1:
                    {
                        ReceiveMessageDeconstructPanel(mdp1);
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
