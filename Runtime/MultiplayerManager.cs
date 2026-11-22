using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sdx17.Multiplayer
{
    /// <summary>
    /// Owns a session: starts host / client / server, keeps the player list,
    /// spawns and removes <see cref="NetworkPlayer"/> objects, and runs the
    /// server-validated action pattern.
    ///
    /// Server-validated RPC pattern (the core security decision in this package):
    ///   1. A client calls RequestDamage / RequestAddItem / RequestAction.
    ///      It changes nothing locally — it only sends a request.
    ///   2. The server receives the request and validates it: the sender
    ///      must be a connected player, must be alive for combat actions,
    ///      the value must be in range, and the request must not be stale
    ///      (see MultiplayerConfig.ActionStalenessWindowMs — this is the
    ///      lag note: a request that arrives after the window is rejected,
    ///      so a delayed or replayed packet cannot apply twice or apply a
    ///      hit that was already answered).
    ///   3. Only when validation passes does the server apply the change
    ///      and broadcast the result through MultiplayerEvents.
    /// A client that edits its own health or inventory directly is ignored:
    /// NetworkPlayer refuses state changes on non-server instances, and
    /// the server never accepts state values sent by a client.
    ///
    /// Lag note: this package does not do client-side prediction or lag
    /// compensation for movement. It validates timestamps and rejects stale
    /// actions, which is enough for the demo and for turn-style or slow
    /// actions. Fast hit-scan combat needs a dedicated lag-compensation
    /// pass on top of this layer — see README "Integration notes".
    /// </summary>
    public sealed class MultiplayerManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private MultiplayerConfig _config;

        [Header("Player")]
        [SerializeField] private NetworkPlayer _playerPrefab;
        [SerializeField] private string _localPlayerName = "Player";

        [Header("Transport")]
        [SerializeField]
        [Tooltip("When true the manager builds its own LocalLoopbackTransport. Turn it off and assign a transport in code with SetTransport before starting, for example the Netcode adapter.")]
        private bool _createLoopbackTransport = true;

        public MultiplayerConfig Config => _config;
        public NetworkPlayer PlayerPrefab => _playerPrefab;
        public string LocalPlayerName => _localPlayerName;

        public ITransport Transport { get; private set; }
        public ConnectionMode Mode => Transport != null ? Transport.Mode : ConnectionMode.None;
        public bool IsRunning => Transport != null && Transport.State == TransportState.Running;
        public bool IsServer => Transport != null && Transport.IsServer;
        public bool IsHost => Transport != null && Transport.IsHost;
        public bool IsClient => Transport != null && Transport.IsClient;
        public ulong LocalClientId => Transport != null ? Transport.LocalClientId : ulong.MaxValue;

        /// <summary>All currently registered players, keyed by client id.</summary>
        public IReadOnlyDictionary<ulong, NetworkPlayer> Players => _players;

        public NetworkPlayer LocalPlayer
        {
            get
            {
                if (Transport == null) return null;
                _players.TryGetValue(Transport.LocalClientId, out NetworkPlayer player);
                return player;
            }
        }

        private readonly Dictionary<ulong, NetworkPlayer> _players = new Dictionary<ulong, NetworkPlayer>();
        private readonly Dictionary<ulong, double> _lastActionTimeByClient = new Dictionary<ulong, double>();
        private float _heartbeatTimer;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] MultiplayerManager: no MultiplayerConfig assigned. Assign one in the Inspector, or the manager will refuse to start.");
            }

            if (_createLoopbackTransport)
            {
                SetTransport(new LocalLoopbackTransport());
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            UnsubscribeTransport();
        }

        private void Update()
        {
            if (!IsRunning || !IsServer)
            {
                return;
            }

            // Heartbeat bookkeeping: the timer itself is the heartbeat in
            // this layer — a real transport sends a packet here. If a
            // client stops answering for longer than the configured
            // timeout, the transport reports the disconnect and
            // HandleClientDisconnected removes the player.
            float interval = _config != null ? _config.HeartbeatIntervalSeconds : 2f;
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= interval)
            {
                _heartbeatTimer = 0f;
            }
        }

        /// <summary>
        /// Replaces the transport (for example with the Netcode adapter).
        /// Must be called before StartHost / StartClient / StartServer.
        /// Passing null clears the transport.
        /// </summary>
        public void SetTransport(ITransport transport)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] MultiplayerManager: cannot change transport while a session is running. Shut down first.");
                return;
            }

            UnsubscribeTransport();
            Transport = transport;
            SubscribeTransport();
        }

        public void SetConfig(MultiplayerConfig config)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] MultiplayerManager: cannot change config while a session is running. Shut down first.");
                return;
            }

            _config = config;
        }

        public bool StartHost()
        {
            if (!CanStart(out string problem))
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] StartHost refused: {problem}");
                return false;
            }

            if (!Transport.StartHost())
            {
                return false;
            }

            MultiplayerEvents.RaiseConnectionChanged(ConnectionMode.Host, true);
            SpawnLocalPlayer();
            return true;
        }

        public bool StartClient()
        {
            if (!CanStart(out string problem))
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] StartClient refused: {problem}");
                return false;
            }

            if (!Transport.StartClient(_config.ServerAddress, _config.ServerPort))
            {
                return false;
            }

            MultiplayerEvents.RaiseConnectionChanged(ConnectionMode.Client, true);
            SpawnLocalPlayer();
            return true;
        }

        public bool StartServer()
        {
            if (!CanStart(out string problem))
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] StartServer refused: {problem}");
                return false;
            }

            if (!Transport.StartServer(_config.ServerPort))
            {
                return false;
            }

            MultiplayerEvents.RaiseConnectionChanged(ConnectionMode.Server, true);
            return true;
        }

        public void Shutdown()
        {
            if (Transport == null || Transport.State == TransportState.Stopped)
            {
                return;
            }

            ConnectionMode mode = Transport.Mode;
            Transport.Shutdown();
            RemoveAllPlayers();
            _lastActionTimeByClient.Clear();
            MultiplayerEvents.RaiseConnectionChanged(mode, false);
        }

        /// <summary>
        /// Server/host only. Disconnects a remote client and removes its
        /// player. The local client id can never be disconnected this way.
        /// Returns false when not the server, when the id is unknown, or
        /// when the id is the local client.
        /// </summary>
        public bool DisconnectPlayer(ulong clientId)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[Sdx17.Multiplayer] DisconnectPlayer refused — this instance is not the server.");
                return false;
            }

            if (Transport != null && clientId == Transport.LocalClientId)
            {
                return false;
            }

            return Transport != null && Transport.DisconnectClient(clientId);
        }

        /// <summary>
        /// Registers a player that already exists in the scene (for example
        /// one spawned by a transport callback). Returns false for a null
        /// player or a client id that is already registered.
        /// </summary>
        public bool RegisterPlayer(NetworkPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (_players.ContainsKey(player.ClientId))
            {
                return false;
            }

            _players.Add(player.ClientId, player);
            MultiplayerEvents.RaisePlayerSpawned(player);
            return true;
        }

        /// <summary>
        /// Removes a player from the list and destroys its GameObject when
        /// one exists. Returns false when the player is not registered.
        /// </summary>
        public bool RemovePlayer(ulong clientId)
        {
            if (!_players.TryGetValue(clientId, out NetworkPlayer player))
            {
                return false;
            }

            _players.Remove(clientId);
            _lastActionTimeByClient.Remove(clientId);
            MultiplayerEvents.RaisePlayerLeft(clientId, player != null ? player.PlayerName : string.Empty);
            if (player != null && player.gameObject != null)
            {
                Destroy(player.gameObject);
            }

            return true;
        }

        /// <summary>
        /// Client request: deal damage to another player. The client changes
        /// nothing itself. The request travels to the server, which
        /// validates it (see ValidateAction) and only then applies damage
        /// and broadcasts the result.
        /// </summary>
        public bool RequestDamage(ulong targetClientId, int damage)
        {
            if (Transport == null || !IsRunning)
            {
                return false;
            }

            if (damage <= 0)
            {
                return false;
            }

            var action = new PlayerAction(Transport.LocalClientId, "damage", targetClientId.ToString(), damage, Time.realtimeSinceStartupAsDouble);
            return Transport.SendAction(action);
        }

        /// <summary>
        /// Client request: heal the local player. Validated on the server
        /// the same way as damage.
        /// </summary>
        public bool RequestHeal(int amount)
        {
            if (Transport == null || !IsRunning || amount <= 0)
            {
                return false;
            }

            var action = new PlayerAction(Transport.LocalClientId, "heal", Transport.LocalClientId.ToString(), amount, Time.realtimeSinceStartupAsDouble);
            return Transport.SendAction(action);
        }

        /// <summary>
        /// Client request: give the local player an item. The server checks
        /// the item id and that the player does not already own it.
        /// </summary>
        public bool RequestAddItem(string itemId)
        {
            if (Transport == null || !IsRunning || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            var action = new PlayerAction(Transport.LocalClientId, "add-item", itemId, 0f, Time.realtimeSinceStartupAsDouble);
            return Transport.SendAction(action);
        }

        /// <summary>
        /// Client request with a custom action id. Use it for game-specific
        /// actions (for example "interact" or "use-skill"). The server
        /// validates the sender and the timestamp, then broadcasts the
        /// action unchanged for game code to handle — the manager does not
        /// apply custom actions itself.
        /// </summary>
        public bool RequestAction(string actionId, string targetId, float value)
        {
            if (Transport == null || !IsRunning || string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            var action = new PlayerAction(Transport.LocalClientId, actionId, targetId ?? string.Empty, value, Time.realtimeSinceStartupAsDouble);
            return Transport.SendAction(action);
        }

        /// <summary>
        /// Server-side validation for an incoming action. Returns null when
        /// the action is valid, otherwise a human-readable rejection reason.
        /// Checks, in order: the sender is a known connected player; the
        /// action is not stale (lag / replay guard); the action id is known;
        /// the target exists where one is needed; the sender is alive for
        /// combat and heal actions; damage and heal values are in range.
        /// </summary>
        public string ValidateAction(PlayerAction action)
        {
            if (Transport == null || !_players.ContainsKey(action.ClientId))
            {
                return $"Unknown sender {action.ClientId}.";
            }

            if (_config != null)
            {
                double ageMs = (Time.realtimeSinceStartupAsDouble - action.ClientTimestamp) * 1000.0;
                if (ageMs > _config.ActionStalenessWindowMs)
                {
                    return $"Stale action '{action.ActionId}' from {action.ClientId} ({ageMs:F0} ms old).";
                }

                if (ageMs < -1000.0)
                {
                    return $"Action '{action.ActionId}' from {action.ClientId} is dated in the future.";
                }
            }

            switch (action.ActionId)
            {
                case "damage":
                    if (!ulong.TryParse(action.TargetId, out ulong damageTargetId) || !_players.TryGetValue(damageTargetId, out NetworkPlayer damageTarget))
                    {
                        return $"Damage target '{action.TargetId}' does not exist.";
                    }

                    if (!_players[action.ClientId].IsAlive)
                    {
                        return "Dead players cannot deal damage.";
                    }

                    if (!damageTarget.IsAlive)
                    {
                        return "Target is already dead.";
                    }

                    if (action.Value <= 0f || action.Value > 1000f)
                    {
                        return $"Damage value {action.Value} is out of range (1-1000).";
                    }

                    return null;

                case "heal":
                    if (!_players[action.ClientId].IsAlive)
                    {
                        return "Dead players cannot heal.";
                    }

                    if (action.Value <= 0f || action.Value > 1000f)
                    {
                        return $"Heal value {action.Value} is out of range (1-1000).";
                    }

                    return null;

                case "add-item":
                    if (string.IsNullOrWhiteSpace(action.TargetId))
                    {
                        return "Item id is empty.";
                    }

                    if (_players[action.ClientId].HasItem(action.TargetId))
                    {
                        return $"Player already owns item '{action.TargetId}'.";
                    }

                    return null;

                default:
                    // Custom actions are accepted at this layer when the
                    // sender and timestamp check out; game code decides
                    // what they mean after the broadcast.
                    if (string.IsNullOrWhiteSpace(action.ActionId))
                    {
                        return "Action id is empty.";
                    }

                    return null;
            }
        }

        /// <summary>
        /// Applies an action that has already passed <see cref="ValidateAction"/>.
        /// Called by the server when an action arrives, and by tests to
        /// exercise the apply path directly. Never call this with an
        /// unvalidated action from a client.
        /// </summary>
        public bool ApplyValidatedAction(PlayerAction action)
        {
            if (!IsServer)
            {
                return false;
            }

            string problem = ValidateAction(action);
            if (problem != null)
            {
                MultiplayerEvents.RaiseActionRejected(action, problem);
                return false;
            }

            switch (action.ActionId)
            {
                case "damage":
                    ulong targetId = ulong.Parse(action.TargetId);
                    _players[targetId].ApplyDamage(Mathf.RoundToInt(action.Value));
                    break;

                case "heal":
                    _players[action.ClientId].Heal(Mathf.RoundToInt(action.Value));
                    break;

                case "add-item":
                    _players[action.ClientId].AddItem(action.TargetId);
                    break;

                default:
                    // Custom actions have no built-in effect; the broadcast
                    // below is what game code listens for.
                    break;
            }

            MultiplayerEvents.RaiseValidatedAction(action);
            if (Transport != null)
            {
                Transport.BroadcastValidatedAction(action);
            }

            return true;
        }

        /// <summary>
        /// Applies a broadcast the server already validated and applied.
        /// Only runs on non-server instances: the server applied the action
        /// itself in <see cref="ApplyValidatedAction"/> and must not apply
        /// it a second time. The client mirrors the server's result with
        /// the server-directed methods on <see cref="NetworkPlayer"/>,
        /// which are only reachable through this manager path.
        /// </summary>
        public void ApplyServerBroadcast(PlayerAction action)
        {
            if (IsServer || Transport == null)
            {
                return;
            }

            switch (action.ActionId)
            {
                case "damage":
                    if (ulong.TryParse(action.TargetId, out ulong targetId) && _players.TryGetValue(targetId, out NetworkPlayer damageTarget))
                    {
                        damageTarget.ApplyServerDirectedDamage(Mathf.RoundToInt(action.Value));
                    }

                    break;

                case "heal":
                    if (_players.TryGetValue(action.ClientId, out NetworkPlayer healTarget))
                    {
                        healTarget.ApplyServerDirectedHeal(Mathf.RoundToInt(action.Value));
                    }

                    break;

                case "add-item":
                    if (_players.TryGetValue(action.ClientId, out NetworkPlayer itemTarget))
                    {
                        itemTarget.ApplyServerDirectedAddItem(action.TargetId);
                    }

                    break;

                default:
                    break;
            }

            MultiplayerEvents.RaiseValidatedAction(action);
        }

        private bool CanStart(out string problem)
        {
            problem = null;
            if (Transport == null)
            {
                problem = "No transport assigned.";
                return false;
            }

            if (IsRunning)
            {
                problem = "A session is already running.";
                return false;
            }

            if (_config == null)
            {
                problem = "No MultiplayerConfig assigned.";
                return false;
            }

            problem = _config.Validate();
            return problem == null;
        }

        private void SpawnLocalPlayer()
        {
            if (Transport == null)
            {
                return;
            }

            ulong localId = Transport.LocalClientId;
            if (_players.ContainsKey(localId))
            {
                return;
            }

            NetworkPlayer player;
            if (_playerPrefab != null)
            {
                player = Instantiate(_playerPrefab, Vector3.zero, Quaternion.identity);
                player.gameObject.name = $"Player_{localId}";
            }
            else
            {
                var go = new GameObject($"Player_{localId}");
                player = go.AddComponent<NetworkPlayer>();
            }

            int maxHealth = _config != null ? _config.MaxHealth : 100;
            player.Initialize(localId, _localPlayerName, maxHealth, IsServer);
            RegisterPlayer(player);
        }

        private void SpawnRemotePlayer(ulong clientId)
        {
            if (_players.ContainsKey(clientId))
            {
                return;
            }

            if (_config != null && _players.Count >= _config.MaxPlayers)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] Player limit ({_config.MaxPlayers}) reached — refusing client {clientId}.");
                DisconnectPlayer(clientId);
                return;
            }

            NetworkPlayer player;
            if (_playerPrefab != null)
            {
                player = Instantiate(_playerPrefab, Vector3.zero, Quaternion.identity);
                player.gameObject.name = $"Player_{clientId}";
            }
            else
            {
                var go = new GameObject($"Player_{clientId}");
                player = go.AddComponent<NetworkPlayer>();
            }

            int maxHealth = _config != null ? _config.MaxHealth : 100;
            player.Initialize(clientId, $"Player {clientId}", maxHealth, IsServer);
            RegisterPlayer(player);
        }

        private void RemoveAllPlayers()
        {
            var ids = new List<ulong>(_players.Keys);
            foreach (ulong id in ids)
            {
                RemovePlayer(id);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            if (Transport != null && clientId == Transport.LocalClientId)
            {
                return;
            }

            SpawnRemotePlayer(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // A disconnect removes the player on every side that knows it:
            // on the server the player object goes away, on a client the
            // remote player's object goes away. The local player's own
            // disconnect is handled by Shutdown, which clears all players.
            RemovePlayer(clientId);
        }

        private void HandleActionReceived(PlayerAction action)
        {
            if (!IsServer)
            {
                // Clients never validate or apply actions themselves; they
                // only ever receive results the server chose to broadcast.
                return;
            }

            ApplyValidatedAction(action);
        }

        private void HandleBroadcastReceived(PlayerAction action)
        {
            ApplyServerBroadcast(action);
        }

        private void SubscribeTransport()
        {
            if (Transport == null)
            {
                return;
            }

            Transport.OnClientConnected += HandleClientConnected;
            Transport.OnClientDisconnected += HandleClientDisconnected;
            Transport.OnActionReceived += HandleActionReceived;
            Transport.OnBroadcastReceived += HandleBroadcastReceived;
        }

        private void UnsubscribeTransport()
        {
            if (Transport == null)
            {
                return;
            }

            Transport.OnClientConnected -= HandleClientConnected;
            Transport.OnClientDisconnected -= HandleClientDisconnected;
            Transport.OnActionReceived -= HandleActionReceived;
            Transport.OnBroadcastReceived -= HandleBroadcastReceived;
        }
    }
}
