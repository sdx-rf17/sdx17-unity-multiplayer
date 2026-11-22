# Sdx17 Multiplayer Architecture

> **Version 0.1.0 — UNTESTED IN UNITY**
>
> This package has **not** been loaded in Unity, run in Play mode, or
> tested against the Netcode for GameObjects package. The code was
> written and reviewed locally only. Do not treat it as tested or
> production-ready. Run the test checklist at the bottom of this file
> in Unity 2022.3 LTS before using it in a real project, and fix
> anything the checklist finds.

Server-authoritative multiplayer for Unity 2022.3 LTS, written in C#,
URP-compatible, with no paid dependencies. The core compiles with no
extra packages; the optional Netcode for GameObjects adapter needs
`com.unity.netcode.gameobjects` (see "Setup").

## What it is

- A transport abstraction (`ITransport`) with an in-memory loopback
  transport for offline development and Play-mode self-tests.
- `MultiplayerManager`: host / client / dedicated-server modes, a
  player list, spawn and disconnect handling.
- `NetworkPlayer`: spawn, health sync and inventory sync through
  events, with server-only state changes.
- The server-validated action pattern: a client sends a request, the
  server validates it (known sender, alive, value in range, not
  stale), and only then applies it and broadcasts the result. A
  client can never change its own health or inventory directly.
- A `MultiplayerConfig` ScriptableObject for address, port, player
  limits, timeouts and the server-authority flag.
- Editor support: a custom inspector for the config and
  `Sdx17 > Multiplayer` menu items.
- A demo script and a Play-mode self-test that logs PASS/FAIL per check.

## What it is NOT

- Not a complete game, not a lobby, and not a matchmaking service.
  It starts sessions and moves validated actions; finding players
  and forming a match is a separate system (see "Integration notes").
- Not movement sync, not client-side prediction and not lag
  compensation for fast combat. It rejects stale actions (see
  `ActionStalenessWindowMs`), which is enough for slow or turn-style
  actions. Fast hit-scan combat needs a dedicated lag-compensation
  pass on top of this layer.
- Not a cheat-proof server. Server validation stops a modified
  client from inventing health, items or damage inside this layer,
  but a dedicated server, proper authentication and rate limiting
  are still needed for a public game.
- Not tested. See the header: v0.1.0 is untested in Unity.

## Setup — under 5 minutes

### Core only (no extra packages)

1. Copy this package into your project (`Packages/com.sdx17.multiplayer`)
   or add it as a local package in the Package Manager.
2. **Sdx17 > Multiplayer > Create Multiplayer Config** — leave the
   defaults (127.0.0.1 : 7777, 8 players, server authority on).
3. **Sdx17 > Multiplayer > Create Multiplayer Manager** and assign
   the config to the manager's **Config** field.
4. Press Play, press **H** (host). That is a working session using
   the loopback transport — no network and no extra package needed.
5. Follow `Samples~/Demo/README.md` to add the demo script.

### Real networking (Netcode for GameObjects)

1. Install the package: **Window > Package Manager > + > Add
   package by name**, name `com.unity.netcode.gameobjects` (use the
   version listed for Unity 2022.3 LTS). Or add
   `"com.unity.netcode.gameobjects": "1.8.1"` to
   `Packages/manifest.json` and let Unity resolve it.
2. Add a NetworkManager to your scene (GameObject > Netcode >
   NetworkManager) and set its transport address and port to match
   your `MultiplayerConfig`.
3. Add a `NetcodeTransportAdapter` component (in
   `Runtime/Netcode/`) to the manager GameObject.
4. Before starting a session, hand it to the manager:

```csharp
manager.SetTransport(FindObjectOfType<NetcodeTransportAdapter>());
manager.StartHost();
```

5. Read `Runtime/Netcode/README.md` for the compile guard and the
   full notes. Without the Netcode package installed, the adapter
   file compiles to nothing and the core still works.

## Files

