using BepInEx;
using FeatMultiplayer.MessageTypes;
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

        static readonly ConcurrentQueue<object> _receiveQueue = new();

        static CancellationTokenSource stopNetwork;

        static int _uniqueClientId;
        static readonly ConcurrentDictionary<int, ClientConnection> _clientConnections = new();

        static volatile ClientConnection _towardsHost;

        static void Receive(ClientConnection sender, MessageBase obj)
        {
            obj.sender = sender;
            _receiveQueue.Enqueue(obj);
        }

        static void SendHost(object obj, bool signal = false)
        {
            var h = _towardsHost;
            h?.Send(obj);
            if (signal)
            {
                h?.Signal();
            }
        }

        static void SendClient(int clientId, object obj, bool signal = false)
        {
            if (_clientConnections.TryGetValue(clientId, out var cc))
            {
                cc.Send(obj);
                if (signal)
                {
                    cc.Signal();
                }
            }
            else
            {
                LogWarning("Unknown client or client already disconnected: " + clientId);
            }
        }

        static void SendAllClients(object obj, bool signal = false)
        {
            foreach (var kv in _clientConnections)
            {
                kv.Value.Send(obj);
                if (signal)
                {
                    kv.Value.Signal();
                }
            }
        }

        /// <summary>
        /// Send a message to all clients, except one client.
        /// Typically used when dispatching a message from one
        /// client to the rest of the clients on the host.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="obj"></param>
        static void SendAllClientsExcept(int clientId, object obj, bool signal = false)
        {
            foreach (var kv in _clientConnections)
            {
                if (kv.Key != clientId)
                {
                    kv.Value.Send(obj);
                    if (signal)
                    {
                        kv.Value.Signal();
                    }
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
                    stopNetwork.Token.Register(() =>
                    {
                        client.Close();
                    });

                    var cc = new ClientConnection(0);
                    cc.clientName = ""; // host is always ""
                    _towardsHost = cc;
                    cc.tcpClient = client;

                    Task.Factory.StartNew(SenderLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    Task.Factory.StartNew(ReceiveLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

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
            var ccount = _clientConnections.Count;
            var cmax = maxClients.Value;
            if (ccount >= cmax)
            {
                LogInfo("Too many clients: Current = " + ccount + ", Max = " + cmax);
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

                var cc = CreateNewClient();
                cc.tcpClient = client;
                Task.Factory.StartNew(SenderLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task.Factory.StartNew(ReceiveLoop, cc, stopNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
                                    case IMessageStringProvider msp:
                                        {
                                            stream.Write(msp.GetString());
                                            stream.Flush();
                                            break;
                                        }
                                    case IMessageBytesProvider msp:
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
            var clientConnection = (ClientConnection)clientObj;
            var client = clientConnection.tcpClient;
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
                                NetworkParseMessage(message, clientConnection);
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
            _receiveQueue.Enqueue("Disconnected");
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
            if (_clientConnections.TryRemove(cc.id, out _))
            {
                Receive(cc, new MessageClientDisconnected());
            }
        }
    }
}
