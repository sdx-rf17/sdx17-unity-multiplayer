using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sdx17.Multiplayer
{
    /// <summary>
    /// How this instance participates in a session.
    /// Host runs a server and a local client at the same time.
    /// </summary>
    public enum ConnectionMode
    {
        None,
        Host,
        Client,
        Server
    }

    /// <summary>
    /// Lifecycle of the active transport.
    /// </summary>
    public enum TransportState
    {
        Stopped,
        Starting,
        Running,
        Stopping
    }

    /// <summary>
    /// A validated player action travelling client -> server -> all clients.
    /// The server is the only authority that decides whether an action happened.
    /// Clients never apply their own action directly; they wait for the
    /// server broadcast, so a modified client cannot invent results.
    /// </summary>
    [Serializable]
    public struct PlayerAction
    {
        public ulong ClientId;
        public string ActionId;
        public string TargetId;
        public float Value;
        public double ClientTimestamp;

        public PlayerAction(ulong clientId, string actionId, string targetId, float value, double clientTimestamp)
        {
            ClientId = clientId;
            ActionId = actionId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Value = value;
            ClientTimestamp = clientTimestamp;
        }
    }

    /// <summary>
    /// Transport abstraction. Core multiplayer code talks only to this
    /// interface, never to a networking library directly, so the transport
    /// can be swapped (loopback for offline work, Netcode for real play)
    /// without touching game logic.
    /// Security note: a transport must never trust a remote peer's claims
    /// about its own identity, health or inventory. Those values live on
    /// the server and are only ever sent downwards by the server.
    /// </summary>
    public interface ITransport
    {
        event Action<ulong> OnClientConnected;
        event Action<ulong> OnClientDisconnected;
        event Action<PlayerAction> OnActionReceived;

        /// <summary>
        /// Raised when the server broadcasts an action it already validated
        /// and applied. Clients use this to apply the server's result.
        /// The server never raises this for itself (it already applied it).
        /// </summary>
        event Action<PlayerAction> OnBroadcastReceived;

        TransportState State { get; }
        ConnectionMode Mode { get; }
        bool IsServer { get; }
        bool IsHost { get; }
        bool IsClient { get; }
        ulong LocalClientId { get; }
        IReadOnlyCollection<ulong> ConnectedClientIds { get; }

        bool StartHost();
        bool StartClient(string address, ushort port);
        bool StartServer(ushort port);
        void Shutdown();

        /// <summary>
        /// Send an action request to the server (client side) or, when called
        /// on the server, broadcast a validated action to every client.
        /// Returns false when there is no active connection to send on.
        /// </summary>
        bool SendAction(PlayerAction action);

        /// <summary>
        /// Server only. Broadcast an action the server already validated
        /// and applied, so clients can apply the server's result.
        /// Returns false when not the server or not running.
        /// </summary>
        bool BroadcastValidatedAction(PlayerAction action);

        /// <summary>
        /// Disconnect one remote client. Server/host only; a client calling
        /// this on a remote id must be ignored by the implementation.
        /// </summary>
        bool DisconnectClient(ulong clientId);
    }

    /// <summary>
    /// In-memory transport for offline development and Play-mode self-tests.
    /// It needs no network, no package and no server process: StartHost
    /// creates a local server plus a local client (id 0), and simulated
    /// remote clients can be added with <see cref="SimulateRemoteClientJoin"/>.
    /// Actions sent by a client are delivered straight to the server side
    /// of this same transport, which is exactly the path
    /// <see cref="MultiplayerManager"/> validates before broadcasting.
    /// Do not use this transport for real networked play; use the Netcode
    /// adapter in Runtime/Netcode instead.
    /// </summary>
    public sealed class LocalLoopbackTransport : ITransport
    {
        public event Action<ulong> OnClientConnected;
        public event Action<ulong> OnClientDisconnected;
        public event Action<PlayerAction> OnActionReceived;
        public event Action<PlayerAction> OnBroadcastReceived;

        public TransportState State { get; private set; } = TransportState.Stopped;
        public ConnectionMode Mode { get; private set; } = ConnectionMode.None;

        public bool IsServer => Mode == ConnectionMode.Host || Mode == ConnectionMode.Server;
        public bool IsHost => Mode == ConnectionMode.Host;
        public bool IsClient => Mode == ConnectionMode.Host || Mode == ConnectionMode.Client;

        public ulong LocalClientId { get; private set; }

        private readonly HashSet<ulong> _connectedClientIds = new HashSet<ulong>();
        private readonly List<ulong> _simulatedRemoteClients = new List<ulong>();
        private ulong _nextSimulatedClientId = 1;

        public IReadOnlyCollection<ulong> ConnectedClientIds => _connectedClientIds;

        public bool StartHost()
        {
            if (State == TransportState.Running)
            {
                return false;
            }

            State = TransportState.Starting;
            Mode = ConnectionMode.Host;
            LocalClientId = 0;
            _connectedClientIds.Clear();
            _connectedClientIds.Add(LocalClientId);
            State = TransportState.Running;
            OnClientConnected?.Invoke(LocalClientId);
            return true;
        }

        public bool StartClient(string address, ushort port)
        {
            if (State == TransportState.Running)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(address) || port == 0)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] LocalLoopbackTransport: StartClient needs a non-empty address and a non-zero port.");
                return false;
            }

            // The loopback transport has no real socket, so a "client" run
            // is a local session with no server to talk to. It still runs
            // so demo code can exercise the client code path offline.
            State = TransportState.Starting;
            Mode = ConnectionMode.Client;
            LocalClientId = 0;
            _connectedClientIds.Clear();
            _connectedClientIds.Add(LocalClientId);
            State = TransportState.Running;
            OnClientConnected?.Invoke(LocalClientId);
            return true;
        }

        public bool StartServer(ushort port)
        {
            if (State == TransportState.Running)
            {
                return false;
            }

            if (port == 0)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] LocalLoopbackTransport: StartServer needs a non-zero port.");
                return false;
            }

            State = TransportState.Starting;
            Mode = ConnectionMode.Server;
            LocalClientId = 0;
            _connectedClientIds.Clear();
            State = TransportState.Running;
            return true;
        }

        public void Shutdown()
        {
            if (State == TransportState.Stopped)
            {
                return;
            }

            State = TransportState.Stopping;
            var ids = new List<ulong>(_connectedClientIds);
            _connectedClientIds.Clear();
            _simulatedRemoteClients.Clear();
            foreach (ulong id in ids)
            {
                OnClientDisconnected?.Invoke(id);
            }

            Mode = ConnectionMode.None;
            LocalClientId = 0;
            State = TransportState.Stopped;
        }

        public bool SendAction(PlayerAction action)
        {
            if (State != TransportState.Running)
            {
                return false;
            }

            if (!_connectedClientIds.Contains(action.ClientId))
            {
                // Security: an action from a client id this transport has
                // never seen connected is dropped, not processed.
                Debug.LogWarning($"[Sdx17.Multiplayer] LocalLoopbackTransport: dropped action '{action.ActionId}' from unknown client {action.ClientId}.");
                return false;
            }

            OnActionReceived?.Invoke(action);
            return true;
        }

        public bool BroadcastValidatedAction(PlayerAction action)
        {
            if (!IsServer || State != TransportState.Running)
            {
                return false;
            }

            // In-process transport: the broadcast reaches the same manager
            // instance, which ignores it because it already applied the
            // action as the server. A real network transport delivers it
            // to remote clients instead (see the Netcode adapter).
            OnBroadcastReceived?.Invoke(action);
            return true;
        }

        public bool DisconnectClient(ulong clientId)
        {
            if (!IsServer)
            {
                return false;
            }

            if (!_connectedClientIds.Contains(clientId))
            {
                return false;
            }

            _connectedClientIds.Remove(clientId);
            _simulatedRemoteClients.Remove(clientId);
            OnClientDisconnected?.Invoke(clientId);
            return true;
        }

        /// <summary>
        /// Test/demo helper: pretend a remote client joined this local
        /// server. Returns the simulated client id, or ulong.MaxValue when
        /// this transport is not running as a server.
        /// </summary>
        public ulong SimulateRemoteClientJoin()
        {
            if (!IsServer || State != TransportState.Running)
            {
                return ulong.MaxValue;
            }

            ulong id = _nextSimulatedClientId++;
            _simulatedRemoteClients.Add(id);
            _connectedClientIds.Add(id);
            OnClientConnected?.Invoke(id);
            return id;
        }
    }
}
