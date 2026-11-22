# Sdx17 Multiplayer Architecture — Guide

> **Version 0.1.0 — UNTESTED IN UNITY**
>
> This package has **not** been loaded in Unity, run in Play mode, or
> tested against the Netcode for GameObjects package. Everything in
> this guide describes the code as written and reviewed locally only.
> Run the test checklist in `README.md` in Unity 2022.3 LTS before
> using this package in a real project, and fix anything it finds.
> Do not treat this package as tested or production-ready.

Server-authoritative multiplayer for Unity 2022.3 LTS, written in C#.
Package name: `com.sdx17.multiplayer`. Namespace: `Sdx17.Multiplayer`
(the Netcode adapter lives in `Sdx17.Multiplayer.Netcode`).
No paid dependencies. The core compiles with no extra packages; the
optional Netcode for GameObjects adapter needs
`com.unity.netcode.gameobjects` installed separately — it is **not**
bundled with this package.

---

## Contents

1. [Quickstart (5 minutes)](#1-quickstart-5-minutes)
2. [Concepts](#2-concepts)
3. [API reference](#3-api-reference)
4. [Config reference](#4-config-reference)
5. [Integration with the other Sdx17 systems](#5-integration-with-the-other-sdx17-systems)
6. [Troubleshooting / FAQ](#6-troubleshooting--faq)
7. [Performance notes](#7-performance-notes)
8. [Limitations](#8-limitations)

---

## 1. Quickstart (5 minutes)

This quickstart uses only the core package — no extra packages, no
network, no server process. It runs on the in-memory loopback
transport, so everything happens inside one Editor Play-mode session.

### Step 1 — Add the package

Copy this package into your project at
`Packages/com.sdx17.multiplayer`, or add it as a local package in the
Package Manager (**Window > Package Manager > + > Add package from
disk**, then select this folder's `package.json`).

### Step 2 — Create a config

**Sdx17 > Multiplayer > Create Multiplayer Config**

This creates `Assets/MultiplayerConfig.asset` with the defaults:
address `127.0.0.1`, port `7777`, 8 players, server authority on.
Leave the defaults for your first run.

### Step 3 — Create a manager

**Sdx17 > Multiplayer > Create Multiplayer Manager**

This creates a `MultiplayerManager` GameObject in the scene. Assign
the config asset from Step 2 to the manager's **Config** field in the
Inspector. (The manager also has a **Player Prefab** field — leave it
empty for now; the manager creates a plain `NetworkPlayer` GameObject
when no prefab is assigned — and a **Local Player Name** field,
default `"Player"`.)

### Step 4 — Add the demo script

Follow `Samples~/Demo/README.md` to add `DemoMultiplayer`
(`Samples~/Demo/DemoMultiplayer.cs`) to a GameObject in the scene.
It finds the `MultiplayerManager` itself if you leave its
**Manager** field empty.

### Step 5 — Press Play, press H

Press **Play**, then press **H** (host). The Console should log the
local player spawning at 100/100 health. Then try:

| Key | What it does |
|---|---|
| **H** | Start as host (server + local client) |
| **C** | Start as client (loopback session, no real network) |
| **S** | Start as dedicated server (no local player) |
| **X** | Request 10 damage to the local player (server-validated) |
| **G** | Request a 10-point heal for the local player |
| **I** | Request the item `"health-potion"` for the local player |
| **Q** | Shut the session down |

Pressing **X** should log the server validating the damage action,
then the health change to 90/100. Pressing **Q** stops the session
and empties the player list.

### Step 6 — Run the self-test (optional, recommended)

Add `MultiplayerSelfTest` (`Tests~/MultiplayerSelfTest.cs`) to a
GameObject in a test scene and press Play. It runs 18 checks
(config, transport, spawn, damage validation, inventory, disconnect)
and logs a summary line. The expected result is
**18 passed, 0 failed**. If any check fails, fix it before using the
package — see [Section 6](#6-troubleshooting--faq).

### Next step — real networking

When the loopback demo works, follow
`Runtime/Netcode/README.md` to install
`com.unity.netcode.gameobjects`, add a NetworkManager, and hand a
`NetcodeTransportAdapter` to the manager with `SetTransport`
(see [Section 3.6](#36-netcodetransportadapter-optional-netcode-adapter)
and [FAQ Q2](#q2-how-do-i-get-real-networking-working)).

---

## 2. Concepts

### 2.1 The transport abstraction (`ITransport`)

Core multiplayer code talks only to the `ITransport` interface, never
to a networking library directly. That is what makes the transport
swappable: the loopback transport for offline work, the Netcode
adapter for real play, or your own transport later — without touching
game logic.

An `ITransport` owns the connection lifecycle (`StartHost`,
`StartClient`, `StartServer`, `Shutdown`), reports connection state
(`State`, `Mode`, `IsServer`, `IsHost`, `IsClient`,
`LocalClientId`, `ConnectedClientIds`), moves actions
(`SendAction`, `BroadcastValidatedAction`), disconnects clients
(`DisconnectClient`), and raises four events:
`OnClientConnected`, `OnClientDisconnected`, `OnActionReceived`,
`OnBroadcastReceived`.

Two implementations ship with the package:

- **`LocalLoopbackTransport`** — in-memory, offline/local only. See
  [Section 2.5](#25-the-loopback-transport).
- **`NetcodeTransportAdapter`** — optional, in `Runtime/Netcode/`,
  implements `ITransport` on top of Netcode for GameObjects. See
  [Section 3.6](#36-netcodetransportadapter-optional-netcode-adapter).

### 2.2 Connection modes (`ConnectionMode`)

| Mode | Meaning |
|---|---|
| `None` | No session running. |
| `Host` | Server + local client in one instance. |
| `Client` | Joins a server; no local server. |
| `Server` | Dedicated server, no local player. |

`TransportState` tracks the transport lifecycle separately:
`Stopped`, `Starting`, `Running`, `Stopping`.

### 2.3 The server-validated action pattern

This is the core security decision in the package. It works in three
steps:

1. **A client requests.** It calls `RequestDamage`, `RequestHeal`,
   `RequestAddItem`, or `RequestAction` on `MultiplayerManager`.
   It changes **nothing** locally — it only sends a `PlayerAction`
   (client id, action id, target id, value, client timestamp) to the
   server.
2. **The server validates.** `MultiplayerManager.ValidateAction`
   checks, in order: the sender is a known connected player; the
   action is not stale (see [Section 2.4](#24-staleness--lag));
   the action id is known; the target exists where one is needed;
   the sender is alive for combat and heal actions; damage and heal
   values are in range (1–1000). It returns `null` when the action
   is valid, otherwise a human-readable rejection reason.
3. **The server applies and broadcasts.** Only when validation
   passes does the server apply the change
   (`ApplyValidatedAction`), raise `MultiplayerEvents.OnValidatedAction`,
   and broadcast the result through the transport
   (`BroadcastValidatedAction`). Clients mirror the result with the
   server-directed methods on `NetworkPlayer`
   (`ApplyServerDirectedDamage`, `ApplyServerDirectedHeal`,
   `ApplyServerDirectedAddItem`).

A client that edits its own health or inventory directly is refused:
`NetworkPlayer.ApplyDamage`, `Heal`, `Respawn`, `AddItem`, and
`RemoveItem` all return `false` and change nothing unless the
instance is running as the server
(`IsServerAuthoritativeInstance` is true). The server never accepts
health or inventory values sent by a client.

Built-in action ids are `"damage"`, `"heal"`, and `"add-item"`.
Any other action id is a **custom action**: the server validates the
sender and the timestamp, then broadcasts it unchanged for game code
to handle — the manager does not apply custom actions itself. Use
`RequestAction("interact", "door-01", 0f)` for game-specific actions
like interacting, using a skill, or triggering a quest step.

### 2.4 Staleness / lag

A `PlayerAction` carries the client's timestamp
(`ClientTimestamp`, seconds since startup, from
`Time.realtimeSinceStartupAsDouble`). The server rejects any action
older than `MultiplayerConfig.ActionStalenessWindowMs`
(default 5000 ms) as stale, and any action dated more than 1000 ms
in the future. This stops a delayed or replayed packet from applying
twice or landing a hit that was already answered.

There is **no client-side prediction and no lag compensation** for
movement or aiming in this version. The staleness window is enough
for slow or turn-style actions. Fast hit-scan combat needs a
dedicated lag-compensation pass on top of this layer.

### 2.5 The loopback transport

`LocalLoopbackTransport` is an in-memory transport for offline
development and Play-mode self-tests. It needs no network, no package
and no server process:

- `StartHost()` creates a local server plus a local client (id `0`).
- `StartClient(address, port)` creates a local session with no server
  to talk to — it still runs, so demo code can exercise the client
  code path offline. It refuses an empty address or a port of `0`.
- `StartServer(port)` creates a local server with no local player.
  It refuses a port of `0`.
- `SimulateRemoteClientJoin()` pretends a remote client joined the
  local server and returns the simulated client id, or
  `ulong.MaxValue` when the transport is not running as a server.
  This is a test/demo helper, used by the self-test.
- Actions sent by a client are delivered straight to the server side
  of the same transport, which is exactly the path
  `MultiplayerManager` validates before broadcasting. Actions from a
  client id the transport has never seen connected are dropped, not
  processed.

**Do not use this transport for real networked play.** Use the
Netcode adapter in `Runtime/Netcode/` instead.

### 2.6 Events, not polling

Health and inventory changes raise events that UI, audio, and other
systems listen to, instead of polling values every frame. There are
three layers, all carrying the same signals:

- **`MultiplayerEvents`** (static hub) — C# events, no reference to
  the manager or a player needed. See [Section 3.5](#35-multiplayerevents-static-hub).
- **`NetworkPlayer.HealthChanged` / `InventoryChanged`** — per-player
  C# events, plus the Inspector-wirable UnityEvents
  `OnHealthChangedEvent` (`UnityEvent<int, int>`) and
  `OnInventoryChangedEvent` (`UnityEvent<string, bool>`).
- **`MultiplayerEventBridge`** — a `MonoBehaviour` that forwards
  `MultiplayerEvents` into Inspector-wirable UnityEvents, for wiring
  UI in the Editor without writing code.

All events fire on the Unity main thread. The server is the only side
that raises state-changing events; clients receive them as the result
of a server broadcast, never from a remote peer directly.

### 2.7 The manager and the player

- **`MultiplayerManager`** owns a session: it starts host / client /
  server, keeps the player list, spawns and removes `NetworkPlayer`
  objects, and runs the server-validated action pattern. There is
  normally one manager per scene. It creates a
  `LocalLoopbackTransport` by itself on `Awake` unless you turn off
  **Create Loopback Transport** in the Inspector and assign a
  transport in code with `SetTransport` before starting.
- **`NetworkPlayer`** is one player in a session: identity
  (`ClientId`, `PlayerName`), health (`Health`, `MaxHealth`,
  `IsAlive`), and a session-only inventory (`Inventory`,
  `HasItem`). Health and inventory are server state — see
  [Section 2.3](#23-the-server-validated-action-pattern).
  `NetworkPlayer` inventory is **session state only; it is not
  saved**. See the Inventory / Secure Save integration notes in
  [Section 5](#5-integration-with-the-other-sdx17-systems).

---

## 3. API reference

All runtime types are in the `Sdx17.Multiplayer` namespace, except
the Netcode adapter, which is in `Sdx17.Multiplayer.Netcode`.
Every class and method below exists in the `Runtime/` folder of
version 0.1.0 — nothing here is invented. Private helpers
(`SpawnLocalPlayer`, `SpawnRemotePlayer`, `HandleClientConnected`,
and similar) are intentionally not listed; they are internal
implementation details.

### 3.1 `ITransport` (interface) — `Runtime/ITransport.cs`

The transport abstraction. Core code talks only to this interface.

**Enums**

| Type | Values |
|---|---|
| `ConnectionMode` | `None`, `Host`, `Client`, `Server` |
| `TransportState` | `Stopped`, `Starting`, `Running`, `Stopping` |

**Events**

| Event | Arguments | Meaning |
|---|---|---|
| `OnClientConnected` | `ulong` client id | A client connected. |
| `OnClientDisconnected` | `ulong` client id | A client disconnected. |
| `OnActionReceived` | `PlayerAction` | An action request arrived (server side). |
| `OnBroadcastReceived` | `PlayerAction` | A validated action broadcast arrived (client side). The server never raises this for itself — it already applied the action. |

**Properties**

| Property | Type | Meaning |
|---|---|---|
| `State` | `TransportState` | Lifecycle state. |
| `Mode` | `ConnectionMode` | How this instance participates. |
| `IsServer` | `bool` | True for Host and Server modes. |
| `IsHost` | `bool` | True for Host mode only. |
| `IsClient` | `bool` | True for Host and Client modes. |
| `LocalClientId` | `ulong` | This instance's client id. |
| `ConnectedClientIds` | `IReadOnlyCollection<ulong>` | Ids currently connected. |

**Methods**

| Method | Returns | Meaning |
|---|---|---|
| `StartHost()` | `bool` | Start as host (server + local client). |
| `StartClient(string address, ushort port)` | `bool` | Join a server at the given address and port. |
| `StartServer(ushort port)` | `bool` | Start a dedicated server on the given port. |
| `Shutdown()` | `void` | Stop the transport. |
| `SendAction(PlayerAction action)` | `bool` | Send an action request to the server (client side) or, when called on the server, broadcast a validated action. Returns `false` when there is no active connection to send on. |
| `BroadcastValidatedAction(PlayerAction action)` | `bool` | Server only. Broadcast an action the server already validated and applied. Returns `false` when not the server or not running. |
| `DisconnectClient(ulong clientId)` | `bool` | Disconnect one remote client. Server/host only; a client calling this on a remote id must be ignored by the implementation. |

**`PlayerAction` (struct)** — a validated player action travelling
client → server → all clients. Fields, all public:
`ClientId` (`ulong`), `ActionId` (`string`), `TargetId`
(`string`), `Value` (`float`), `ClientTimestamp` (`double`).
Constructor:
`PlayerAction(ulong clientId, string actionId, string targetId, float value, double clientTimestamp)`.
`ActionId` and `TargetId` are never left `null` — the constructor
replaces a `null` value with `string.Empty`.

### 3.2 `LocalLoopbackTransport` (sealed class) — `Runtime/ITransport.cs`

In-memory `ITransport` implementation for offline development and
Play-mode self-tests. Implements every `ITransport` member listed
in [Section 3.1](#31-itransport-interface--runtimeitransportcs),
plus one extra method:

| Method | Returns | Meaning |
|---|---|---|
| `SimulateRemoteClientJoin()` | `ulong` | Test/demo helper: pretend a remote client joined this local server. Returns the simulated client id, or `ulong.MaxValue` when the transport is not running as a server. |

Notes: `StartHost()` creates a local server plus a local client
(id `0`). `StartClient` refuses an empty address or a port of `0`.
`StartServer` refuses a port of `0`. `SendAction` drops actions
from a client id the transport has never seen connected.
`DisconnectClient` returns `false` when not the server or when the
id is unknown. Do not use this transport for real networked play.

### 3.3 `MultiplayerConfig` (ScriptableObject) — `Runtime/MultiplayerConfig.cs`

All tunable settings in one asset. Create one with
**Sdx17 > Multiplayer > Create Multiplayer Config**, or right-click
in the Project window > **Create > Sdx17 > Multiplayer Config**
(`[CreateAssetMenu(fileName = "MultiplayerConfig", menuName = "Sdx17/Multiplayer Config")]`).

**Properties** (all read-only; edit the values in the Inspector)

| Property | Type | Default | Meaning |
|---|---|---|---|
| `ServerAddress` | `string` | `"127.0.0.1"` | Address a client connects to. |
| `ServerPort` | `ushort` | `7777` | Must match the transport's port. `0` is rejected. |
| `ConnectionTimeoutSeconds` | `float` | `10` | A client silent for longer than this counts as disconnected. |
| `MaxPlayers` | `int` | `8` | 1–64. Joining past the limit is refused and the client is disconnected. |
| `MaxHealth` | `int` | `100` | Starting and maximum health for every player. |
| `ServerAuthoritative` | `bool` | `true` | Keep on. Off means clients can change their own state — local demo use only. |
| `ActionStalenessWindowMs` | `float` | `5000` | Actions older than this are rejected as stale (lag / replay guard). |
| `HeartbeatIntervalSeconds` | `float` | `2` | How often the server heartbeat timer ticks. |

**Methods**

| Method | Returns | Meaning |
|---|---|---|
| `Validate()` | `string` | Checks every value and returns a human-readable problem, or `null` when the config is usable. Call before starting a session so a bad config fails loudly in the Editor instead of silently at runtime. |
| `IsValid` | `bool` (property) | `true` when `Validate()` returns `null`. |
| `CloneForLocalDemo()` | `MultiplayerConfig` | Returns a config with the same values as this one but safe for a local demo: loopback address (`127.0.0.1`) and server authority kept on. |

`OnValidate` clamps out-of-range values in the Editor (max players
to 1–64, max health to at least 1, timeout back to 10 s when ≤ 0,
staleness window to at least 0, heartbeat back to 2 s when ≤ 0,
empty address back to `127.0.0.1`), so an accidental drag in the
Inspector can never produce a config that crashes a session at
runtime. The custom inspector (`MultiplayerConfigEditor`, see
[Section 3.7](#37-editor-support)) shows the validation result
inline.

### 3.4 `MultiplayerManager` (MonoBehaviour) — `Runtime/MultiplayerManager.cs`

Owns a session. Normally one per scene.

**Properties**

| Property | Type | Meaning |
|---|---|---|
| `Config` | `MultiplayerConfig` | The assigned config asset (Inspector field **Config**). |
| `PlayerPrefab` | `NetworkPlayer` | Optional player prefab (Inspector field **Player Prefab**). When `null`, the manager creates a plain `NetworkPlayer` GameObject. |
| `LocalPlayerName` | `string` | Name for the local player (Inspector field, default `"Player"`). |
| `Transport` | `ITransport` | The active transport. |
| `Mode` | `ConnectionMode` | The transport's mode, or `None` when there is no transport. |
| `IsRunning` | `bool` | `true` when a transport exists and its state is `Running`. |
| `IsServer` | `bool` | `true` when the transport is the server (host or dedicated server). |
| `IsHost` | `bool` | `true` when the transport is a host. |
| `IsClient` | `bool` | `true` when the transport is a client (host or client). |
| `LocalClientId` | `ulong` | The transport's local client id, or `ulong.MaxValue` when there is no transport. |
| `Players` | `IReadOnlyDictionary<ulong, NetworkPlayer>` | All currently registered players, keyed by client id. |
| `LocalPlayer` | `NetworkPlayer` | The local player, or `null` when there is no transport or no player for the local id. |

**Session methods**

| Method | Returns | Meaning |
|---|---|---|
| `SetTransport(ITransport transport)` | `void` | Replaces the transport (for example with the Netcode adapter). Must be called **before** `StartHost` / `StartClient` / `StartServer`. Refused with a warning while a session is running. Passing `null` clears the transport. |
| `SetConfig(MultiplayerConfig config)` | `void` | Replaces the config. Refused with a warning while a session is running. |
| `StartHost()` | `bool` | Start as host (server + local client). Refused when there is no transport, a session is already running, no config is assigned, or the config fails `Validate()`. Spawns the local player. |
| `StartClient()` | `bool` | Join a server using the config's address and port. Same refusal rules as `StartHost`. Spawns the local player. |
| `StartServer()` | `bool` | Start a dedicated server on the config's port. Same refusal rules; does **not** spawn a local player. |
| `Shutdown()` | `void` | Stop the transport, remove all players, clear the per-client action history, and raise `OnConnectionChanged` with `running = false`. Does nothing when there is no transport or it is already stopped. |
| `DisconnectPlayer(ulong clientId)` | `bool` | Server/host only. Disconnects a remote client and removes its player. Returns `false` when not the server, when the id is unknown, or when the id is the local client. |

**Player-list methods**

| Method | Returns | Meaning |
|---|---|---|
| `RegisterPlayer(NetworkPlayer player)` | `bool` | Registers a player that already exists in the scene (for example one spawned by a transport callback). Returns `false` for a `null` player or a client id that is already registered. Raises `OnPlayerSpawned`. |
| `RemovePlayer(ulong clientId)` | `bool` | Removes a player from the list and destroys its GameObject when one exists. Returns `false` when the player is not registered. Raises `OnPlayerLeft`. |

**Client request methods** — these change nothing locally; they only
send a request the server validates (see [Section 2.3](#23-the-server-validated-action-pattern)).
All return `false` when there is no transport or no session is running.

| Method | Returns | Meaning |
|---|---|---|
| `RequestDamage(ulong targetClientId, int damage)` | `bool` | Ask the server to deal damage to another player. Returns `false` when `damage <= 0`. Sends action id `"damage"` with the target id as `TargetId` and the damage as `Value`. |
| `RequestHeal(int amount)` | `bool` | Ask the server to heal the local player. Returns `false` when `amount <= 0`. Sends action id `"heal"`. |
| `RequestAddItem(string itemId)` | `bool` | Ask the server to give the local player an item. Returns `false` when the item id is empty. Sends action id `"add-item"` with the item id as `TargetId`. The server checks the item id and that the player does not already own it. |
| `RequestAction(string actionId, string targetId, float value)` | `bool` | Ask the server to run a custom, game-specific action (for example `"interact"` or `"use-skill"`). Returns `false` when the action id is empty. `targetId` may be `null` (treated as empty). The server validates the sender and the timestamp, then broadcasts the action unchanged — the manager does not apply custom actions itself. |

**Server-side methods**

| Method | Returns | Meaning |
|---|---|---|
| `ValidateAction(PlayerAction action)` | `string` | Server-side validation for an incoming action. Returns `null` when valid, otherwise a human-readable rejection reason. Checks, in order: the sender is a known connected player; the action is not stale (and not dated in the future); the action id is known; the target exists where one is needed; the sender is alive for combat and heal actions; damage and heal values are in range (1–1000). |
| `ApplyValidatedAction(PlayerAction action)` | `bool` | Applies an action that has already passed `ValidateAction`. Returns `false` when not the server. Re-validates internally: on failure it raises `OnActionRejected` and returns `false`. On success it applies the effect (`"damage"` → `ApplyDamage`, `"heal"` → `Heal`, `"add-item"` → `AddItem`, custom → no built-in effect), raises `OnValidatedAction`, and broadcasts through the transport. Never call this with an unvalidated action from a client. |
| `ApplyServerBroadcast(PlayerAction action)` | `void` | Applies a broadcast the server already validated and applied. Only runs on non-server instances — the server applied the action itself in `ApplyValidatedAction` and must not apply it twice. Mirrors the result with the server-directed methods on `NetworkPlayer` and raises `OnValidatedAction`. |

**Notes**

- `Awake` logs a warning when no config is assigned, and builds a
  `LocalLoopbackTransport` when **Create Loopback Transport** is on
  (the default). `OnDestroy` calls `Shutdown()`.
- `Update` runs heartbeat bookkeeping on the server only: a timer
  that ticks every `HeartbeatIntervalSeconds`. The timer itself is
  the heartbeat in this layer — a real transport sends a packet here.
  If a client stops answering for longer than the configured
  timeout, the transport reports the disconnect and the manager
  removes the player.
- When a remote client connects and the player list is already at
  `MaxPlayers`, the manager refuses the join and disconnects the
  client (`DisconnectPlayer`).
- The manager never accepts health or inventory values sent by a
  client. Only action requests travel client → server.

### 3.5 `MultiplayerEvents` (static class) + `MultiplayerEventBridge` — `Runtime/NetworkEvents.cs`

One hub for multiplayer events, so UI, audio, and game systems can
subscribe without holding a reference to the manager or to a player.

**`MultiplayerEvents` events** (all `public static event`)

| Event | Arguments | Meaning |
|---|---|---|
| `OnPlayerSpawned` | `NetworkPlayer` player | A player object was spawned and registered. |
| `OnPlayerLeft` | `ulong` client id, `string` player name | A player left or was disconnected. |
| `OnHealthChanged` | `NetworkPlayer` player, `int` health, `int` max health | A player's health changed. |
| `OnInventoryChanged` | `NetworkPlayer` player, `string` item id, `bool` added | A player's inventory changed (`added` is `true` for a grant, `false` for a removal). |
| `OnValidatedAction` | `PlayerAction` action | The server validated an action and broadcast it. |
| `OnActionRejected` | `PlayerAction` action, `string` reason | The server rejected an action. |
| `OnConnectionChanged` | `ConnectionMode` mode, `bool` running | The connection state changed (`running` is `true` when a session started, `false` when it stopped). |

**`MultiplayerEvents` method**

| Method | Returns | Meaning |
|---|---|---|
| `ClearAllListeners()` | `void` | Clears every subscriber. Call from a test or a scene teardown so a destroyed scene cannot leave stale listeners behind. (Used by `MultiplayerSelfTest`.) |

The `Raise…` methods (`RaisePlayerSpawned`, `RaisePlayerLeft`,
`RaiseHealthChanged`, `RaiseInventoryChanged`,
`RaiseValidatedAction`, `RaiseActionRejected`,
`RaiseConnectionChanged`) are `internal` — game code subscribes to
the events, it does not raise them.

**`MultiplayerEventBridge` (MonoBehaviour)** — Inspector-friendly
UnityEvent versions of the same signals, for wiring UI in the Editor
without writing code. Add this component to any GameObject and wire
its events in the Inspector. It subscribes on `OnEnable` and
unsubscribes on `OnDisable`.

| Property | Type | Meaning |
|---|---|---|
| `OnPlayerSpawned` | `UnityEvent<NetworkPlayer>` | Forwarded from `MultiplayerEvents.OnPlayerSpawned`. |
| `OnPlayerLeft` | `UnityEvent<string>` | Forwarded from `MultiplayerEvents.OnPlayerLeft`, carrying the player **name** only (not the client id). |
| `OnConnected` | `UnityEvent` | Raised when `OnConnectionChanged` fires with `running = true`. |
| `OnDisconnected` | `UnityEvent` | Raised when `OnConnectionChanged` fires with `running = false`. |

Example — a health bar without holding a manager reference:

```csharp
using Sdx17.Multiplayer;

void OnEnable()
{
    MultiplayerEvents.OnHealthChanged += HandleHealthChanged;
    MultiplayerEvents.OnPlayerLeft += HandlePlayerLeft;
}

void OnDisable()
{
    MultiplayerEvents.OnHealthChanged -= HandleHealthChanged;
    MultiplayerEvents.OnPlayerLeft -= HandlePlayerLeft;
}

void HandleHealthChanged(NetworkPlayer player, int health, int maxHealth)
{
    // Update the health bar for this player.
}

void HandlePlayerLeft(ulong clientId, string playerName)
{
    // Remove this player from the UI.
}
```

Always unsubscribe in `OnDisable` (or call
`MultiplayerEvents.ClearAllListeners()` on scene teardown) — a
destroyed scene that leaves stale listeners behind is the most
common source of duplicate event handling with this package.

### 3.6 `NetcodeTransportAdapter` (optional Netcode adapter) — `Runtime/Netcode/NetcodeTransportAdapter.cs`

Namespace: `Sdx17.Multiplayer.Netcode`. Implements `ITransport` on
top of Netcode for GameObjects. Attach it to the same GameObject as
the NetworkManager, then hand it to the manager with
`MultiplayerManager.SetTransport` before starting.

**The compile guard — read this first.** The whole file is wrapped in:

```csharp
#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE
// ... adapter code, references Unity.Netcode types ...
#endif
```

- **Without the Netcode package installed**, the file compiles to
  nothing. The core files compile standalone and the
  `LocalLoopbackTransport` works for offline development and tests.
- **With the package installed**, the adapter compiles and
  implements `ITransport` on top of `NetworkManager`, so
  `MultiplayerManager` works over a real network with no changes to
  game code.

The guard symbol (`UNITY_NETCODE_GAMEOBJECTS` or `UNITY_NETCODE`)
needs confirming on first Unity load — see
[FAQ Q1](#q1-i-installed-the-netcode-package-but-the-adapter-still-does-not-compile--what-do-i-check).
If the symbol is not defined after installing the package, the
adapter silently compiles to nothing and `SetTransport` with the
adapter is not possible; the core still works on the loopback
transport.

**Properties**

| Property | Type | Meaning |
|---|---|---|
| `ReliableActions` | `bool` | Whether actions are sent with reliable delivery. Backed by the Inspector field **Reliable Actions** (default `true`). Keep this on for gameplay actions; unreliable delivery is only suitable for frequent state that is resent every frame. |
| (all `ITransport` properties) | — | `State`, `Mode`, `ConnectedClientIds`, plus `IsServer` / `IsHost` / `IsClient` / `LocalClientId`, which read from `NetworkManager.Singleton` (and return `ulong.MaxValue` for the local id when there is no `NetworkManager`). |

**Methods** — the same `ITransport` surface as
[Section 3.1](#31-itransport-interface--runtimeitransportcs):
`StartHost()`, `StartClient(string address, ushort port)`,
`StartServer(ushort port)`, `Shutdown()`,
`SendAction(PlayerAction action)`,
`BroadcastValidatedAction(PlayerAction action)`,
`DisconnectClient(ulong clientId)`, with Netcode-specific behaviour:

- `StartHost` / `StartClient` / `StartServer` call the matching
  method on `NetworkManager.Singleton` and return `false` when there
  is no `NetworkManager` or the transport is already running.
  `StartClient` and `StartServer` also refuse an empty address or a
  port of `0`.
- **Address and port come from the NetworkManager's transport
  component, not from the method arguments.** UnityTransport reads
  its address and port from the NetworkManager's transport
  component, so configure those to match the `MultiplayerConfig`
  first — `StartClient(address, port)` validates the arguments but
  the actual connection uses the transport component's settings.
- `SendAction`: on the server/host it delivers the action straight
  to the local server side, exactly as a remote client's request
  would arrive, so it goes through the same validation. On a client
  it sends the request to the server only, through
  `SubmitActionServerRpc`.
- `BroadcastValidatedAction`: server only; sends the validated action
  to all clients through `BroadcastActionClientRpc`. The host
  ignores its own broadcast (it already applied the action as the
  server).
- `DisconnectClient`: server only; calls
  `NetworkManager.DisconnectClient(clientId)`. Returns `false`
  when not the server, when there is no `NetworkManager`, or when
  the id is not in the connected set.

**RPCs (private, listed so you know what the adapter does)**

- `SubmitActionServerRpc(ulong clientId, string actionId, string targetId, float value, double clientTimestamp, ServerRpcParams serverRpcParams = default)` — `[ServerRpc(RequireOwnership = false)]`.
  Defence in depth: the id the client claims must match the id
  Netcode actually assigned to that connection
  (`serverRpcParams.Receive.SenderClientId`). A client that lies
  about who it is gets its action dropped here, before it ever
  reaches `MultiplayerManager`.
- `BroadcastActionClientRpc(ulong clientId, string actionId, string targetId, float value, double clientTimestamp)` — `[ClientRpc]`.
  Server → all clients, only for actions the server already
  validated and applied. Clients apply the result through
  `MultiplayerManager.ApplyServerBroadcast`.

**What the adapter does NOT do.** There is no movement sync, no
NetworkVariable health sync, and no scene management in this
adapter. Those belong in game code built on `NetworkPlayer` and
`MultiplayerEvents`. The adapter only moves connection events and
validated actions. Server validation still happens in
`MultiplayerManager.ValidateAction` — the transport never decides
whether an action is allowed. Health and inventory are never sent
by a client through this adapter.

**Usage**

```csharp
using Sdx17.Multiplayer;
using Sdx17.Multiplayer.Netcode;

// Add a NetworkManager to the scene (GameObject > Netcode >
// NetworkManager) and set its transport address and port to match
// your MultiplayerConfig first.
var adapter = FindObjectOfType<NetcodeTransportAdapter>();
manager.SetTransport(adapter);
manager.StartHost();
```

### 3.7 Editor support — `Editor/MultiplayerEditor.cs`

**`MultiplayerConfigEditor`** (`[CustomEditor(typeof(MultiplayerConfig))]`):
a custom inspector for `MultiplayerConfig`. It draws the config's
fields, then shows the validation result inline — an error HelpBox
when `Validate()` returns a problem, otherwise an info HelpBox
summarising address, port, player limit, and server-authority state
(a warning HelpBox when server authority is off, plus a second
warning that server authority off is local-demo use only). It also
has an **Open Multiplayer README** button that logs a pointer to
`README.md` in the Console.

**`MultiplayerMenu`** (static class, menu items under
**Sdx17 > Multiplayer**):

| Menu item | What it does |
|---|---|
| **Sdx17 > Multiplayer > Create Multiplayer Config** | Creates a `MultiplayerConfig` asset at `Assets/MultiplayerConfig.asset`, selects it in the Project window, and logs a reminder to assign it to a `MultiplayerManager`. |
| **Sdx17 > Multiplayer > Create Multiplayer Manager** | Creates a `MultiplayerManager` GameObject in the scene (with Undo support) and selects it. |
| **Sdx17 > Multiplayer > Log Setup Checklist** | Logs the four-step setup checklist to the Console (create config, add the demo, press Play with H/C, install Netcode for real networking). |

### 3.8 `NetworkPlayer` (MonoBehaviour) — `Runtime/NetworkPlayer.cs`

One player in a session. Health and inventory are server state.

**Properties**

| Property | Type | Meaning |
|---|---|---|
| `ClientId` | `ulong` | This player's client id. |
| `PlayerName` | `string` | This player's name. |
| `MaxHealth` | `int` | Maximum health (set by `Initialize`). |
| `Health` | `int` | Current health. |
| `IsAlive` | `bool` | `true` when `Health > 0`. |
| `IsServerAuthoritativeInstance` | `bool` | `true` when this instance is running on the server (or host). Only then may health and inventory be changed directly. Set by `MultiplayerManager` on spawn; set it by hand in a test only. |
| `Inventory` | `IReadOnlyList<string>` | Session-only item ids owned by this player. |
| `OnHealthChangedEvent` | `UnityEvent<int, int>` | Inspector-wirable: new health, max health. |
| `OnInventoryChangedEvent` | `UnityEvent<string, bool>` | Inspector-wirable: item id, added (`true`) or removed (`false`). |

**Events** (C# events, per player)

| Event | Arguments | Meaning |
|---|---|---|
| `HealthChanged` | `int` health, `int` max health | Fired on this player when health changes. |
| `InventoryChanged` | `string` item id, `bool` added | Fired on this player when inventory changes. |

**Methods**

| Method | Returns | Meaning |
|---|---|---|
| `Initialize(ulong clientId, string playerName, int maxHealth, bool isServerAuthoritativeInstance)` | `void` | Sets up a freshly spawned player. Called by `MultiplayerManager`; call it by hand only in a test. Empty player name falls back to `"Player {clientId}"`; max health is clamped to at least 1; health starts at max; inventory is cleared. |
| `SetServerAuthority(bool isServer)` | `void` | Marks this instance as server-authoritative (or not). The manager calls this on spawn; tests use it to exercise both sides. |
| `ApplyDamage(int damage)` | `bool` | **Server only.** Reduces health, clamped at zero. Returns `false` and changes nothing when called on a client, when the player is already dead, or when damage is not positive. A client asking for damage must go through the manager's validated-action path instead. |
| `Heal(int amount)` | `bool` | **Server only.** Restores health, clamped at max health. Returns `false` and changes nothing on a client, when the amount is not positive, when the player is dead, or when already at full health. |
| `Respawn()` | `bool` | **Server only.** Restores full health. Returns `false` on a client or when the player is already at full health. |
| `AddItem(string itemId)` | `bool` | **Server only.** Adds an item id to the inventory. Duplicate ids are rejected, so a repeated (or replayed) request cannot stack the same item twice. Returns `false` on a client, for an empty id, or for a duplicate. |
| `RemoveItem(string itemId)` | `bool` | **Server only.** Removes an item id from the inventory. Returns `false` on a client, for an empty id, or when the player does not own it. |
| `HasItem(string itemId)` | `bool` | `true` when the player owns the item id. Returns `false` for an empty id. |
| `ApplyServerHealth(int serverHealth)` | `void` | Applies a health value received from the server, clamped to 0–max health. This is the one path a client uses to update health from a server value; it is never taken from another client. Callers outside the manager should not use this. |
| `ApplyServerDirectedDamage(int damage)` | `void` | Mirrors damage the server already validated and broadcast. Only the local `MultiplayerManager` calls this, when handling a server broadcast. It bypasses the server-authority check that `ApplyDamage` enforces, because the decision was already made by the server. Does nothing when dead or when damage is not positive. |
| `ApplyServerDirectedHeal(int amount)` | `void` | Mirrors a heal the server already validated and broadcast. Same rule as `ApplyServerDirectedDamage`. |
| `ApplyServerDirectedAddItem(string itemId)` | `void` | Mirrors an item grant the server already validated and broadcast. Same rule; duplicates and empty ids are ignored. |
| `ApplyServerInventory(IEnumerable<string> serverInventory)` | `void` | Replaces the inventory with the list the server sent. Server data only; duplicates and empty ids are dropped. Does nothing when the list is `null`. |

All health and inventory changes (through any of the paths above)
raise `HealthChanged` / `InventoryChanged` (both the C# events and
the UnityEvents) and the matching `MultiplayerEvents` hub event.
`ApplyServerInventory` replaces the list silently — it does not
raise one event per item.

### 3.9 Demo and self-test (not runtime API, listed for completeness)

**`DemoMultiplayer`** (`Samples~/Demo/DemoMultiplayer.cs`,
namespace `Sdx17.Multiplayer.Samples`): a minimal demo
`MonoBehaviour`. Property `Manager` (`MultiplayerManager`). Keys
H / C / S / X / G / I / Q as listed in
[Section 1](#1-quickstart-5-minutes). It subscribes to
`MultiplayerEvents` on `OnEnable` and unsubscribes on `OnDisable`,
and logs every spawn, leave, health change, validated action, and
rejected action to the Console. Full scene setup is in
`Samples~/Demo/README.md`.

**`MultiplayerSelfTest`** (`Tests~/MultiplayerSelfTest.cs`,
namespace `Sdx17.Multiplayer.Tests`): a Play-mode self-test
`MonoBehaviour`. Public method `RunAllTests()` (returns
`IEnumerator`; runs automatically on `Start` when the Inspector
field **Run On Start** is `true`). It creates its own manager,
config, and players at runtime and destroys them when it finishes,
so it does not touch your scene. It runs 18 checks and logs a
PASS/FAIL line per check plus a summary line
(`"[Sdx17.Multiplayer SelfTest] Finished: N passed, M failed."`).
Expected result: **18 passed, 0 failed**. Check names:
`ConfigValid`, `ConfigCloneForLocalDemo`, `StartHost`,
`LocalPlayerSpawned`, `PlayerListHasLocalPlayer`,
`ValidatedDamageApplies`, `InvalidDamageRefused`,
`ClientCannotDirectDamage`, `ClientCannotDirectAddItem`,
`ServerAddItem`, `DuplicateItemRejected`, `ServerRemoveItem`,
`UnknownSenderRejected`, `StaleActionRejected`,
`ValidatedHealApplies`, `RemoteClientJoins`,
`DisconnectRemovesPlayer`, `ShutdownClearsPlayers`.
Nothing in the self-test needs the Netcode package or a network:
it runs against `LocalLoopbackTransport`. See `Tests~/README.md`
for what each check proves.

---

## 4. Config reference

All fields live on the `MultiplayerConfig` ScriptableObject.
See [Section 3.3](#33-multiplayerconfig-scriptableobject--runtimemultiplayerconfigcs)
for the property and method reference; this section is the
tuning guidance.

| Field | Default | Range / rule | Guidance |
|---|---|---|---|
| Server Address | `127.0.0.1` | Non-empty string | Address a client connects to. `127.0.0.1` for local testing; your server's address or hostname for real play. Must match the address configured on the NetworkManager's transport component when using the Netcode adapter. |
| Server Port | `7777` | `ushort`, not `0` | Must match the transport's port. `0` is rejected by `Validate()`. Pick a port your firewall and host allow; `7777` is the conventional Unity default. |
| Connection Timeout (s) | `10` | `> 0` | A client silent for longer than this counts as disconnected. Lower it (for example 5 s) for fast-paced games where a dead connection should be noticed quickly; raise it on unreliable networks. |
| Max Players | `8` | 1–64 | Joining past the limit is refused and the client is disconnected. `OnValidate` clamps the Inspector value to 1–64. Start at 8; raise it only after testing with real clients — see [Section 7](#7-performance-notes). |
| Max Health | `100` | `≥ 1` | Starting and maximum health for every player. Every player spawns at full health. |
| Server Authoritative | `true` | — | **Keep on.** Off means clients can change their own state — local demo use only. The custom inspector shows a warning when it is off. There is no supported reason to ship a real game with it off. |
| Action Staleness Window (ms) | `5000` | `≥ 0` | Actions older than this are rejected as stale. Lower it (for example 1000–2000 ms) for fast-paced games to shrink the replay window; raise it on high-latency connections so legitimate slow actions are not rejected. `0` rejects anything that arrives even slightly late — only useful for tests. |
| Heartbeat Interval (s) | `2` | `> 0` | How often the server heartbeat timer ticks. Shorter intervals notice a dead client sooner but cost more bookkeeping; in this version the timer is bookkeeping only (a real transport sends a packet here). |

`Validate()` returns a human-readable problem or `null` when the
config is usable; `IsValid` is `true` when `Validate()` returns
`null`. The custom inspector shows the validation result inline, so
check the Inspector before pressing Play — a bad port or player
limit is visible there first.

`CloneForLocalDemo()` returns a copy with the loopback address and
server authority kept on — use it when a test or a demo needs a
known-safe config without editing the asset.

---

## 5. Integration with the other Sdx17 systems

This package is one system in the Sdx17 family. It starts and runs a
session; it does not find players, run combat, save data, or build a
UI. The systems below are **separate Sdx17 systems, not included in
this package**. The notes in this section are integration guidance —
how each system should talk to this one, using only the public API in
[Section 3](#3-api-reference). None of these integrations has been
built or tested yet; treat each note as the intended wiring, not as a
working feature.

The shared rules for every integration:

- **The server decides.** Any system that changes health, inventory,
  or game state sends a request through `MultiplayerManager` and
  waits for the server to validate it. It never writes state on a
  client.
- **Listen, don't poll.** Game systems subscribe to
  `MultiplayerEvents` (or use a `MultiplayerEventBridge`) instead of
  reading `manager.Players` every frame.
- **`manager.Players` is the source of truth** for who is in the
  session once it starts. A matchmaking or admin system reads it;
  it does not keep its own parallel player list.

### 5.1 Matchmaking

This package starts and runs a session; it does not find players.
A matchmaking system should hand this package a formed match — a
server address, a port, and the list of client ids — and then call
`StartClient` (or `StartHost` for the host) with a config built
from that match. Build the config with `SetConfig` (or a fresh
`MultiplayerConfig` asset with the match's address, port, and player
count) before starting. The player list in
`MultiplayerManager.Players` is the source of truth for who is in
the session once it starts. Matchmaking owns the lobby, the queue,
and the match; this package owns everything after the match is
formed.

### 5.2 Combat (server-authoritative)

Combat code should send its actions through `RequestAction` /
`RequestDamage` and listen to `OnValidatedAction`, never apply
damage locally. Hit detection belongs on the server: a client
reports "I fired at X" (for example
`RequestAction("fire", targetId, damage)`), the server checks range,
line of sight, and the shooter's state, then applies damage with
`NetworkPlayer.ApplyDamage` on the server instance. That method
already refuses non-server callers, so combat built on it inherits
that rule. Note the limits: this package has no movement sync, no
client-side prediction, and no lag compensation for fast combat —
the staleness window only rejects stale actions. Fast hit-scan
combat needs a dedicated lag-compensation pass on top of this layer
before it is fair at real latency.

### 5.3 Admin

An admin system can use `DisconnectPlayer(clientId)` to kick a
client, read `Players` for a live player list, and listen to
`OnActionRejected` to see rejected (possibly abusive) requests —
repeated rejections from one client id are a useful ban/kick signal.
Admin commands must run on the server only: a client sending an
admin action through `RequestAction` is just another action for the
server to validate and reject unless the sender is an admin. Do not
trust a client-sent "I am an admin" claim; the server must check
the sender against its own admin list before acting on any admin
action id.

### 5.4 Inventory

`NetworkPlayer` inventory is session state only — item ids owned
during a session, synced through events. It is not a full inventory
system: there are no item definitions, stacks, equipment slots, or
persistence here. An inventory system should own item definitions
and rules, and use this package only for the session copy: grant
items through the server (`AddItem` on the server instance, or
`RequestAddItem` from a client followed by server validation),
listen to `OnInventoryChanged` to update the UI, and never trust an
inventory list a client sends. Use `ApplyServerInventory` only with
a list the server itself produced.

### 5.5 Secure Save (Secure DataStore)

`NetworkPlayer` inventory is not saved. A Secure Save / DataStore
system should save inventory on session end (listen to
`OnPlayerLeft` and to shutdown via `OnConnectionChanged` with
`running = false`) and restore it by granting items through the
server after spawn (`AddItem` on the server instance), never by
trusting an inventory list a client sends. Save on the server side
only: a client-side save file is data the player controls, so it
must be treated as untrusted input and re-validated on load.
`MultiplayerConfig.CloneForLocalDemo()` is a useful pattern here —
a demo or test save should never overwrite a real save.

### 5.6 Quest

Quest progress is game state, so it follows the same rule as combat:
the client reports progress, the server validates it. A quest system
should send progress as a custom action
(`RequestAction("quest-progress", questId, stepValue)`), have the
server check that the step is reachable from the player's current
quest state, and only then advance the quest and broadcast the
result. Listen to `OnValidatedAction` for the broadcast, and to
`OnPlayerLeft` to clean up quest state for a player who disconnects
mid-quest. Never let a client claim a quest reward directly — the
reward grant must be a server-side `AddItem` (or equivalent) after
the server confirms the quest is complete.

### 5.7 NPC

NPCs are server-owned entities, like players but without a client.
An NPC system should spawn and move NPCs on the server (host or
dedicated server) and send their state to clients through the same
broadcast path — either as custom actions
(`RequestAction` from the server side, or a server broadcast via
`BroadcastValidatedAction`) or through its own Netcode sync layered
on the same transport. NPCs should never be driven by a client:
a client reporting "the NPC moved here" is an action the server
must validate against the NPC's real position, exactly like a
player action. Use `OnPlayerSpawned` / `OnPlayerLeft` to let NPC
AI react to players joining or leaving (for example, aggro range
checks only need to run while a player is in the session).

### 5.8 Dialogue

Dialogue is mostly a client-side presentation concern, but dialogue
choices that change game state (accept a quest, buy an item, open a
door) are actions, not UI events. A dialogue system should send
each consequential choice as a custom action
(`RequestAction("dialogue-choice", dialogueId, choiceIndex)`) and
wait for the server's validated broadcast before applying the
outcome. Purely cosmetic dialogue (flavour text, lore) needs no
server round-trip — but anything that grants an item, advances a
quest, or changes a flag must go through validation, or a modified
client can grant itself the reward by replaying the choice.

### 5.9 UI

UI is the main consumer of `MultiplayerEvents`. A UI system should
subscribe to `OnPlayerSpawned` / `OnPlayerLeft` (player list),
`OnHealthChanged` (health bars), `OnInventoryChanged` (bag UI),
`OnValidatedAction` / `OnActionRejected` (feedback: show the
rejection reason when an action fails), and `OnConnectionChanged`
(connection status, "hosting / connected / disconnected" screens).
For Inspector-only wiring with no code, add a
`MultiplayerEventBridge` to a GameObject and connect its UnityEvents
(`OnPlayerSpawned`, `OnPlayerLeft`, `OnConnected`,
`OnDisconnected`) to UI methods. Remember to unsubscribe in
`OnDisable` (or call `MultiplayerEvents.ClearAllListeners()` on
scene teardown) — stale listeners are the most common source of
duplicate UI updates with this package.

### 5.10 Terrain Tools

Terrain Tools and multiplayer meet at world state: which parts of
the world exist, and who can change them. Terrain edits (digging,
building, placing objects) are state changes, so they follow the
server-validated pattern: a client sends an edit request as a
custom action (`RequestAction("terrain-edit", positionId, value)`),
the server validates that the position is editable and the player
is allowed to edit it, then applies the edit and broadcasts the
result so every client sees the same world. Never let a client
apply a terrain edit locally and hope the server agrees — two
clients editing the same spot will diverge. The terrain system
owns the world data; this package owns the request → validate →
broadcast path that keeps every client in sync.

### 5.11 Developer Plugin

A Developer Plugin (debug console, inspector tools, cheats for
testing) must respect the same authority boundary as everything
else — that is exactly what makes it safe to ship in a development
build. Debug commands that change state (give health, grant items,
teleport a player) should call the server-side methods
(`ApplyDamage`, `Heal`, `AddItem`) only when the instance is the
server, or send a validated request through the manager like any
other client. A debug command that bypasses validation to "just set
health to 100" on a client proves nothing about the real game and
hides bugs. Useful plugin hooks already in this package:
`MultiplayerMenu.LogSetupChecklist` (logs the setup checklist),
`MultiplayerConfig.Validate()` (config health, shown inline by the
custom inspector), and `MultiplayerSelfTest.RunAllTests()` (18
checks, callable from a debug button via a UnityEvent).

---

## 6. Troubleshooting / FAQ

### Q1. I installed the Netcode package, but the adapter still does not compile — what do I check?

The adapter file (`Runtime/Netcode/NetcodeTransportAdapter.cs`) is
wrapped in `#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE`. It only
compiles when one of those symbols is defined, and that symbol is
defined by the Netcode package — it is **not** defined by this
package, and its exact name needs confirming on first Unity load.

Check, in order:

1. **Is the package actually installed?** Open **Window > Package
   Manager** and confirm `com.unity.netcode.gameobjects` appears in
   the list (use the version listed for Unity 2022.3 LTS — the
   README suggests `1.8.1` in `Packages/manifest.json`).
2. **Did the Editor finish compiling after the install?** Wait for
   the spinner in the bottom-right of the Editor to stop, then check
   the Console. If Unity asked you to restart the Editor after the
   install, restart it.
3. **Is the symbol defined?** Create a temporary script that logs
   the symbol, or check **Edit > Project Settings > Player >
   Scripting Define Symbols** — the Netcode package should add its
   symbol there. If the symbol list does not mention Netcode, the
   package install did not complete; remove and re-add the package.
4. **Is the error in the adapter or in the core?** If the Console
   shows errors mentioning `Unity.Netcode` types
   (`NetworkManager`, `ServerRpc`, `ClientRpc`), the adapter is
   compiling but the package reference is broken — re-import the
   package. If there are no Netcode errors at all and the adapter
   simply does nothing, the guard symbol is not defined and the
   file is compiling to nothing — that is the expected behaviour
   without the package, not a bug.
5. **Remember the core still works without it.** Without the
   Netcode package, the adapter compiles to nothing and the core
   files compile standalone on the loopback transport. A missing
   adapter is not a broken package — it just means no real
   networking yet.

### Q2. How do I get real networking working?

1. Install `com.unity.netcode.gameobjects` — **Window > Package
   Manager > + > Add package by name**, name
   `com.unity.netcode.gameobjects` (use the version listed for Unity
   2022.3 LTS), or add
   `"com.unity.netcode.gameobjects": "1.8.1"` to
   `Packages/manifest.json` and let Unity resolve it. Restart the
   Editor if the package asks for it.
2. Confirm the project still compiles with no errors — the adapter
   must now compile (see Q1 if it does not), and the core must be
   unchanged.
3. Add a NetworkManager to your scene (**GameObject > Netcode >
   NetworkManager**) and set its transport address and port to
   match your `MultiplayerConfig`. Note: UnityTransport reads its
   address and port from the NetworkManager's transport component,
   so configure those to match the config first — the adapter's
   `StartClient(address, port)` validates its arguments but the
   actual connection uses the transport component's settings.
4. Add a `NetcodeTransportAdapter` component to the manager
   GameObject (or the NetworkManager GameObject).
5. Before starting a session, hand it to the manager:

   ```csharp
   manager.SetTransport(FindObjectOfType<NetcodeTransportAdapter>());
   manager.StartHost();
   ```

   `SetTransport` must be called **before** `StartHost` /
   `StartClient` / `StartServer` — it is refused while a session is
   running. If the manager's **Create Loopback Transport** box is
   ticked (the default), `Awake` builds a loopback transport first;
   your `SetTransport` call replaces it, which is the intended use.
6. Build a standalone player, run the build as a client and the
   Editor as host, and confirm the client appears in the host's
   player list. From the client, request damage and confirm the host
   Console logs the validation and the client sees the health change.
   This package includes **no hosted server** — you provide the
   machine (or service) that runs the host / dedicated server build.

### Q3. `StartHost()` / `StartClient()` returns `false` — why?

`StartHost`, `StartClient`, and `StartServer` all refuse to start
when any of these is true, and log a warning naming the problem:

- **No transport assigned.** `Transport` is `null`. This happens if
  **Create Loopback Transport** is off and nothing called
  `SetTransport` before starting.
- **A session is already running.** Call `Shutdown()` first, then
  start again. Starting twice without shutting down is refused.
- **No `MultiplayerConfig` assigned.** Assign the config asset to
  the manager's **Config** field in the Inspector (or call
  `SetConfig` before starting).
- **The config fails `Validate()`.** The warning names the problem
  (empty address, port `0`, max players outside 1–64, max health
  below 1, timeout or heartbeat ≤ 0, negative staleness window).
  The custom inspector shows the same problem inline — check the
  config asset in the Inspector first.

`StartHost` can also return `false` when the transport itself
refuses — for example the loopback transport refuses
`StartClient` with an empty address or a port of `0`, and the
Netcode adapter refuses when there is no `NetworkManager` in the
scene. Check the Console warning above the `false` return; the
reason is logged there.

### Q4. My damage / heal / item request does nothing — what happened?

First, check whether the request was even sent. `RequestDamage`,
`RequestHeal`, `RequestAddItem`, and `RequestAction` all return
`false` immediately (and send nothing) when there is no transport,
no session is running, or the arguments are invalid:
`RequestDamage` refuses `damage <= 0`, `RequestHeal` refuses
`amount <= 0`, `RequestAddItem` refuses an empty item id,
`RequestAction` refuses an empty action id. The demo logs each call
as `ok` or `refused — see the warning above` — read that warning.

If the request was sent but nothing changed, the server rejected it.
Subscribe to `MultiplayerEvents.OnActionRejected` — it fires with
the action and a human-readable reason. The common reasons, from
`ValidateAction`:

- `"Unknown sender {id}."` — the sender is not a registered player.
  Usually the session was not started, or the player was removed.
- `"Stale action …"` — the request arrived after
  `ActionStalenessWindowMs` (default 5000 ms). On a slow connection,
  raise the window in the config; in the loopback demo this should
  never happen.
- `"Damage target … does not exist."` — the target client id is not
  in `Players`. Check you passed the right id (the demo passes
  `local.ClientId`).
- `"Dead players cannot deal damage."` / `"Target is already dead."`
  / `"Dead players cannot heal."` — the sender or the target is at
  0 health. Use `Respawn()` on the server instance to restore a
  dead player.
- `"Damage value … is out of range (1-1000)."` / the heal equivalent
  — the value must be 1–1000. `RequestDamage` already refuses
  values ≤ 0 before sending, so this fires for values above 1000.
- `"Item id is empty."` / `"Player already owns item …"` — the item
  id was empty, or the player already has that item (duplicates are
  rejected by design).

If you called `player.ApplyDamage(...)` directly on a client and
nothing happened, that is by design: it returns `false` on any
non-server instance. Clients must go through
`manager.RequestDamage(...)` — see
[Section 2.3](#23-the-server-validated-action-pattern).

### Q5. A player joins but never appears in `Players` — or a player who left is still there

- **Join, but no player object:** `MultiplayerManager` spawns a
  remote player in `HandleClientConnected` only when the instance
  is the server. On a client, remote players appear only when the
  server tells the client about them — and this package has no
  player-spawn broadcast for remote players over the network yet.
  In the loopback demo, simulated remote clients (via
  `SimulateRemoteClientJoin()`) do appear, because the loopback
  transport delivers the join to the same manager instance. Over
  real Netcode, a second client joining does not yet spawn a
  `NetworkPlayer` for that client on the first client's screen —
  that is a known gap, not a misconfiguration. See
  [Section 8](#8-limitations).
- **Player limit reached:** when `Players.Count` is already at
  `MaxPlayers`, the manager refuses the join and disconnects the
  client. Raise `MaxPlayers` in the config (maximum 64) if you
  need more players.
- **Left, but still listed:** `RemovePlayer` runs from
  `HandleClientDisconnected`, which fires when the transport
  reports the disconnect. With the loopback transport this is
  immediate. With the Netcode adapter it depends on
  `NetworkManager.OnClientDisconnectCallback` firing — if the
  client process was killed (not shut down cleanly), the disconnect
  may only be noticed after the connection timeout. Check
  `Connection Timeout (s)` in the config.
- **Local player missing after `StartClient`:** `StartClient`
  spawns the local player after the transport starts. If the
  transport's `StartClient` returned `false` (no NetworkManager,
  bad address/port), the session never started and no player was
  spawned — see Q3.

### Q6. I get duplicate event handling — my health bar updates twice

You subscribed twice, or a destroyed scene left stale listeners
behind. `MultiplayerEvents` is a static hub: its subscriptions
survive scene loads. Every `OnEnable` subscription needs a matching
`OnDisable` unsubscription, with the same method reference. The
demo (`DemoMultiplayer`) shows the pattern. If you are tearing a
scene down, call `MultiplayerEvents.ClearAllListeners()` — the
self-test does this in `TeardownManager`. Also check you do not
have two `MultiplayerEventBridge` components, or a bridge plus a
manual subscription to the same event, both updating the same UI.

### Q7. Can I turn off server authority to make testing easier?

You can — set **Server Authoritative** to `false` in the config —
but only for a local demo. With it off, clients can change their
own state, which is exactly what the server-validated pattern
exists to prevent. The custom inspector shows a warning when it is
off. There is no supported reason to ship a real game with it off,
and turning it off does not fix a validation problem — it hides it.
If your requests are being rejected, read the rejection reason
(`OnActionRejected`, see Q4) instead of turning authority off.

### Q8. Does this work with URP / Built-in / mobile / console?

The package contains no rendering code, so it works with URP,
Built-in, and any other render pipeline. It is plain C# plus
UnityEngine, with no platform-specific code. It has not been tested
on any platform, including mobile and console — see
[Section 8](#8-limitations). The Netcode adapter depends on
`com.unity.netcode.gameobjects`, so its platform support is whatever
that package supports in Unity 2022.3 LTS.

---

## 7. Performance notes

Honest note first: **no performance testing has been done.**
Version 0.1.0 is untested in Unity, so there are no measured frame
times, no bandwidth figures, and no tested player counts. The notes
below are design reasoning from the code, not measurements. Measure
before you quote numbers to anyone.

- **Event-based sync, not polling.** Health and inventory changes
  raise events; nothing in the package polls values every frame.
  The only per-frame work is `MultiplayerManager.Update`, which on
  the server adds `Time.deltaTime` to a heartbeat timer and resets
  it when it reaches the interval — negligible.
- **Player list lookups are dictionary lookups.** `Players` is a
  `Dictionary<ulong, NetworkPlayer>`; `ValidateAction` does a
  `ContainsKey` / `TryGetValue` per action. Validation cost per
  action is small and constant, not proportional to player count.
- **Inventory is a list plus a hash set.** `NetworkPlayer` keeps a
  `List<string>` (ordered, exposed as `Inventory`) and a
  `HashSet<string>` (for `HasItem` and duplicate checks). Duplicate
  checks are O(1). Inventory is per-player and small by design;
  do not store hundreds of items per player and expect the
  `ApplyServerInventory` full-replace path to be cheap.
- **Actions are small.** A `PlayerAction` is one client id, two
  short strings, one float, and one double. The cost that matters
  is how many actions per second your game sends, not the size of
  one action. The staleness check uses `Time.realtimeSinceStartupAsDouble`,
  which is cheap.
- **The loopback transport is in-process.** It delivers actions by
  invoking events synchronously on the same thread — there is no
  serialisation, no socket, and no latency. Its performance tells
  you nothing about real network performance. Measure with the
  Netcode adapter and real clients.
- **The Netcode adapter sends one RPC per action.**
  `SubmitActionServerRpc` (client → server) and
  `BroadcastActionClientRpc` (server → all clients) are reliable by
  default (`ReliableActions` is `true`). Reliable RPCs are the right
  choice for gameplay actions, but do not send them every frame —
  use them for discrete actions (damage, heal, item, interact), and
  put high-frequency state (positions, every frame) on a separate
  unreliable channel or a dedicated sync pass, which this package
  does not provide.
- **Player count.** The config caps `MaxPlayers` at 64, and
  `OnValidate` enforces it. That cap is a safety limit, not a
  tested capacity. The default of 8 is the only count the demo
  exercises (and only with simulated clients). Test with real
  clients before raising the limit, and remember the server is
  doing validation work per action per client — a dedicated server
  build is the right host for larger sessions, not a player's
  machine running as host.
- **No allocations called out as hot paths.** `PlayerAction` is a
  struct; `ValidateAction` allocates only its rejection string when
  an action fails. `ApplyServerBroadcast` and the event raises
  allocate nothing beyond the event invocation itself. This has not
  been profiled — treat it as a design observation, not a guarantee.

---

## 8. Limitations

Read this section before deciding this package fits your game.
It is deliberately blunt.

- **UNTESTED IN UNITY (v0.1.0).** The package has not been loaded
  in Unity, run in Play mode, or tested against the Netcode for
  GameObjects package. The code was written and reviewed locally
  only. The 18 self-test checks are written, not run. Run the test
  checklist in `README.md` in Unity 2022.3 LTS before using this
  package in a real project, and fix anything it finds. Do not
  treat this package as tested or production-ready.
- **`LocalLoopbackTransport` is offline/local only.** It is an
  in-memory transport for offline development and Play-mode
  self-tests. It has no socket, no latency, and no real remote
  clients — `SimulateRemoteClientJoin()` only pretends. It must
  not be used for real networked play.
- **No hosted server included.** This package contains no server
  binary, no hosting, no relay, and no lobby service. You provide
  the machine (or service) that runs the host or dedicated server
  build, and you handle firewall, port forwarding, and player
  discovery yourself — or with the separate Sdx17 Matchmaking
  system (see [Section 5.1](#51-matchmaking)).
- **No movement sync, no client-side prediction, no lag
  compensation.** There is no position sync, no NetworkVariable
  health sync, and no scene management in this package or in the
  Netcode adapter. Fast hit-scan combat needs a dedicated
  lag-compensation pass on top of this layer.
- **No remote-player spawn broadcast (known gap).** Over a real
  network, a client joining does not yet spawn a `NetworkPlayer`
  for that client on other clients' screens — the manager spawns
  remote players only from transport connect events on the server
  side. In the loopback demo this works because everything runs in
  one manager instance. Over Netcode, you need a spawn broadcast
  (or Netcode's own player-object spawning) before other clients
  see a new player. See [FAQ Q5](#q5-a-player-joins-but-never-appears-in-players--or-a-player-who-left-is-still-there).
- **Not a cheat-proof server.** Server validation stops a modified
  client from inventing health, items, or damage inside this layer,
  but a dedicated server, proper authentication, and rate limiting
  are still needed for a public game. There is no authentication in
  this package — any client that can connect can send actions, and
  the server's only defence is validation. There is no rate
  limiting: a client can send actions as fast as the transport
  allows.
- **Heartbeat is bookkeeping only.** `MultiplayerManager.Update`
  ticks a timer on the server; a real transport sends a packet
  there. In this version no heartbeat packet is sent, so a silent
  client is only noticed when the transport reports the disconnect.
- **No persistence.** `NetworkPlayer` inventory is session state
  only. Nothing is saved when a session ends. Use the separate
  Sdx17 Secure Save / DataStore system (see
  [Section 5.5](#55-secure-save-secure-datastore)).
- **No lobby or matchmaking.** It starts sessions and moves
  validated actions; finding players and forming a match is a
  separate system (see [Section 5.1](#51-matchmaking)).
- **Max 64 players, by design.** `MultiplayerConfig` rejects and
  clamps max players above 64. Larger sessions need a different
  architecture, not a bigger number in this config.
- **No authentication, no encryption.** The Netcode adapter moves
  actions over whatever transport Netcode is configured with.
  Encryption, login, and account systems are outside this package.
- **The `#if` guard symbol needs confirming on first Unity load.**
  See [FAQ Q1](#q1-i-installed-the-netcode-package-but-the-adapter-still-does-not-compile--what-do-i-check).
  If the symbol is not defined after installing the Netcode package,
  the adapter silently compiles to nothing.
- **No demo scene is included — only a demo script.** The package
  ships `Samples~/Demo/DemoMultiplayer.cs` and a setup guide
  (`Samples~/Demo/README.md`), not a ready-made `.unity` scene
  file. You build the demo scene by following that guide. The
  Asset Store listing describes the demo scene you build from it —
  see `LISTING.md`.
- **Editor support is minimal.** One custom inspector
  (`MultiplayerConfigEditor`) and three menu items
  (`MultiplayerMenu`). There is no setup wizard, no session
  monitor window, and no runtime debug overlay.

---

## File map

| File | What it does |
|---|---|
| `package.json` | UPM package definition (`com.sdx17.multiplayer`, 0.1.0, Unity 2022.3). |
| `README.md` | Overview, setup, public API summary, test checklist. |
| `Documentation~/GUIDE.md` | This guide. |
| `LISTING.md` | Asset Store listing draft (not submitted). |
| `SELLING_REQUIREMENTS.md` | Checklist to pass before submitting to the Asset Store. |
| `ThirdPartyNotices.txt` | Third-party notices (none bundled). |
| `CHANGELOG.md` | Version history. |
| `LICENSE` | MIT, Copyright 2026 Sdx17. |
| `Runtime/ITransport.cs` | `ITransport`, `PlayerAction`, `ConnectionMode`, `TransportState`, `LocalLoopbackTransport`. |
| `Runtime/MultiplayerConfig.cs` | `MultiplayerConfig` ScriptableObject. |
| `Runtime/NetworkPlayer.cs` | `NetworkPlayer`: health, inventory, server-only changes, server-directed mirrors. |
| `Runtime/MultiplayerManager.cs` | Session lifecycle, player list, server-validated actions, disconnect handling. |
| `Runtime/NetworkEvents.cs` | `MultiplayerEvents` static hub and `MultiplayerEventBridge`. |
| `Runtime/Netcode/NetcodeTransportAdapter.cs` | Optional Netcode adapter, guarded by `#if UNITY_NETCODE_GAMEOBJECTS \|\| UNITY_NETCODE`. |
| `Runtime/Netcode/README.md` | Netcode install and usage notes. |
| `Editor/MultiplayerEditor.cs` | Custom inspector for `MultiplayerConfig` and `Sdx17 > Multiplayer` menu items. |
| `Samples~/Demo/DemoMultiplayer.cs` | Demo MonoBehaviour: H/C/S to start, X damage, G heal, I item, Q shutdown. |
| `Samples~/Demo/README.md` | Minimal demo scene setup. |
| `Tests~/MultiplayerSelfTest.cs` | Play-mode self-test, 18 checks, logs PASS/FAIL per check plus a summary. |
| `Tests~/README.md` | What each self-test check proves. |

## License

MIT — see `LICENSE`. Copyright 2026 Sdx17.
