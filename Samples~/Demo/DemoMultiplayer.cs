using UnityEngine;
using Sdx17.Multiplayer;

namespace Sdx17.Multiplayer.Samples
{
    /// <summary>
    /// Minimal demo for the Multiplayer Architecture package.
    /// Attach it to a GameObject in a demo scene next to a
    /// MultiplayerManager (see Samples~/Demo/README.md for the full setup).
    /// Keys, all in Play mode:
    ///   H — start as host (server + local client)
    ///   C — start as client (loopback session, no real network)
    ///   S — start as dedicated server (no local player)
    ///   X — request 10 damage to the local player (server-validated)
    ///   G — request a 10-point heal for the local player
    ///   I — request the item "health-potion" for the local player
    ///   Q — shut the session down
    /// Everything is logged to the Console, including the player list
    /// whenever a player spawns or leaves.
    /// </summary>
    public sealed class DemoMultiplayer : MonoBehaviour
    {
        [SerializeField] private MultiplayerManager _manager;

        public MultiplayerManager Manager => _manager;

        private void Awake()
        {
            if (_manager == null)
            {
                _manager = FindObjectOfType<MultiplayerManager>();
            }

            if (_manager == null)
            {
                Debug.LogError("[Sdx17.Multiplayer Demo] No MultiplayerManager found. Add one to the scene (Sdx17 > Multiplayer > Create Multiplayer Manager) and assign a MultiplayerConfig to it.");
            }
        }

        private void OnEnable()
        {
            MultiplayerEvents.OnPlayerSpawned += HandlePlayerSpawned;
            MultiplayerEvents.OnPlayerLeft += HandlePlayerLeft;
            MultiplayerEvents.OnHealthChanged += HandleHealthChanged;
            MultiplayerEvents.OnValidatedAction += HandleValidatedAction;
            MultiplayerEvents.OnActionRejected += HandleActionRejected;
        }

        private void OnDisable()
        {
            MultiplayerEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            MultiplayerEvents.OnPlayerLeft -= HandlePlayerLeft;
            MultiplayerEvents.OnHealthChanged -= HandleHealthChanged;
            MultiplayerEvents.OnValidatedAction -= HandleValidatedAction;
            MultiplayerEvents.OnActionRejected -= HandleActionRejected;
        }

        private void Update()
        {
            if (_manager == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.H))
            {
                LogResult("StartHost", _manager.StartHost());
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                LogResult("StartClient", _manager.StartClient());
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                LogResult("StartServer", _manager.StartServer());
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                _manager.Shutdown();
                Debug.Log("[Sdx17.Multiplayer Demo] Session shut down.");
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                RequestDamageToLocalPlayer();
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                LogResult("RequestHeal(10)", _manager.RequestHeal(10));
            }
            else if (Input.GetKeyDown(KeyCode.I))
            {
                LogResult("RequestAddItem(health-potion)", _manager.RequestAddItem("health-potion"));
            }
        }

        private void RequestDamageToLocalPlayer()
        {
            NetworkPlayer local = _manager.LocalPlayer;
            if (local == null)
            {
                Debug.LogWarning("[Sdx17.Multiplayer Demo] No local player yet. Start a host or client session first (H or C).");
                return;
            }

            LogResult($"RequestDamage(target={local.ClientId}, 10)", _manager.RequestDamage(local.ClientId, 10));
        }

        private void LogResult(string action, bool ok)
        {
            Debug.Log($"[Sdx17.Multiplayer Demo] {action}: {(ok ? "ok" : "refused — see the warning above")}.");
        }

        private void HandlePlayerSpawned(NetworkPlayer player)
        {
            if (player == null)
            {
                return;
            }

            Debug.Log($"[Sdx17.Multiplayer Demo] Player spawned: {player.PlayerName} (client {player.ClientId}), health {player.Health}/{player.MaxHealth}. Players in session: {(_manager != null ? _manager.Players.Count : 0)}.");
        }

        private void HandlePlayerLeft(ulong clientId, string playerName)
        {
            Debug.Log($"[Sdx17.Multiplayer Demo] Player left: {playerName} (client {clientId}). Players in session: {(_manager != null ? _manager.Players.Count : 0)}.");
        }

        private void HandleHealthChanged(NetworkPlayer player, int health, int maxHealth)
        {
            if (player == null)
            {
                return;
            }

            Debug.Log($"[Sdx17.Multiplayer Demo] Health changed: {player.PlayerName} is now {health}/{maxHealth}.");
        }

        private void HandleValidatedAction(PlayerAction action)
        {
            Debug.Log($"[Sdx17.Multiplayer Demo] Server validated action '{action.ActionId}' from client {action.ClientId} (target '{action.TargetId}', value {action.Value}).");
        }

        private void HandleActionRejected(PlayerAction action, string reason)
        {
            Debug.LogWarning($"[Sdx17.Multiplayer Demo] Server rejected action '{action.ActionId}' from client {action.ClientId}: {reason}");
        }
    }
}
