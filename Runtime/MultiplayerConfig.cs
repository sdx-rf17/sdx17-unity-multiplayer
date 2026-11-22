using UnityEngine;

namespace Sdx17.Multiplayer
{
    /// <summary>
    /// All tunable multiplayer settings in one asset, so a designer can
    /// change address, port and player limits without touching code.
    /// Create one with: Sdx17 > Multiplayer > Create Multiplayer Config,
    /// or right-click in the Project window > Create > Sdx17 > Multiplayer Config.
    /// </summary>
    [CreateAssetMenu(fileName = "MultiplayerConfig", menuName = "Sdx17/Multiplayer Config", order = 0)]
    public sealed class MultiplayerConfig : ScriptableObject
    {
        [Header("Connection")]
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private ushort _serverPort = 7777;
        [SerializeField] private float _connectionTimeoutSeconds = 10f;

        [Header("Session")]
        [SerializeField] private int _maxPlayers = 8;
        [SerializeField] private int _maxHealth = 100;

        [Header("Authority")]
        [SerializeField]
        [Tooltip("When true, only the server may change health and inventory. Clients send requests and the server validates them. Keep this on for any real game; turning it off lets any client change its own state, which is only useful for a local demo.")]
        private bool _serverAuthoritative = true;

        [Header("Lag")]
        [SerializeField]
        [Tooltip("How far back in time the server looks when validating a timed action, in milliseconds. Actions older than this window are rejected as stale, which stops replayed or badly delayed requests from applying twice.")]
        private float _actionStalenessWindowMs = 5000f;

        [SerializeField]
        [Tooltip("How often the server sends a heartbeat, in seconds. A missed heartbeat past the connection timeout counts as a disconnect.")]
        private float _heartbeatIntervalSeconds = 2f;

        public string ServerAddress => _serverAddress;
        public ushort ServerPort => _serverPort;
        public float ConnectionTimeoutSeconds => _connectionTimeoutSeconds;
        public int MaxPlayers => _maxPlayers;
        public int MaxHealth => _maxHealth;
        public bool ServerAuthoritative => _serverAuthoritative;
        public float ActionStalenessWindowMs => _actionStalenessWindowMs;
        public float HeartbeatIntervalSeconds => _heartbeatIntervalSeconds;

        /// <summary>
        /// Returns a config with the same values as this one but safe for a
        /// local demo: loopback address and server authority kept on.
        /// </summary>
        public MultiplayerConfig CloneForLocalDemo()
        {
            MultiplayerConfig clone = CreateInstance<MultiplayerConfig>();
            clone._serverAddress = "127.0.0.1";
            clone._serverPort = _serverPort;
            clone._connectionTimeoutSeconds = _connectionTimeoutSeconds;
            clone._maxPlayers = _maxPlayers;
            clone._maxHealth = _maxHealth;
            clone._serverAuthoritative = true;
            clone._actionStalenessWindowMs = _actionStalenessWindowMs;
            clone._heartbeatIntervalSeconds = _heartbeatIntervalSeconds;
            return clone;
        }

        /// <summary>
        /// Checks every value and returns a human-readable problem, or null
        /// when the config is usable. Call before starting a session so a
        /// bad config fails loudly in the Editor instead of silently at runtime.
        /// </summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(_serverAddress))
            {
                return "Server address is empty.";
            }

            if (_serverPort == 0)
            {
                return "Server port is 0. Use a port such as 7777.";
            }

            if (_maxPlayers < 1)
            {
                return "Max players must be at least 1.";
            }

            if (_maxPlayers > 64)
            {
                return "Max players is above 64, which this package does not support.";
            }

            if (_maxHealth < 1)
            {
                return "Max health must be at least 1.";
            }

            if (_connectionTimeoutSeconds <= 0f)
            {
                return "Connection timeout must be greater than 0.";
            }

            if (_actionStalenessWindowMs < 0f)
            {
                return "Action staleness window must not be negative.";
            }

            if (_heartbeatIntervalSeconds <= 0f)
            {
                return "Heartbeat interval must be greater than 0.";
            }

            return null;
        }

        public bool IsValid => Validate() == null;

        private void OnValidate()
        {
            // Clamp in the Editor so an accidental drag in the Inspector
            // can never produce a config that crashes a session at runtime.
            if (_maxPlayers < 1) _maxPlayers = 1;
            if (_maxPlayers > 64) _maxPlayers = 64;
            if (_maxHealth < 1) _maxHealth = 1;
            if (_connectionTimeoutSeconds <= 0f) _connectionTimeoutSeconds = 10f;
            if (_actionStalenessWindowMs < 0f) _actionStalenessWindowMs = 0f;
            if (_heartbeatIntervalSeconds <= 0f) _heartbeatIntervalSeconds = 2f;
            if (string.IsNullOrWhiteSpace(_serverAddress)) _serverAddress = "127.0.0.1";
        }
    }
}