| File | What it does |
|---|---|
| `package.json` | UPM package definition (`com.sdx17.multiplayer`, 0.1.0, Unity 2022.3). |
| `README.md` | This file. |
| `CHANGELOG.md` | Version history. |
| `LICENSE` | MIT, Copyright 2026 Sdx17. |
| `.gitignore` | Unity standard ignores; `.meta` files are kept. |
| `Runtime/Sdx17.Multiplayer.Runtime.asmdef` | Runtime assembly definition. |
| `Runtime/ITransport.cs` | `ITransport`, `PlayerAction`, `ConnectionMode`, and `LocalLoopbackTransport`. |
| `Runtime/MultiplayerConfig.cs` | `MultiplayerConfig` ScriptableObject with validation and clamping. |
| `Runtime/NetworkPlayer.cs` | `NetworkPlayer`: health, inventory, server-only changes, server-directed mirrors. |
| `Runtime/MultiplayerManager.cs` | Session lifecycle, player list, server-validated actions, disconnect handling. |
| `Runtime/NetworkEvents.cs` | `MultiplayerEvents` static hub and `MultiplayerEventBridge` for Inspector wiring. |
| `Runtime/Netcode/NetcodeTransportAdapter.cs` | Optional Netcode adapter, guarded so it only compiles with the package present. |
| `Runtime/Netcode/README.md` | Netcode install and usage notes. |
| `Editor/Sdx17.Multiplayer.Editor.asmdef` | Editor assembly definition (references the runtime assembly). |
| `Editor/MultiplayerEditor.cs` | Custom inspector for `MultiplayerConfig` and `Sdx17 > Multiplayer` menu items. |
| `Samples~/Demo/DemoMultiplayer.cs` | Demo MonoBehaviour: H/C/S to start, X damage, G heal, I item, Q shutdown. |
| `Samples~/Demo/README.md` | Minimal demo scene setup (GameObjects to create, components to add). |
| `Tests~/MultiplayerSelfTest.cs` | Play-mode self-test, logs PASS/FAIL per check plus a summary. |
| `Tests~/README.md` | What each self-test check proves. |

## Config options

All fields live on the `MultiplayerConfig` ScriptableObject.

| Field | Default | Notes |
|---|---|---|
| Server Address | `127.0.0.1` | Address a client connects to. |
| Server Port | `7777` | Must match the transport's port. 0 is rejected. |
| Connection Timeout (s) | `10` | A client silent for longer than this counts as disconnected. |
| Max Players | `8` | 1–64. Joining past the limit is refused and the client is disconnected. |
| Max Health | `100` | Starting and maximum health for every player. |
| Server Authoritative | `true` | Keep on. Off means clients can change their own state — local demo use only. |
| Action Staleness Window (ms) | `5000` | Actions older than this are rejected as stale (lag / replay guard). |
| Heartbeat Interval (s) | `2` | How often the server heartbeat timer ticks. |

`Validate()` returns a human-readable problem or `null` when the
config is usable; `OnValidate` clamps out-of-range values in the
Editor. The custom inspector shows the validation result inline.

## Public API

```csharp
using Sdx17.Multiplayer;

// Manager: start a session (config assigned in the Inspector).
var manager = FindObjectOfType<MultiplayerManager>();
manager.StartHost();    // server + local client
manager.StartClient();  // join a server
manager.StartServer();  // dedicated server, no local player
manager.Shutdown();

// Player list and local player.
IReadOnlyDictionary<ulong, NetworkPlayer> players = manager.Players;
NetworkPlayer local = manager.LocalPlayer;
bool running = manager.IsRunning;
bool isServer = manager.IsServer;

// Client requests — these change nothing locally. The server validates
// each one, applies it, and broadcasts the result.
manager.RequestDamage(targetClientId, 25);
manager.RequestHeal(10);
manager.RequestAddItem("health-potion");
manager.RequestAction("interact", "door-01", 0f);

// Server-side: validate and apply by hand (the manager normally does
// this when an action arrives through the transport).
string problem = manager.ValidateAction(action); // null = valid
bool applied = manager.ApplyValidatedAction(action);

// Server-side: disconnect a remote client (removes its player).
manager.DisconnectPlayer(clientId);

// Listen for state changes (UI, audio, game systems).
MultiplayerEvents.OnPlayerSpawned += player => { /* show player */ };
MultiplayerEvents.OnHealthChanged += (player, health, max) => { /* update bar */ };
MultiplayerEvents.OnInventoryChanged += (player, itemId, added) => { /* update bag */ };
MultiplayerEvents.OnValidatedAction += action => { /* react to a validated action */ };
MultiplayerEvents.OnActionRejected += (action, reason) => { /* log why */ };
MultiplayerEvents.OnPlayerLeft += (clientId, name) => { /* remove from UI */ };

// Swap the transport before starting (for example, the Netcode adapter).
manager.SetTransport(new LocalLoopbackTransport());
manager.SetTransport(FindObjectOfType<NetcodeTransportAdapter>());

// NetworkPlayer, server only (refused on a client instance).
player.ApplyDamage(10);
player.Heal(10);
player.Respawn();
player.AddItem("sword");
player.RemoveItem("sword");
bool owned = player.HasItem("sword");
```

