using BepInEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static readonly byte[] ENoClientSlotBytes = Encoding.UTF8.GetBytes("ENoClientSlot\n");
        static readonly byte[] EAccessDenied = Encoding.UTF8.GetBytes("EAccessDenied\n");

        static readonly ConcurrentQueue<object> receiveQueue = new();

        static CancellationTokenSource stopNetwork;
        static volatile bool clientConnected;
        static volatile bool networkConnected;

        static int _uniqueClientId;
        static readonly ConcurrentDictionary<int, ClientConnection> _clientConnections = new();

        static volatile ClientConnection _towardsHost;

        static void SendHost(object obj)
        {
            _towardsHost?.Send(obj);
        }

        static void SendClient(int clientId, object obj)
        {
            if (_clientConnections.TryGetValue(clientId, out var cc))
            {
                cc.Send(obj);
            }
            else
            {
                LogWarning("Unknown client or client already disconnected: " + clientId);
            }
        }

        static void SendAllClients(object obj)
        {
            foreach (var kv in _clientConnections)
            {
                kv.Value.Send(obj);
            }
        }

        /// <summary>
        /// Send a message to all clients, except one client.
        /// Typically used when dispatching a message from one
        /// client to the rest of the clients on the host.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="obj"></param>
        static void SendAllClientsExcept(int clientId, object obj)
        {
            foreach (var kv in _clientConnections)
            {
                if (kv.Key != clientId)
                {
                    kv.Value.Send(obj);
                }
            }
        }

        static void SignalHost()
        {
            _towardsHost?.Signal();
        }

        static void SignalClient(int clientId)
        {
            if (_clientConnections.TryGetValue(clientId, out var cc))
            {
                cc.Signal();
            }
            else
            {
                LogWarning("Unknown client or client already disconnected: " + clientId);
            }
        }

        static void SignalAllClients()
        {
            foreach (var kv in _clientConnections)
            {
                kv.Value.Signal();
            }
        }

        static void SignalAllClientsExcept(int clientId)
        {
            foreach (var kv in _clientConnections)
            {
                if (kv.Key != clientId)
                {
                    kv.Value.Signal();
                }
            }
        }


        static void StartAsHost()
        {
            stopNetwork = new CancellationTokenSource();
            Task.Factory.StartNew(HostAcceptor, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        static void StartAsClient()
        {
            NotifyUser("Connecting to Host...");
            stopNetwork = new CancellationTokenSource();
            Task.Run(() =>
            {
                LogInfo("Client connecting to " + hostAddress.Value + ":" + port.Value);
                try
                {
                    TcpClient client = new TcpClient();
                    client.Connect(hostAddress.Value, port.Value);
                    LogInfo("Client connection success");
                    NotifyUserFromBackground("Connecting to Host...Success");
                    networkConnected = true;
                    stopNetwork.Token.Register(() =>
                    {
                        networkConnected = false;
                        client.Close();
                    });

                    var cc = new ClientConnection(0);
                    _towardsHost = cc;
                    cc.tcpClient = client;

                    Task.Factory.StartNew(SenderLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                    cc.Send(new MessageLogin
                    {
                        user = clientJoinName,
                        password = clientJoinPassword
                    });
                    cc.Signal();
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    NotifyUserFromBackground("Error: could not connect to Host");
                }
            });
        }

        static void HostAcceptor()
        {
            var hostIp = hostServiceAddress.Value;
            IPAddress hostIPAddress = IPAddress.Any;
            if (hostIp == "default")
            {
                hostIPAddress = GetMainIPv4();
            }
            else
            if (hostIp == "defaultv6")
            {
                hostIPAddress = GetMainIPv6();
            }
            else
            {
                hostIPAddress = IPAddress.Parse(hostIp);
            }
            LogInfo("Starting HostAcceptor on " + hostIp + ":" + port.Value + " (" + hostIPAddress + ")");
            try
            {
                TcpListener listener = new TcpListener(hostIPAddress, port.Value);
                listener.Start();
                stopNetwork.Token.Register(() =>
                {
                    networkConnected = false;
                    listener.Stop();
                });
                try
                {
                    while (!stopNetwork.IsCancellationRequested)
                    {
                        var client = listener.AcceptTcpClient();
                        ManageClient(client);
                    }
                }
                finally
                {
                    listener.Stop();
                    LogInfo("Stopping HostAcceptor on port " + port.Value);
                    clientConnected = false;
                }
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested)
                {
                    LogError(ex);
                }
            }
        }

        static void ManageClient(TcpClient client)
        {
            if (clientConnected)
            {
                LogInfo("A client already connected");
                try
                {
                    try
                    {
                        var stream = client.GetStream();
                        try
                        {
                            stream.Write(ENoClientSlotBytes);
                            stream.Flush();
                        }
                        finally
                        {
                            stream.Close();
                        }
                    }
                    finally
                    {
                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (!stopNetwork.IsCancellationRequested)
                    {
                        LogError(ex);
                    }
                }
            }
            else
            {
                LogInfo("New Client from " + client.Client.RemoteEndPoint);
                clientConnected = true;

                var cc = CreateNewClient();
                cc.tcpClient = client;
                Task.Factory.StartNew(SenderLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(ReceiveLoop, client, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        static void SenderLoop(object clientObj)
        {
            LogInfo("SenderLoop begin");
            var clientConnection = (ClientConnection)clientObj;
            var client = clientConnection.tcpClient;
            var _sendQueue = clientConnection._sendQueue;
            var _sendQueueBlock = clientConnection._sendQueueBlock;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    try
                    {
                        LogInfo("SenderLoop loop");
                        while (!stopNetwork.IsCancellationRequested)
                        {
                            if (_sendQueue.TryDequeue(out var message))
                            {
                                switch (message)
                                {
                                    case string s:
                                        {
                                            stream.Write(s);
                                            stream.Flush();
                                            break;
                                        }
                                    case byte[] b:
                                        {
                                            stream.Write(b);
                                            stream.Flush();
                                            break;
                                        }
                                    case MessageStringProvider msp:
                                        {
                                            stream.Write(msp.GetString());
                                            stream.Flush();
                                            break;
                                        }
                                    case MessageBytesProvider msp:
                                        {
                                            stream.Write(msp.GetBytes());
                                            stream.Flush();
                                            break;
                                        }
                                    default:
                                        {
                                            LogWarning("Can't serialize message: " + message);
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                _sendQueueBlock.WaitOne(1000);
                            }
                        }
                    }
                    finally
                    {
                        stream.Close();
                    }
                }
                finally
                {
                    client.Close();
                }
                LogInfo("SenderLoop stop");
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested && !(ex is ObjectDisposedException))
                {
                    LogError(ex);
                }
            }
            receiveQueue.Enqueue("Disconnected");
            networkConnected = false;
            if (clientConnection == _towardsHost)
            {
                _towardsHost = null;
            }
            else
            {
                RemoveClient(clientConnection);
            }
        }

        static void ReceiveLoop(object clientObj)
        {
            LogInfo("ReceiverLoop start");
            var client = (TcpClient)clientObj;
            try
            {
                try
                {
                    var stream = client.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8, true, 64 * 1024);
                    try
                    {
                        LogInfo("ReceiverLoop loop");
                        while (!stopNetwork.IsCancellationRequested)
                        {
                            var message = reader.ReadLine();
                            if (message != null)
                            {
                                NetworkParseMessage(message);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        stream.Close();
                    }
                }
                finally
                {
                    client.Close();
                }
                LogInfo("ReceiverLoop stop");
            }
            catch (Exception ex)
            {
                if (!stopNetwork.IsCancellationRequested && !(ex is ObjectDisposedException))
                {
                    LogError(ex);
                }
            }
            receiveQueue.Enqueue("Disconnected");
            networkConnected = false;
        }

        static ClientConnection CreateNewClient()
        {
            int id = Interlocked.Increment(ref _uniqueClientId);
            var cc = new ClientConnection(id);
            _clientConnections[id] = cc;
            return cc;
        }

        static void RemoveClient(ClientConnection cc)
        {
            if (!_clientConnections.TryRemove(cc.id, out _))
            {
                LogWarning("Client " + cc.id + " not found among the connections!");
            }
        }

        /// <summary>
        /// Represents the dedicated sender queue towards a specific client.
        /// 
        /// The receiver queue is shared.
        /// </summary>
        internal class ClientConnection
        {
            internal readonly int id;

            internal TcpClient tcpClient;

            internal PlayerAvatar otherPlayer;

            internal readonly ConcurrentQueue<object> _sendQueue = new ConcurrentQueue<object>();
            internal readonly AutoResetEvent _sendQueueBlock = new AutoResetEvent(false);

            public ClientConnection(int id)
            {
                this.id = id;
            }

            internal void Send(object message)
            {
                if (otherPlayer != null)
                {
                    _sendQueue.Enqueue(message);
                }
            }
            internal void Signal()
            {
                _sendQueueBlock.Set();
            }
        }
    }
}
