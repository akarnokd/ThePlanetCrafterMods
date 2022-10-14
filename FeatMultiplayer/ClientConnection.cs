using SpaceCraft;
using System.Collections.Concurrent;
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

        internal string clientName;

        internal Inventory shadowBackpack;
        internal int shadowBackpackWorldObjectId;
        internal Inventory shadowEquipment;
        internal int shadowEquipmentWorldObjectId;

        internal readonly ConcurrentQueue<object> _sendQueue = new ConcurrentQueue<object>();
        internal readonly AutoResetEvent _sendQueueBlock = new AutoResetEvent(false);

        public ClientConnection(int id)
        {
            this.id = id;
        }

        internal void Send(object message)
        {
            _sendQueue.Enqueue(message);
        }
        internal void Signal()
        {
            _sendQueueBlock.Set();
        }
    }
}
