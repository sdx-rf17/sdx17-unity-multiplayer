// Netcode adapter for Sdx17 Multiplayer Architecture.
// Requires com.unity.netcode.gameobjects. See Runtime/Netcode/README.md.
// The whole file is guarded: without the Netcode package installed it
// compiles to nothing and the core files still compile standalone.
#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Sdx17.Multiplayer.Netcode
{
    /// <summary>
    /// Implements <see cref="ITransport"/> on top of Netcode for GameObjects.
    /// Attach this to the same GameObject as the NetworkManager, then hand
    /// it to the manager with MultiplayerManager.SetTransport before starting.
    /// Security: this adapter only moves bytes. It never trusts a client:
    /// every action it receives is handed to MultiplayerManager, which
    /// validates it on the server before anything is applied or broadcast.
    /// Health and inventory are never sent by a client through this adapter.
    /// </summary>
    public sealed class NetcodeTransportAdapter : MonoBehaviour, ITransport
    {
        public event Action<ulong> OnClientConnected;
        public event Action<ulong> OnClientDisconnected;
        public event Action<PlayerAction> OnActionReceived;
        public event Action<PlayerAction> OnBroadcastReceived;

        [SerializeField]
        [Tooltip("When true, actions are sent with reliable delivery. Keep this on for gameplay actions; unreliable delivery is only suitable for frequent state that is resent every frame.")]
        private bool _reliableActions = true;

        public TransportState State { get; private set; } = TransportState.Stopped;
        public ConnectionMode Mode { get; private set; } = ConnectionMode.None;

        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsClient => NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost);
        public ulong LocalClientId => NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;

        private readonly HashSet<ulong> _connectedClientIds = new HashSet<ulong>();

        public IReadOnlyCollection<ulong> ConnectedClientIds => _connectedClientIds;

        public bool ReliableActions => _reliableActions;

        private void OnEnable()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] NetcodeTransportAdapter: no NetworkManager in the scene. Add one (GameObject > Netcode > NetworkManager) before starting.");
                return;
            }

            networkManager.OnClientConnectedCallback += HandleNetworkClientConnected;
            networkManager.OnClientDisconnectCallback += HandleNetworkClientDisconnected;
        }

        private void OnDisable()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= HandleNetworkClientConnected;
                networkManager.OnClientDisconnectCallback -= HandleNetworkClientDisconnected;
            }
        }

        public bool StartHost()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || State == TransportState.Running)
            {
                return false;
            }

            State = TransportState.Starting;
            bool started = networkManager.StartHost();
            if (!started)
            {
                State = TransportState.Stopped;
                return false;
            }

            Mode = ConnectionMode.Host;
            State = TransportState.Running;
            return true;
        }

        public bool StartClient(string address, ushort port)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || State == TransportState.Running)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(address) || port == 0)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] NetcodeTransportAdapter: StartClient needs a non-empty address and a non-zero port.");
                return false;
            }

            // UnityTransport reads its address and port from the
            // NetworkManager's transport component, so the caller must
            // configure those to match the MultiplayerConfig first.
            State = TransportState.Starting;
            bool started = networkManager.StartClient();
            if (!started)
            {
                State = TransportState.Stopped;
                return false;
            }

            Mode = ConnectionMode.Client;
            State = TransportState.Running;
            return true;
        }

        public bool StartServer(ushort port)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || State == TransportState.Running || port == 0)
            {
                return false;
            }

            State = TransportState.Starting;
            bool started = networkManager.StartServer();
            if (!started)
            {
                State = TransportState.Stopped;
                return false;
            }

            Mode = ConnectionMode.Server;
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
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            _connectedClientIds.Clear();
            Mode = ConnectionMode.None;
            State = TransportState.Stopped;
        }

        public bool SendAction(PlayerAction action)
        {
            if (State != TransportState.Running)
            {
                return false;
            }

            if (IsServer)
            {
                // Host/server request: deliver it to the local server side
                // exactly as a remote client's request would arrive, so it
                // goes through the same validation in MultiplayerManager.
                OnActionReceived?.Invoke(action);
                return true;
            }

            // Client -> server: send the request to the server only. The
            // server validates it before anyone else hears about it.
            SubmitActionServerRpc(action.ClientId, action.ActionId, action.TargetId, action.Value, action.ClientTimestamp);
            return true;
        }

        public bool BroadcastValidatedAction(PlayerAction action)
        {
            if (!IsServer || State != TransportState.Running)
            {
                return false;
            }

            BroadcastActionClientRpc(action.ClientId, action.ActionId, action.TargetId, action.Value, action.ClientTimestamp);
            return true;
        }

        /// <summary>
        /// Server -> all clients. Only the server ever sends this, and only
        /// for actions it already validated and applied. Clients apply the
        /// result through MultiplayerManager.ApplyServerBroadcast.
        /// </summary>
        [ClientRpc]
        private void BroadcastActionClientRpc(ulong clientId, string actionId, string targetId, float value, double clientTimestamp)
        {
            if (IsServer)
            {
                // The host already applied this action as the server.
                return;
            }

            OnBroadcastReceived?.Invoke(new PlayerAction(clientId, actionId, targetId, value, clientTimestamp));
        }

        public bool DisconnectClient(ulong clientId)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (!IsServer || networkManager == null)
            {
                return false;
            }

            if (!_connectedClientIds.Contains(clientId))
            {
                return false;
            }

            networkManager.DisconnectClient(clientId);
            return true;
        }

        /// <summary>
        /// Client-to-server request. RequireOwnership is false because the
        /// sender is a player asking the server to check an action, not
        /// claiming ownership of anything. The server validates the action
        /// in MultiplayerManager before applying it.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void SubmitActionServerRpc(ulong clientId, string actionId, string targetId, float value, double clientTimestamp, ServerRpcParams serverRpcParams = default)
        {
            // Defence in depth: the id the client claims must match the
            // id Netcode actually assigned to that connection. A client
            // that lies about who it is gets its action dropped here.
            if (serverRpcParams.Receive.SenderClientId != clientId)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetcodeTransportAdapter: dropped action '{actionId}' — sender {serverRpcParams.Receive.SenderClientId} claimed to be {clientId}.");
                return;
            }

            OnActionReceived?.Invoke(new PlayerAction(clientId, actionId, targetId, value, clientTimestamp));
        }

        private void HandleNetworkClientConnected(ulong clientId)
        {
            _connectedClientIds.Add(clientId);
            OnClientConnected?.Invoke(clientId);
        }

        private void HandleNetworkClientDisconnected(ulong clientId)
        {
            _connectedClientIds.Remove(clientId);
            OnClientDisconnected?.Invoke(clientId);
        }
    }
}
#endif
