using SpaceCraft;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace FeatMultiplayer
{
    /// <summary>
    /// Represents the dedicated sender queue towards a specific client.
    /// 
    /// The receiver queue is shared.
    /// </summary>
    public class ClientConnection
    {
        internal readonly int id;

        internal TcpClient tcpClient;

        /// <summary>
        /// Contains the client's sanitized name if the login was successful.
        /// Null until then.
        /// </summary>
        internal volatile string clientName;

        internal Inventory shadowBackpack;
        internal int shadowBackpackWorldObjectId;
        internal Inventory shadowEquipment;
        internal int shadowEquipmentWorldObjectId;

        internal readonly Dictionary<string, string> storage = new();

        internal readonly ConcurrentQueue<object> _sendQueue = new ConcurrentQueue<object>();
        internal readonly AutoResetEvent _sendQueueBlock = new AutoResetEvent(false);

        public ClientConnection(int id)
        {
            this.id = id;
        }

        internal void Send(object message)
        {
            if (clientName != null)
            {
                _sendQueue.Enqueue(message);
            }
        }
        internal void Signal()
        {
            _sendQueueBlock.Set();
        }

        internal string GetData(string key)
        {
            if (storage.TryGetValue(key, out var v))
            {
                return v;
            }
            return null;
        }

        internal void SetData(string key, string value)
        {
            storage[key] = value;
        }
    }
}
