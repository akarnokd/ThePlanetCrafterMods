using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game periodically runs StoryEventsHandler::TryToLaunchAnEventLogic
        /// to trigger a story event (a message or a meteor strike as of now).
        /// 
        /// On the host, we let it happen as the creation of message or meteor event
        /// is handled elsewhere.
        /// 
        /// On the client, we don't do any story events.
        /// </summary>
        /// <returns>False for the client, true otherwise</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StoryEventsHandler), "TryToLaunchAnEventLogic")]
        static bool StoryEventsHandler_TryToLaunchAnEventLogic()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        /// <summary>
        /// The vanilla game calls MessagesHandler::AddNewReceivedMessage to give the
        /// player a new message and ping it for them.
        /// 
        /// On the host, we let this happen and send the message to the client.
        /// 
        /// On the client, we let the original method run.
        /// </summary>
        /// <param name="_messageData">The new message to be received.</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MessagesHandler), nameof(MessagesHandler.AddNewReceivedMessage))]
        static void MessagesHandler_AddNewReceivedMessage(MessageData _messageData, bool _showPopup)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendAllClients(new MessageMessageAdd()
                {
                    messageId = _messageData.stringId,
                    showPopup = _showPopup
                }, true);
            }
        }

        static void SendMessages()
        {
            List<Message> receivedMessages = Managers.GetManager<MessagesHandler>().GetReceivedMessages();

            var mm = new MessageMessages();

            foreach (Message m in receivedMessages)
            {
                mm.messages.Add(m.GetMessageData().stringId);
            }
            SendAllClients(mm);
        }

        static void SendStoryEvents()
        {
            // TODO maybe if there will be other event types in the future (message, meteor).
            var seh = Managers.GetManager<StoryEventsHandler>();

            var mse = new MessageStoryEvents();
            mse.eventIds = new();

            foreach (var se in seh.GetTriggeredStoryEvents())
            {
                mse.eventIds.Add(se.storyEventData.id);
            }
            SendAllClients(mse);
        }

        static void ReceiveMessageMessages(MessageMessages mm)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }

            var mh = Managers.GetManager<MessagesHandler>();
            var list = mh.GetReceivedMessages();
            list.Clear();

            foreach (var mid in mm.messages)
            {
                var md = mh.GetMessageDataViaId(mid);
                if (md != null)
                {
                    LogInfo("ReceiveMessageMessages: " + mid);
                    list.Add(new Message(md, false));
                }
                else
                {
                    LogWarning("ReceiveMessageMessages: Unknown message " + mid);
                }
            }
        }

        static void ReceiveMessageMessageAdd(MessageMessageAdd mma)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }
            var mh = Managers.GetManager<MessagesHandler>();

            var md = mh.GetMessageDataViaId(mma.messageId);
            if (md != null)
            {
                LogInfo("ReceiveMessageMessages: " + mma.messageId);
                mh.AddNewReceivedMessage(md);
            }
            else
            {
                LogWarning("ReceiveMessageMessageAdd: Unknown message " + mma.messageId);
            }
        }

        static void ReceiveMessageStoryEvents(MessageStoryEvents mse)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }
            var seh = Managers.GetManager<StoryEventsHandler>();

            List<StoryEvent> list = new();
            foreach (var id in mse.eventIds)
            {
                var sed = seh.GetStoryEventDataViaId(id);
                list.Add(new StoryEvent(sed));
            }

            seh.SetTriggeredEvents(list);
        }
    }
}
