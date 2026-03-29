// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        LanSync.cs                                                   ║
// ║  PURPOSE:     LAN PEER-TO-PEER DISCOVERY AND DIRECT SYNC                  ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Discovers other WorkFlow instances on the same local network via UDP      ║
// ║  broadcast. When peers are found, enables direct TCP communication for:    ║
// ║    - Chat message sync (bypasses Firebase for LAN users)                   ║
// ║    - Direct messages between LAN peers                                     ║
// ║    - File transfer between LAN peers                                       ║
// ║                                                                            ║
// ║  PROTOCOL:                                                                 ║
// ║    UDP 42420: Discovery broadcast (every 10 seconds)                       ║
// ║      Format: "WF|{teamJoinCode}|{userName}|{tcpPort}"                      ║
// ║    TCP {dynamic}: Direct message & file exchange                           ║
// ║      Header: 4-byte length + JSON payload                                  ║
// ║      Types: "chat_sync", "dm", "file_offer", "file_data"                  ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

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
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// Represents a discovered peer on the local network.
    /// </summary>
    public class LanPeer
    {
        public string UserName { get; set; }
        public string TeamJoinCode { get; set; }
        public IPAddress Address { get; set; }
        public int TcpPort { get; set; }
        public DateTime LastSeen { get; set; }

        /// <summary>True if this peer was seen in the last 30 seconds.</summary>
        public bool IsAlive => (DateTime.UtcNow - LastSeen).TotalSeconds < 30;
    }

    /// <summary>
    /// A message sent between peers over TCP.
    /// </summary>
    public class LanMessage
    {
        /// <summary>Message type: "chat", "dm", "file_offer", "file_data", "chat_request"</summary>
        public string type { get; set; }

        /// <summary>Sender username</summary>
        public string from { get; set; }

        /// <summary>Target username (for DMs) or "all" for chat broadcast</summary>
        public string to { get; set; }

        /// <summary>JSON payload (ChatMessage, DirectMessage, or file metadata)</summary>
        public string payload { get; set; }

        /// <summary>File name (for file_offer / file_data)</summary>
        public string fileName { get; set; }

        /// <summary>File size in bytes (for file_offer)</summary>
        public long fileSize { get; set; }

        /// <summary>Base64-encoded file data (for small files, &lt; 5MB)</summary>
        public string fileData { get; set; }
    }

    /// <summary>
    /// LAN peer-to-peer discovery and sync engine.
    /// Usage:
    ///   var lan = new LanSync("TEAMCODE", "MyName");
    ///   lan.OnChatReceived += (msg) => { /* new chat from LAN peer */ };
    ///   lan.OnDmReceived += (dm) => { /* new DM from LAN peer */ };
    ///   lan.OnFileReceived += (name, data) => { /* file from LAN peer */ };
    ///   lan.Start();
    ///   // ... later ...
    ///   lan.BroadcastChat(chatMessage);
    ///   lan.SendDirect("Bob", dmMessage);
    ///   lan.SendFile("Bob", filePath);
    ///   lan.Stop();
    /// </summary>
    public class LanSync : IDisposable
    {
        // ═══ CONFIGURATION ═══
        private const int UDP_PORT = 42420;          // UDP broadcast port for discovery
        private const int BROADCAST_INTERVAL = 10000; // Broadcast every 10 seconds
        private const int MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB max for inline file transfer

        private readonly string _teamJoinCode;
        private readonly string _userName;
        private int _tcpPort;

        // ═══ NETWORK COMPONENTS ═══
        private UdpClient _udpBroadcaster;
        private UdpClient _udpListener;
        private TcpListener _tcpListener;
        private CancellationTokenSource _cts;

        // ═══ PEER TRACKING ═══
        private readonly ConcurrentDictionary<string, LanPeer> _peers
            = new ConcurrentDictionary<string, LanPeer>();

        // ═══ EVENTS ═══
        /// <summary>Fired when a chat message is received from a LAN peer.</summary>
        public event Action<ChatMessage> OnChatReceived;

        /// <summary>Fired when a DM is received from a LAN peer.</summary>
        public event Action<DirectMessage> OnDmReceived;

        /// <summary>Fired when a file is received from a LAN peer. Args: fileName, fileData bytes.</summary>
        public event Action<string, byte[]> OnFileReceived;

        /// <summary>Fired when peer list changes (peer joins/leaves).</summary>
        public event Action<List<LanPeer>> OnPeersChanged;

        /// <summary>Fired for debug/status messages.</summary>
        public event Action<string> OnDebugMessage;

        public LanSync(string teamJoinCode, string userName)
        {
            _teamJoinCode = teamJoinCode?.ToUpper() ?? "DEFAULT";
            _userName = userName;
        }

        /// <summary>Returns current list of alive LAN peers.</summary>
        public List<LanPeer> GetPeers()
        {
            return _peers.Values.Where(p => p.IsAlive).ToList();
        }

        /// <summary>Check if a specific user is available on LAN.</summary>
        public bool IsPeerOnLan(string userName)
        {
            return _peers.Values.Any(p => p.IsAlive
                && p.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
        }

        // ═══ START / STOP ═══

        /// <summary>
        /// Start the LAN discovery and sync service.
        /// Initializes UDP broadcaster/listener and TCP server, starts background tasks.
        /// </summary>
        public void Start()
        {
//             DebugLogger.Log("[LanSync] Start() called");

            if (_cts != null)
            {
//                 DebugLogger.Log("[LanSync] Already running, ignoring Start() call");
                return; // already running
            }

            _cts = new CancellationTokenSource();

            try
            {
                // Find a free TCP port
//                 DebugLogger.Log("[LanSync] Finding free TCP port for peer connections");
                _tcpListener = new TcpListener(IPAddress.Any, 0);
                _tcpListener.Start();
                _tcpPort = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
//                 DebugLogger.Log($"[LanSync] TCP listener bound to port {_tcpPort}");

                // Start UDP listener
//                 DebugLogger.Log("[LanSync] Initializing UDP listener on port {UDP_PORT}");
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));
//                 DebugLogger.Log($"[LanSync] UDP listener bound to port {UDP_PORT}");

                // Start background tasks
//                 DebugLogger.Log("[LanSync] Starting background discovery and sync tasks");
                Task.Run(() => UdpBroadcastLoop(_cts.Token));
                Task.Run(() => UdpListenLoop(_cts.Token));
                Task.Run(() => TcpAcceptLoop(_cts.Token));
                Task.Run(() => PeerCleanupLoop(_cts.Token));

                OnDebugMessage?.Invoke($"[LAN] Started — TCP port {_tcpPort}, UDP port {UDP_PORT}");
//                 DebugLogger.Log($"[LanSync] Service started successfully - TCP:{_tcpPort}, UDP:{UDP_PORT}, Team:{_teamJoinCode}, User:{_userName}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR starting service: {ex.Message}");
                OnDebugMessage?.Invoke($"[LAN] Start failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the LAN discovery and sync service.
        /// Cancels all background tasks, closes sockets, clears peer list.
        /// </summary>
        public void Stop()
        {
//             DebugLogger.Log("[LanSync] Stop() called");
            _cts?.Cancel();
            _udpBroadcaster?.Close();
            _udpListener?.Close();
            _tcpListener?.Stop();
            int peerCount = _peers.Count;
            _peers.Clear();
//             DebugLogger.Log($"[LanSync] Service stopped - closed {peerCount} peer connection(s)");
            OnDebugMessage?.Invoke("[LAN] Stopped");
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }

        // ═══ UDP BROADCAST — ANNOUNCE PRESENCE ═══

        /// <summary>
        /// UDP broadcast loop - announces this peer's presence every 10 seconds.
        /// Message format: "WF|{teamJoinCode}|{userName}|{tcpPort}"
        /// </summary>
        private async Task UdpBroadcastLoop(CancellationToken ct)
        {
            try
            {
//                 DebugLogger.Log("[LanSync] UDP broadcast loop started");
                _udpBroadcaster = new UdpClient();
                _udpBroadcaster.EnableBroadcast = true;

                int broadcastCount = 0;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        string msg = $"WF|{_teamJoinCode}|{_userName}|{_tcpPort}";
                        byte[] data = Encoding.UTF8.GetBytes(msg);
                        await _udpBroadcaster.SendAsync(data, data.Length,
                            new IPEndPoint(IPAddress.Broadcast, UDP_PORT));
                        broadcastCount++;
//                         DebugLogger.Log($"[LanSync] UDP broadcast #{broadcastCount} sent: {msg}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[LanSync] ERROR sending UDP broadcast: {ex.Message}");
                    }

                    await Task.Delay(BROADCAST_INTERVAL, ct);
                }
            }
            catch (OperationCanceledException)
            {
//                 DebugLogger.Log("[LanSync] UDP broadcast loop cancelled");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR in UDP broadcast loop: {ex.Message}");
            }
        }

        // ═══ UDP LISTEN — DISCOVER PEERS ═══

        /// <summary>
        /// UDP listen loop - receives peer discovery broadcasts.
        /// Parses "WF|TEAMCODE|UserName|TcpPort" messages and tracks live peers.
        /// </summary>
        private async Task UdpListenLoop(CancellationToken ct)
        {
            try
            {
//                 DebugLogger.Log("[LanSync] UDP listen loop started");
                int messageCount = 0;

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _udpListener.ReceiveAsync();
                        string msg = Encoding.UTF8.GetString(result.Buffer);
                        messageCount++;

                        // Parse: "WF|TEAMCODE|UserName|TcpPort"
                        var parts = msg.Split('|');
                        if (parts.Length != 4 || parts[0] != "WF")
                        {
//                             DebugLogger.Log($"[LanSync] Ignoring invalid UDP broadcast (malformed): {msg}");
                            continue;
                        }

                        string teamCode = parts[1];
                        string userName = parts[2];
                        int tcpPort = int.Parse(parts[3]);

                        // Only track peers from the same team, and not ourselves
                        if (!teamCode.Equals(_teamJoinCode, StringComparison.OrdinalIgnoreCase))
                        {
//                             DebugLogger.Log($"[LanSync] Ignoring broadcast from different team: {teamCode}");
                            continue;
                        }

                        if (userName.Equals(_userName, StringComparison.OrdinalIgnoreCase))
                        {
//                             DebugLogger.Log("[LanSync] Ignoring own broadcast");
                            continue;
                        }

                        var peer = new LanPeer
                        {
                            UserName = userName,
                            TeamJoinCode = teamCode,
                            Address = result.RemoteEndPoint.Address,
                            TcpPort = tcpPort,
                            LastSeen = DateTime.UtcNow
                        };

                        bool isNew = !_peers.ContainsKey(userName);
                        _peers[userName] = peer;

                        if (isNew)
                        {
//                             DebugLogger.Log($"[LanSync] NEW PEER discovered: {userName} at {peer.Address}:{tcpPort}");
                            OnDebugMessage?.Invoke($"[LAN] Discovered peer: {userName} at {peer.Address}:{tcpPort}");
                            OnPeersChanged?.Invoke(GetPeers());
                        }
                        else
                        {
//                             DebugLogger.Log($"[LanSync] Peer heartbeat from {userName} at {peer.Address}:{tcpPort}");
                        }
                    }
                    catch (FormatException ex)
                    {
                        DebugLogger.Log($"[LanSync] ERROR parsing UDP message: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[LanSync] ERROR in UDP listen iteration: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
//                 DebugLogger.Log("[LanSync] UDP listen loop cancelled");
            }
            catch (ObjectDisposedException)
            {
//                 DebugLogger.Log("[LanSync] UDP listener socket was disposed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR in UDP listen loop: {ex.Message}");
            }
        }

        // ═══ PEER CLEANUP — REMOVE STALE PEERS ═══

        /// <summary>
        /// Peer cleanup loop - removes stale peers (not seen for 30 seconds).
        /// Runs every 15 seconds to detect peer disconnections.
        /// </summary>
        private async Task PeerCleanupLoop(CancellationToken ct)
        {
            try
            {
//                 DebugLogger.Log("[LanSync] Peer cleanup loop started (runs every 15 seconds)");

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(15000, ct);

                    bool changed = false;
                    int cleanedCount = 0;

                    foreach (var kvp in _peers.ToArray())
                    {
                        if (!kvp.Value.IsAlive)
                        {
                            LanPeer removed;
                            if (_peers.TryRemove(kvp.Key, out removed))
                            {
                                cleanedCount++;
//                                 DebugLogger.Log($"[LanSync] PEER LEFT: {kvp.Key} (no heartbeat for 30+ seconds)");
                                OnDebugMessage?.Invoke($"[LAN] Peer left: {kvp.Key}");
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
//                         DebugLogger.Log($"[LanSync] Peer list changed: {cleanedCount} peer(s) removed, {_peers.Count} remaining");
                        OnPeersChanged?.Invoke(GetPeers());
                    }
                }
            }
            catch (OperationCanceledException)
            {
//                 DebugLogger.Log("[LanSync] Peer cleanup loop cancelled");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR in peer cleanup loop: {ex.Message}");
            }
        }

        // ═══ TCP SERVER — ACCEPT INCOMING CONNECTIONS ═══

        /// <summary>
        /// TCP accept loop - listens for incoming peer connections.
        /// Spawns a task for each connected peer to handle message receiving.
        /// </summary>
        private async Task TcpAcceptLoop(CancellationToken ct)
        {
            try
            {
//                 DebugLogger.Log("[LanSync] TCP accept loop started");
                int connectionCount = 0;

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _tcpListener.AcceptTcpClientAsync();
                        connectionCount++;
//                         DebugLogger.Log($"[LanSync] Incoming TCP connection #{connectionCount} from {client.Client.RemoteEndPoint}");
                        // Handle each connection in its own task
                        _ = Task.Run(() => HandleTcpClient(client, ct));
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[LanSync] ERROR accepting TCP connection: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
//                 DebugLogger.Log("[LanSync] TCP accept loop cancelled");
            }
            catch (ObjectDisposedException)
            {
//                 DebugLogger.Log("[LanSync] TCP listener was disposed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR in TCP accept loop: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle a single TCP client connection.
        /// Reads message length prefix (4 bytes) followed by JSON payload.
        /// </summary>
        private async Task HandleTcpClient(TcpClient client, CancellationToken ct)
        {
            string clientAddress = client?.Client?.RemoteEndPoint?.ToString() ?? "unknown";
            try
            {
//                 DebugLogger.Log($"[LanSync] Handling TCP client from {clientAddress}");

                using (client)
                using (var stream = client.GetStream())
                {
                    // Read 4-byte length prefix
                    byte[] lenBuf = new byte[4];
                    int read = await ReadExactAsync(stream, lenBuf, 4, ct);
                    if (read < 4)
                    {
                        DebugLogger.Log($"[LanSync] Failed to read message length from {clientAddress}");
                        return;
                    }

                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
//                     DebugLogger.Log($"[LanSync] Message size from {clientAddress}: {msgLen} bytes");

                    if (msgLen <= 0 || msgLen > 50 * 1024 * 1024)
                    {
//                         DebugLogger.Log($"[LanSync] Invalid message size {msgLen} from {clientAddress}, aborting");
                        return; // max 50 MB
                    }

                    byte[] msgBuf = new byte[msgLen];
                    read = await ReadExactAsync(stream, msgBuf, msgLen, ct);
                    if (read < msgLen)
                    {
//                         DebugLogger.Log($"[LanSync] Incomplete message from {clientAddress} (expected {msgLen}, got {read})");
                        return;
                    }

                    string json = Encoding.UTF8.GetString(msgBuf);
                    var lanMsg = JsonConvert.DeserializeObject<LanMessage>(json);
                    if (lanMsg == null)
                    {
                        DebugLogger.Log($"[LanSync] Failed to deserialize message from {clientAddress}");
                        return;
                    }

//                     DebugLogger.Log($"[LanSync] Received message type '{lanMsg.type}' from {lanMsg.from}");
                    ProcessIncomingMessage(lanMsg);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR handling TCP client {clientAddress}: {ex.Message}");
            }
        }

        /// <summary>
        /// Process an incoming message based on its type.
        /// Routes to appropriate handlers: chat broadcast, direct message, or file transfer.
        /// </summary>
        private void ProcessIncomingMessage(LanMessage msg)
        {
            try
            {
                switch (msg.type)
                {
                    case "chat":
//                         DebugLogger.Log($"[LanSync] Processing CHAT message from {msg.from}");
                        var chatMsg = JsonConvert.DeserializeObject<ChatMessage>(msg.payload);
                        if (chatMsg != null)
                        {
                            // Save to local storage
                            LocalChatStore.AddMessage(chatMsg);
                            OnChatReceived?.Invoke(chatMsg);
                            string preview = chatMsg.message?.Substring(0, Math.Min(30, chatMsg.message?.Length ?? 0)) ?? "";
                            OnDebugMessage?.Invoke($"[LAN] Chat from {msg.from}: {preview}...");
//                             DebugLogger.Log($"[LanSync] Chat message saved and broadcasted to subscribers");
                        }
                        break;

                    case "dm":
//                         DebugLogger.Log($"[LanSync] Processing DIRECT MESSAGE from {msg.from}");
                        var dm = JsonConvert.DeserializeObject<DirectMessage>(msg.payload);
                        if (dm != null)
                        {
                            OnDmReceived?.Invoke(dm);
                            OnDebugMessage?.Invoke($"[LAN] DM from {msg.from}");
//                             DebugLogger.Log($"[LanSync] Direct message delivered to subscribers");
                        }
                        break;

                    case "file_data":
//                         DebugLogger.Log($"[LanSync] Processing FILE transfer from {msg.from}: {msg.fileName}");
                        if (!string.IsNullOrEmpty(msg.fileData) && !string.IsNullOrEmpty(msg.fileName))
                        {
                            byte[] fileBytes = Convert.FromBase64String(msg.fileData);
                            OnFileReceived?.Invoke(msg.fileName, fileBytes);
                            OnDebugMessage?.Invoke($"[LAN] File received: {msg.fileName} ({fileBytes.Length} bytes) from {msg.from}");
//                             DebugLogger.Log($"[LanSync] File {msg.fileName} ({fileBytes.Length} bytes) received and delivered");
                        }
                        break;

                    default:
//                         DebugLogger.Log($"[LanSync] Unknown message type '{msg.type}' from {msg.from}, ignoring");
                        break;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR processing incoming message (type={msg.type}): {ex.Message}");
            }
        }

        // ═══ SEND METHODS — PUSH DATA TO LAN PEERS ═══

        /// <summary>
        /// Broadcasts a chat message to ALL LAN peers in the same team.
        /// </summary>
        public async Task BroadcastChat(ChatMessage chatMsg)
        {
//             DebugLogger.Log($"[LanSync] Broadcasting chat message to all peers");

            var lanMsg = new LanMessage
            {
                type = "chat",
                from = _userName,
                to = "all",
                payload = JsonConvert.SerializeObject(chatMsg)
            };

            var peers = GetPeers();
//             DebugLogger.Log($"[LanSync] Broadcasting to {peers.Count} peer(s)");

            foreach (var peer in peers)
            {
                await SendToPeerAsync(peer, lanMsg);
            }
        }

        /// <summary>
        /// Sends a DM directly to a specific LAN peer (bypasses Firebase).
        /// </summary>
        public async Task SendDirect(string targetUser, DirectMessage dm)
        {
//             DebugLogger.Log($"[LanSync] SendDirect() to {targetUser}");

            LanPeer peer;
            if (!_peers.TryGetValue(targetUser, out peer) || !peer.IsAlive)
            {
//                 DebugLogger.Log($"[LanSync] Peer '{targetUser}' not available on LAN, will use Firebase instead");
                OnDebugMessage?.Invoke($"[LAN] Peer '{targetUser}' not on LAN, use Firebase");
                return;
            }

//             DebugLogger.Log($"[LanSync] Sending DM to {targetUser} at {peer.Address}:{peer.TcpPort}");

            var lanMsg = new LanMessage
            {
                type = "dm",
                from = _userName,
                to = targetUser,
                payload = JsonConvert.SerializeObject(dm)
            };

            await SendToPeerAsync(peer, lanMsg);
        }

        /// <summary>
        /// Sends a file directly to a LAN peer.
        /// Files under 5 MB are sent inline as base64.
        /// </summary>
        public async Task<bool> SendFile(string targetUser, string filePath)
        {
//             DebugLogger.Log($"[LanSync] SendFile() to {targetUser}: {filePath}");

            LanPeer peer;
            if (!_peers.TryGetValue(targetUser, out peer) || !peer.IsAlive)
            {
//                 DebugLogger.Log($"[LanSync] Peer '{targetUser}' not available on LAN, cannot send file");
                return false;
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
//                 DebugLogger.Log($"[LanSync] File not found: {filePath}");
                return false;
            }

            if (fileInfo.Length > MAX_FILE_SIZE)
            {
//                 DebugLogger.Log($"[LanSync] File too large ({fileInfo.Length} bytes > {MAX_FILE_SIZE} max), cannot send");
                return false;
            }

//             DebugLogger.Log($"[LanSync] Reading file {fileInfo.Name} ({fileInfo.Length} bytes)");
            byte[] fileBytes = File.ReadAllBytes(filePath);

            var lanMsg = new LanMessage
            {
                type = "file_data",
                from = _userName,
                to = targetUser,
                fileName = fileInfo.Name,
                fileSize = fileInfo.Length,
                fileData = Convert.ToBase64String(fileBytes)
            };

            await SendToPeerAsync(peer, lanMsg);
            OnDebugMessage?.Invoke($"[LAN] File sent to {targetUser}: {fileInfo.Name}");
//             DebugLogger.Log($"[LanSync] File {fileInfo.Name} sent to {targetUser}");
            return true;
        }

        // ═══ TCP HELPERS ═══

        /// <summary>
        /// Send a message to a specific peer via TCP.
        /// Includes 4-byte length prefix followed by JSON payload.
        /// </summary>
        private async Task SendToPeerAsync(LanPeer peer, LanMessage msg)
        {
            try
            {
//                 DebugLogger.Log($"[LanSync] Connecting to peer {peer.UserName} at {peer.Address}:{peer.TcpPort} (3s timeout)");

                using (var client = new TcpClient())
                {
                    client.ConnectTimeout(peer.Address, peer.TcpPort, 3000);
//                     DebugLogger.Log($"[LanSync] Connected to {peer.UserName}");

                    using (var stream = client.GetStream())
                    {
                        string json = JsonConvert.SerializeObject(msg);
                        byte[] data = Encoding.UTF8.GetBytes(json);
                        byte[] lenBytes = BitConverter.GetBytes(data.Length);

//                         DebugLogger.Log($"[LanSync] Sending message to {peer.UserName}: {data.Length} bytes (type={msg.type})");

                        await stream.WriteAsync(lenBytes, 0, 4);
                        await stream.WriteAsync(data, 0, data.Length);
                        await stream.FlushAsync();

//                         DebugLogger.Log($"[LanSync] Message sent successfully to {peer.UserName}");
                    }
                }
            }
            catch (TimeoutException)
            {
                DebugLogger.Log($"[LanSync] ERROR: Timeout connecting to peer {peer.UserName} at {peer.Address}:{peer.TcpPort}");
                OnDebugMessage?.Invoke($"[LAN] Send to {peer.UserName} failed: Connection timeout");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LanSync] ERROR sending to peer {peer.UserName}: {ex.Message}");
                OnDebugMessage?.Invoke($"[LAN] Send to {peer.UserName} failed: {ex.Message}");
            }
        }

        private async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int read = await stream.ReadAsync(buffer, total, count - total, ct);
                if (read == 0) break;
                total += read;
            }
            return total;
        }
    }

    /// <summary>
    /// Extension method for TcpClient connect with timeout.
    /// </summary>
    internal static class TcpClientExtensions
    {
        public static void ConnectTimeout(this TcpClient client, IPAddress address, int port, int timeoutMs)
        {
            var result = client.BeginConnect(address, port, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(timeoutMs);
            if (!success)
            {
                client.Close();
                throw new TimeoutException("TCP connect timed out");
            }
            client.EndConnect(result);
        }
    }
}
