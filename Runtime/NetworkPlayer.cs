using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Sdx17.Multiplayer
{
    /// <summary>
    /// One player in a multiplayer session.
    /// Security model: health and inventory are server state. A client may
    /// ask for a change (through <see cref="MultiplayerManager"/>), but only
    /// the server applies it, and only the server's copy is broadcast to
    /// other clients. Direct calls to <see cref="ApplyDamage"/> or
    /// <see cref="AddItem"/> from a client are refused unless the instance
    /// is running as the server — see <see cref="IsServerAuthoritativeInstance"/>.
    /// Sync in this package is event-based: health and inventory changes
    /// raise events that the UI and other systems listen to, instead of
    /// polling values every frame. When the Netcode adapter is used, the
    /// manager forwards those same changes over the network; this class
    /// itself holds no networking library references.
    /// </summary>
    public sealed class NetworkPlayer : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private ulong _clientId;
        [SerializeField] private string _playerName = "Player";

        [Header("Health")]
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _health = 100;

        [Header("Authority")]
        [SerializeField]
        [Tooltip("True when this instance is running on the server (or host). Only then may health and inventory be changed directly. Set by MultiplayerManager on spawn; set it by hand in a test only.")]
        private bool _isServerAuthoritativeInstance;

        [SerializeField] private UnityEvent<int, int> _onHealthChanged = new UnityEvent<int, int>();
        [SerializeField] private UnityEvent<string, bool> _onInventoryChanged = new UnityEvent<string, bool>();

        /// <summary>Fired on this player when health changes. Arguments: new health, max health.</summary>
        public event Action<int, int> HealthChanged;

        /// <summary>Fired on this player when inventory changes. Arguments: item id, added (true) or removed (false).</summary>
        public event Action<string, bool> InventoryChanged;

        private readonly List<string> _inventory = new List<string>();
        private readonly HashSet<string> _inventorySet = new HashSet<string>(StringComparer.Ordinal);

        public ulong ClientId => _clientId;
        public string PlayerName => _playerName;
        public int MaxHealth => _maxHealth;
        public int Health => _health;
        public bool IsAlive => _health > 0;
        public bool IsServerAuthoritativeInstance => _isServerAuthoritativeInstance;
        public IReadOnlyList<string> Inventory => _inventory;

        public UnityEvent<int, int> OnHealthChangedEvent => _onHealthChanged;
        public UnityEvent<string, bool> OnInventoryChangedEvent => _onInventoryChanged;

        /// <summary>
        /// Sets up a freshly spawned player. Called by
        /// <see cref="MultiplayerManager"/>; call it by hand only in a test.
        /// </summary>
        public void Initialize(ulong clientId, string playerName, int maxHealth, bool isServerAuthoritativeInstance)
        {
            _clientId = clientId;
            _playerName = string.IsNullOrWhiteSpace(playerName) ? $"Player {clientId}" : playerName;
            _maxHealth = Mathf.Max(1, maxHealth);
            _health = _maxHealth;
            _isServerAuthoritativeInstance = isServerAuthoritativeInstance;
            _inventory.Clear();
            _inventorySet.Clear();
        }

        /// <summary>
        /// Marks this instance as server-authoritative (or not). The manager
        /// calls this on spawn; tests use it to exercise both sides.
        /// </summary>
        public void SetServerAuthority(bool isServer)
        {
            _isServerAuthoritativeInstance = isServer;
        }

        /// <summary>
        /// Server only. Reduces health by <paramref name="damage"/> and
        /// clamps at zero. Returns false and changes nothing when called on
        /// a client, when the player is already dead, or when damage is not
        /// positive — a client asking for damage must go through the
        /// manager's validated-action path instead.
        /// </summary>
        public bool ApplyDamage(int damage)
        {
            if (!_isServerAuthoritativeInstance)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetworkPlayer '{_playerName}': ApplyDamage refused — this instance is not the server. Use MultiplayerManager.RequestDamage instead.");
                return false;
            }

            if (!IsAlive || damage <= 0)
            {
                return false;
            }

            _health = Mathf.Max(0, _health - damage);
            NotifyHealthChanged();
            return true;
        }

        /// <summary>
        /// Server only. Restores health, clamped at max health. Returns
        /// false and changes nothing on a client or when the amount is not
        /// positive or the player is dead.
        /// </summary>
        public bool Heal(int amount)
        {
            if (!_isServerAuthoritativeInstance)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetworkPlayer '{_playerName}': Heal refused — this instance is not the server.");
                return false;
            }

            if (!IsAlive || amount <= 0 || _health >= _maxHealth)
            {
                return false;
            }

            _health = Mathf.Min(_maxHealth, _health + amount);
            NotifyHealthChanged();
            return true;
        }

        /// <summary>
        /// Server only. Restores full health. Returns false on a client or
        /// when the player is already at full health.
        /// </summary>
        public bool Respawn()
        {
            if (!_isServerAuthoritativeInstance)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetworkPlayer '{_playerName}': Respawn refused — this instance is not the server.");
                return false;
            }

            if (_health >= _maxHealth)
            {
                return false;
            }

            _health = _maxHealth;
            NotifyHealthChanged();
            return true;
        }

        /// <summary>
        /// Server only. Adds an item id to the inventory. Duplicate ids are
        /// rejected so a repeated (or replayed) request cannot stack the
        /// same item twice. Returns false on a client, for an empty id, or
        /// for a duplicate.
        /// </summary>
        public bool AddItem(string itemId)
        {
            if (!_isServerAuthoritativeInstance)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetworkPlayer '{_playerName}': AddItem refused — this instance is not the server.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (!_inventorySet.Add(itemId))
            {
                return false;
            }

            _inventory.Add(itemId);
            NotifyInventoryChanged(itemId, true);
            return true;
        }

        /// <summary>
        /// Server only. Removes an item id from the inventory. Returns false
        /// on a client, for an empty id, or when the player does not own it.
        /// </summary>
        public bool RemoveItem(string itemId)
        {
            if (!_isServerAuthoritativeInstance)
            {
                Debug.LogWarning($"[Sdx17.Multiplayer] NetworkPlayer '{_playerName}': RemoveItem refused — this instance is not the server.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId) || !_inventorySet.Remove(itemId))
            {
                return false;
            }

            _inventory.Remove(itemId);
            NotifyInventoryChanged(itemId, false);
            return true;
        }

        public bool HasItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && _inventorySet.Contains(itemId);
        }

        /// <summary>
        /// Applies a health value received from the server. This is the one
        /// path a client uses to update health: the value arrives from the
        /// server, it is clamped to a sane range, and it is never taken from
        /// another client. Callers outside the manager should not use this.
        /// </summary>
        public void ApplyServerHealth(int serverHealth)
        {
            _health = Mathf.Clamp(serverHealth, 0, _maxHealth);
            NotifyHealthChanged();
        }

        /// <summary>
        /// Applies damage the server already validated and broadcast.
        /// Only the local <see cref="MultiplayerManager"/> calls this, when
        /// handling a server broadcast — never in response to a remote
        /// client. That is why it bypasses the server-authority check that
        /// <see cref="ApplyDamage"/> enforces: the decision was already
        /// made by the server, this only mirrors the result locally.
        /// </summary>
        public void ApplyServerDirectedDamage(int damage)
        {
            if (!IsAlive || damage <= 0)
            {
                return;
            }

            _health = Mathf.Max(0, _health - damage);
            NotifyHealthChanged();
        }

        /// <summary>
        /// Same rule as <see cref="ApplyServerDirectedDamage"/>: mirrors a
        /// heal the server already validated and broadcast.
        /// </summary>
        public void ApplyServerDirectedHeal(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            _health = Mathf.Min(_maxHealth, _health + amount);
            NotifyHealthChanged();
        }

        /// <summary>
        /// Same rule as <see cref="ApplyServerDirectedDamage"/>: mirrors an
        /// item grant the server already validated and broadcast.
        /// </summary>
        public void ApplyServerDirectedAddItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !_inventorySet.Add(itemId))
            {
                return;
            }

            _inventory.Add(itemId);
            NotifyInventoryChanged(itemId, true);
        }

        /// <summary>
        /// Replaces the inventory with the list the server sent. Same rule
        /// as <see cref="ApplyServerHealth"/>: server data only, duplicates
        /// and empty ids dropped.
        /// </summary>
        public void ApplyServerInventory(IEnumerable<string> serverInventory)
        {
            if (serverInventory == null)
            {
                return;
            }

            _inventory.Clear();
            _inventorySet.Clear();
            foreach (string itemId in serverInventory)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    continue;
                }

                if (_inventorySet.Add(itemId))
                {
                    _inventory.Add(itemId);
                }
            }
        }

        private void NotifyHealthChanged()
        {
            _onHealthChanged?.Invoke(_health, _maxHealth);
            HealthChanged?.Invoke(_health, _maxHealth);
            MultiplayerEvents.RaiseHealthChanged(this, _health, _maxHealth);
        }

        private void NotifyInventoryChanged(string itemId, bool added)
        {
            _onInventoryChanged?.Invoke(itemId, added);
            InventoryChanged?.Invoke(itemId, added);
            MultiplayerEvents.RaiseInventoryChanged(this, itemId, added);
        }
    }
}
