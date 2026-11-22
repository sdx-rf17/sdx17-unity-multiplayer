using System;
using UnityEngine;
using UnityEngine.Events;

namespace Sdx17.Multiplayer
{
    /// <summary>
    /// One hub for multiplayer events so UI, audio and game systems can
    /// subscribe without holding a reference to the manager or to a player.
    /// All events fire on the Unity main thread. The server is the only
    /// side that raises state-changing events; clients receive them as
    /// the result of a server broadcast, never from a remote peer directly.
    /// </summary>
    public static class MultiplayerEvents
    {
        /// <summary>Raised when a player object is spawned and registered. Argument: the player.</summary>
        public static event Action<NetworkPlayer> OnPlayerSpawned;

        /// <summary>Raised when a player leaves or is disconnected. Arguments: client id, player name.</summary>
        public static event Action<ulong, string> OnPlayerLeft;

        /// <summary>Raised when a player's health changes. Arguments: player, new health, max health.</summary>
        public static event Action<NetworkPlayer, int, int> OnHealthChanged;

        /// <summary>Raised when a player's inventory changes. Arguments: player, item id, added (true) or removed (false).</summary>
        public static event Action<NetworkPlayer, string, bool> OnInventoryChanged;

        /// <summary>Raised after the server validated an action and broadcast it. Argument: the validated action.</summary>
        public static event Action<PlayerAction> OnValidatedAction;

        /// <summary>Raised when the server rejects an action. Arguments: action, reason.</summary>
        public static event Action<PlayerAction, string> OnActionRejected;

        /// <summary>Raised when the connection state changes. Arguments: mode, running (true) or stopped (false).</summary>
        public static event Action<ConnectionMode, bool> OnConnectionChanged;

        internal static void RaisePlayerSpawned(NetworkPlayer player)
        {
            if (player != null) OnPlayerSpawned?.Invoke(player);
        }

        internal static void RaisePlayerLeft(ulong clientId, string playerName)
        {
            OnPlayerLeft?.Invoke(clientId, playerName ?? string.Empty);
        }

        internal static void RaiseHealthChanged(NetworkPlayer player, int health, int maxHealth)
        {
            if (player != null) OnHealthChanged?.Invoke(player, health, maxHealth);
        }

        internal static void RaiseInventoryChanged(NetworkPlayer player, string itemId, bool added)
        {
            if (player != null && itemId != null && itemId.Length > 0) OnInventoryChanged?.Invoke(player, itemId, added);
        }

        internal static void RaiseValidatedAction(PlayerAction action)
        {
            OnValidatedAction?.Invoke(action);
        }

        internal static void RaiseActionRejected(PlayerAction action, string reason)
        {
            OnActionRejected?.Invoke(action, reason ?? "Rejected.");
        }

        internal static void RaiseConnectionChanged(ConnectionMode mode, bool running)
        {
            OnConnectionChanged?.Invoke(mode, running);
        }

        /// <summary>
        /// Clears every subscriber. Call from a test or a scene teardown so
        /// a destroyed scene cannot leave stale listeners behind.
        /// </summary>
        public static void ClearAllListeners()
        {
            OnPlayerSpawned = null;
            OnPlayerLeft = null;
            OnHealthChanged = null;
            OnInventoryChanged = null;
            OnValidatedAction = null;
            OnActionRejected = null;
            OnConnectionChanged = null;
        }
    }

    /// <summary>
    /// Inspector-friendly UnityEvent versions of the same signals, for
    /// wiring UI in the Editor without writing code. Attach
    /// <see cref="MultiplayerEventBridge"/> behaviour is not needed:
    /// add this component to any GameObject and wire its events in the Inspector.
    /// </summary>
    public sealed class MultiplayerEventBridge : MonoBehaviour
    {
        [SerializeField] private UnityEvent<NetworkPlayer> _onPlayerSpawned = new UnityEvent<NetworkPlayer>();
        [SerializeField] private UnityEvent<string> _onPlayerLeft = new UnityEvent<string>();
        [SerializeField] private UnityEvent _onConnected = new UnityEvent();
        [SerializeField] private UnityEvent _onDisconnected = new UnityEvent();

        public UnityEvent<NetworkPlayer> OnPlayerSpawned => _onPlayerSpawned;
        public UnityEvent<string> OnPlayerLeft => _onPlayerLeft;
        public UnityEvent OnConnected => _onConnected;
        public UnityEvent OnDisconnected => _onDisconnected;

        private void OnEnable()
        {
            MultiplayerEvents.OnPlayerSpawned += HandlePlayerSpawned;
            MultiplayerEvents.OnPlayerLeft += HandlePlayerLeft;
            MultiplayerEvents.OnConnectionChanged += HandleConnectionChanged;
        }

        private void OnDisable()
        {
            MultiplayerEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            MultiplayerEvents.OnPlayerLeft -= HandlePlayerLeft;
            MultiplayerEvents.OnConnectionChanged -= HandleConnectionChanged;
        }

        private void HandlePlayerSpawned(NetworkPlayer player)
        {
            _onPlayerSpawned?.Invoke(player);
        }

        private void HandlePlayerLeft(ulong clientId, string playerName)
        {
            _onPlayerLeft?.Invoke(playerName);
        }

        private void HandleConnectionChanged(ConnectionMode mode, bool running)
        {
            if (running)
            {
                _onConnected?.Invoke();
            }
            else
            {
                _onDisconnected?.Invoke();
            }
        }
    }
}
