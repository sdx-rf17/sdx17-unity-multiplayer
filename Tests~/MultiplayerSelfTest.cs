using System.Collections;
using UnityEngine;
using Sdx17.Multiplayer;

namespace Sdx17.Multiplayer.Tests
{
    /// <summary>
    /// Play-mode self-test for the Multiplayer Architecture package.
    /// Attach it to a GameObject in a test scene, press Play, and read the
    /// Console. Every check logs PASS or FAIL by name, then a summary line
    /// with the pass count. Nothing here needs the Netcode package or a
    /// network: it runs against <see cref="LocalLoopbackTransport"/>.
    /// The test creates its own manager, config and players at runtime and
    /// destroys them when it finishes, so it does not touch your scene.
    /// </summary>
    public sealed class MultiplayerSelfTest : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When true the test starts automatically on Play. Turn it off and call RunAllTests from another script or a UnityEvent instead.")]
        private bool _runOnStart = true;

        private int _passed;
        private int _failed;
        private MultiplayerManager _manager;
        private GameObject _managerObject;

        private void Start()
        {
            if (_runOnStart)
            {
                StartCoroutine(RunAllTests());
            }
        }

        /// <summary>
        /// Runs every check in order and logs a PASS/FAIL line per check
        /// plus a summary. Safe to call more than once; each run builds a
        /// fresh manager and tears it down at the end.
        /// </summary>
        public IEnumerator RunAllTests()
        {
            _passed = 0;
            _failed = 0;
            SetupManager();

            Check("ConfigValid", _manager.Config != null && _manager.Config.IsValid && _manager.Config.ServerPort == 7777 && _manager.Config.MaxPlayers == 8);
            Check("ConfigCloneForLocalDemo", _manager.Config.CloneForLocalDemo().ServerAddress == "127.0.0.1" && _manager.Config.CloneForLocalDemo().ServerAuthoritative);
            Check("StartHost", _manager.StartHost() && _manager.IsRunning && _manager.IsServer && _manager.IsHost);
            Check("LocalPlayerSpawned", _manager.LocalPlayer != null && _manager.LocalPlayer.Health == 100 && _manager.LocalPlayer.IsAlive);
            Check("PlayerListHasLocalPlayer", _manager.Players.ContainsKey(_manager.LocalClientId));

            // Server-validated damage: the request goes through the
            // transport, the server validates it and applies it.
            bool damageSent = _manager.RequestDamage(_manager.LocalClientId, 25);
            Check("ValidatedDamageApplies", damageSent && _manager.LocalPlayer.Health == 75);

            // A request with an out-of-range value must be refused.
            Check("InvalidDamageRefused", !_manager.RequestDamage(_manager.LocalClientId, 0) && _manager.LocalPlayer.Health == 75);

            // A client-side player must not be able to change its own health.
            var clientObject = new GameObject("SelfTest_ClientPlayer");
            var clientPlayer = clientObject.AddComponent<NetworkPlayer>();
            clientPlayer.Initialize(99, "ClientPlayer", 100, false);
            Check("ClientCannotDirectDamage", !clientPlayer.ApplyDamage(10) && clientPlayer.Health == 100);
            Check("ClientCannotDirectAddItem", !clientPlayer.AddItem("sword") && !clientPlayer.HasItem("sword"));
            Destroy(clientObject);

            // Server-side inventory: add works, duplicates are rejected,
            // removal works.
            NetworkPlayer serverPlayer = _manager.LocalPlayer;
            Check("ServerAddItem", serverPlayer.AddItem("sword") && serverPlayer.HasItem("sword"));
            Check("DuplicateItemRejected", !serverPlayer.AddItem("sword") && serverPlayer.Inventory.Count == 1);
            Check("ServerRemoveItem", serverPlayer.RemoveItem("sword") && !serverPlayer.HasItem("sword"));

            // Validation: an unknown sender and a stale action are rejected.
            var unknownSender = new PlayerAction(4242, "damage", _manager.LocalClientId.ToString(), 10f, Time.realtimeSinceStartupAsDouble);
            Check("UnknownSenderRejected", _manager.ValidateAction(unknownSender) != null);
            var stale = new PlayerAction(_manager.LocalClientId, "damage", _manager.LocalClientId.ToString(), 10f, Time.realtimeSinceStartupAsDouble - 60.0);
            Check("StaleActionRejected", _manager.ValidateAction(stale) != null);

            // Heal through the validated path.
            bool healSent = _manager.RequestHeal(10);
            Check("ValidatedHealApplies", healSent && _manager.LocalPlayer.Health == 85);

            // Simulated remote client joins, then is disconnected by the server.
            var loopback = _manager.Transport as LocalLoopbackTransport;
            ulong remoteId = loopback != null ? loopback.SimulateRemoteClientJoin() : ulong.MaxValue;
            Check("RemoteClientJoins", remoteId != ulong.MaxValue && _manager.Players.ContainsKey(remoteId));
            Check("DisconnectRemovesPlayer", remoteId != ulong.MaxValue && _manager.DisconnectPlayer(remoteId) && !_manager.Players.ContainsKey(remoteId));

            // Shutdown empties the player list and stops the session.
            _manager.Shutdown();
            Check("ShutdownClearsPlayers", !_manager.IsRunning && _manager.Players.Count == 0);

            TeardownManager();
            Debug.Log($"[Sdx17.Multiplayer SelfTest] Finished: {_passed} passed, {_failed} failed.");
            yield return null;
        }

        private void SetupManager()
        {
            _managerObject = new GameObject("SelfTest_MultiplayerManager");
            _manager = _managerObject.AddComponent<MultiplayerManager>();
            var config = ScriptableObject.CreateInstance<MultiplayerConfig>();
            _manager.SetConfig(config);
        }

        private void TeardownManager()
        {
            if (_managerObject != null)
            {
                Destroy(_managerObject);
            }

            _manager = null;
            _managerObject = null;
            MultiplayerEvents.ClearAllListeners();
        }

        private void Check(string testName, bool passed)
        {
            if (passed)
            {
                _passed++;
                Debug.Log($"[Sdx17.Multiplayer SelfTest] PASS: {testName}");
            }
            else
            {
                _failed++;
                Debug.LogError($"[Sdx17.Multiplayer SelfTest] FAIL: {testName}");
            }
        }
    }
}