### The server-validated pattern, in one paragraph

A client calls `RequestDamage` and changes nothing. The request
reaches the server, `ValidateAction` checks that the sender is a
known connected player, is alive, the value is in range (1–1000)
and the request is not stale. Only then does the server apply the
damage and call `BroadcastValidatedAction`, which reaches the other
clients through the transport. Clients mirror the result with the
server-directed methods on `NetworkPlayer`. A client that edits its
own health directly is refused by `NetworkPlayer`, and the server
never accepts health or inventory values sent by a client.

### Lag note

A request carries the client's timestamp. The server rejects any
request older than `ActionStalenessWindowMs` (default 5000 ms), so a
delayed or replayed packet cannot apply twice or land a hit that was
already answered. There is no client-side prediction and no
rewind-and-replay for movement or aiming in this version.

## Integration notes with other Sdx17 systems

- **Matchmaking:** this package starts and runs a session; it does
  not find players. A matchmaking system should hand this package a
  formed match — a server address, a port and the list of client ids
  — and then call `StartClient` (or `StartHost` for the host) with a
  config built from that match. The player list in
  `MultiplayerManager.Players` is the source of truth for who is in
  the session once it starts.
- **Combat (server-authoritative):** combat code should send its
  actions through `RequestAction` / `RequestDamage` and listen to
  `OnValidatedAction`, never apply damage locally. Hit detection
  belongs on the server: a client reports "I fired at X", the server
  checks range, line of sight and the shooter's state, then applies
  damage. `NetworkPlayer.ApplyDamage` already refuses non-server
  callers, so combat built on it inherits that rule.
- **Admin:** an admin system can use `DisconnectPlayer` to kick a
  client, read `Players` for a live player list, and listen to
  `OnActionRejected` to see rejected (possibly abusive) requests.
  Admin commands must run on the server only — a client sending an
  admin action through `RequestAction` is just another action for
  the server to validate and reject unless the sender is an admin.
- **Inventory / DataStore:** `NetworkPlayer` inventory is session
  state only; it is not saved. A DataStore system should save
  inventory on session end (listen to `OnPlayerLeft` / shutdown)
  and restore it by granting items through the server after spawn
  (`AddItem` on the server instance), never by trusting an inventory
  list a client sends.

## Test checklist for Unity

Do these in Unity 2022.3 LTS before calling this package working.
Tick each one off only after seeing the result yourself.

### Play test (core, no extra packages)

1. Open the package in a Unity 2022.3 LTS project and confirm the
   Editor opens with no compile errors in the Console.
2. Build the demo scene from `Samples~/Demo/README.md` (config,
   manager, demo GameObject).
3. Press Play, press **H**. Confirm the Console logs the local
   player spawning at 100/100 health.
4. Press **X**. Confirm the Console logs the server validating the
   damage action, then the health change to 90/100.
5. Press **G**, then **I**. Confirm the heal and the item grant are
   logged as validated actions.
6. Press **Q**. Confirm the session stops and the player list empties.
7. Add `MultiplayerSelfTest` to a GameObject in a test scene and
   press Play. Confirm the summary line reads **18 passed, 0 failed**.
   If any check fails, fix it before using the package.

### Demo scene steps (Netcode, optional)

1. Install `com.unity.netcode.gameobjects` as described in
   `Runtime/Netcode/README.md`.
2. Confirm the project still compiles with no errors — the adapter
   must now compile, and the core must be unchanged.
3. Add a NetworkManager and a `NetcodeTransportAdapter`, set the
   transport address/port to match the config, and hand the adapter
   to the manager before starting.
4. Build a standalone player, run the build as a client and the
   Editor as host, and confirm the client appears in the host's
   player list.
5. From the client, request damage. Confirm the host Console logs
   the validation and the client sees the health change — and that a
   modified value (for example, a damage request of 0 or 100000)
   is rejected on the host.

## Requirements

- Unity 2022.3 LTS, C#, URP-compatible (no rendering code is included,
  so any render pipeline works).
- No paid dependencies. The only optional dependency is
  `com.unity.netcode.gameobjects` for the Netcode adapter.

## License

MIT — see `LICENSE`. Copyright 2026 Sdx17.
