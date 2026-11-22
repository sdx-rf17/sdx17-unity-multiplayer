# Changelog

All notable changes to this package are documented here.
The format follows Keep a Changelog, and this project uses Semantic Versioning.

## [0.1.0] - 2026-11-21

### Added

- `ITransport` abstraction plus `LocalLoopbackTransport`, an in-memory transport for offline development and Play-mode self-tests with no extra packages.
- `MultiplayerConfig` ScriptableObject (address, port, max players, timeouts, server-authority flag) with `OnValidate` clamping.
- `MultiplayerManager`: host / client / server modes, player list, spawn and disconnect handling, and the server-validated action pattern (client requests, server validates, server broadcasts).
- `NetworkPlayer`: spawn, health sync and inventory sync through C# events and UnityEvents, with server-only damage and item changes.
- `MultiplayerEvents`: a single event hub for player joined / left, health changed, inventory changed and validated actions.
- `Runtime/Netcode/NetcodeTransportAdapter`: an optional Netcode for GameObjects adapter, guarded so it only compiles when the Netcode package is present.
- Editor support: a custom inspector for `MultiplayerConfig` and `Sdx17 > Multiplayer` menu items to create a config asset and a manager GameObject.
- `Samples~/Demo`: a demo MonoBehaviour and a setup guide for a minimal demo scene.
- `Tests~/MultiplayerSelfTest`: a Play-mode self-test that logs PASS/FAIL for config, transport, spawn, damage validation, inventory and disconnect paths.

### Notes

- Version 0.1.0 is UNTESTED IN UNITY. No Unity load test, Play-mode run or Netcode package test has been performed in this environment.
- No claim is made that this package is production-ready.
