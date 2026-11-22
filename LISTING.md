# Asset Store listing draft — Sdx17 Multiplayer Architecture

> **DRAFT ONLY — not submitted. No Asset Store listing exists yet.**
> Version 0.1.0 is **UNTESTED IN UNITY**: the package has not been
> loaded in Unity, run in Play mode, or tested against the Netcode for
> GameObjects package. Do not submit this listing until every item in
> `SELLING_REQUIREMENTS.md` is passed, including a clean Unity 2022.3
> load, a playing demo scene, real screenshots, and a real recording.
> Nothing in this draft claims sales, reviews, or a live listing —
> there are none.

---

## Title

**Sdx17 Multiplayer Architecture**

## Short description (under 200 chars)

Server-authoritative multiplayer for Unity: host/client/server sessions, validated actions, loopback transport for offline dev, and an optional Netcode adapter. No paid dependencies.

*(Count: 182 characters.)*

## Long description

**Server-authoritative multiplayer for Unity 2022.3 LTS — without
a networking degree.**

Sdx17 Multiplayer Architecture gives your game a session layer you
can trust: a client asks, the server validates, and only then does
anything change. A modified client cannot invent health, items, or
damage — the server is the only authority, and the package is built
around that rule from the first line of code.

**What you get**

- **Host / client / dedicated-server modes** in one
  `MultiplayerManager`: start a session with `StartHost()`,
  `StartClient()`, or `StartServer()`, and shut it down with
  `Shutdown()`.
- **The server-validated action pattern.** A client calls
  `RequestDamage`, `RequestHeal`, `RequestAddItem`, or
  `RequestAction` and changes nothing locally. The server checks
  the sender, the target, the value (1–1000), and the timestamp —
  then applies the change and broadcasts the result. Stale or
  replayed actions are rejected by the staleness window.
- **A transport abstraction (`ITransport`).** Core code never talks
  to a networking library directly, so the transport is swappable.
  The in-memory `LocalLoopbackTransport` runs offline with no
  network, no package, and no server process — press Play, press H,
  and you have a working session. Swap in the Netcode adapter for
  real play without touching game logic.
- **Optional Netcode for GameObjects adapter.** Implements
  `ITransport` on top of Netcode, with a server RPC that checks the
  sender's real client id before the action ever reaches the
  manager. Guarded by `#if UNITY_NETCODE_GAMEOBJECTS ||
  UNITY_NETCODE`, so without the Netcode package installed the
  adapter compiles to nothing and the core still works.
- **`NetworkPlayer` with server-only state.** Health and inventory
  live on the server. Direct calls to `ApplyDamage`, `Heal`,
  `Respawn`, `AddItem`, or `RemoveItem` are refused on a client.
  Clients mirror the server's result through server-directed
  methods, and every change raises events.
- **Events, not polling.** `MultiplayerEvents` is one static hub
  for player spawned / left, health changed, inventory changed,
  validated actions, rejected actions, and connection changes —
  plus a `MultiplayerEventBridge` so you can wire UI in the
  Inspector without writing code.
- **`MultiplayerConfig` ScriptableObject.** Address, port, player
  limits (1–64), timeouts, and the server-authority flag in one
  asset, with `Validate()` and Editor clamping, a custom inspector
  that shows the validation result inline, and
  `Sdx17 > Multiplayer` menu items for one-click setup.
- **Demo and self-test included.** A demo script (H/C/S to start,
  X damage, G heal, I item, Q shutdown) and a Play-mode self-test
  with 18 checks that logs PASS/FAIL per check.

**What it is NOT**

- Not a lobby or a matchmaking service — it starts sessions and
  moves validated actions; finding players is a separate system.
- Not movement sync, client-side prediction, or lag compensation
  for fast combat. The staleness window is enough for slow or
  turn-style actions; fast hit-scan combat needs a dedicated
  lag-compensation pass on top.
- Not a cheat-proof server on its own. Server validation stops a
  modified client from inventing state inside this layer, but a
  dedicated server, proper authentication, and rate limiting are
  still needed for a public game.
- Not a hosted server. No server binary, relay, or hosting is
  included — you run the host or dedicated server build yourself.

**Honest status**

Version 0.1.0 is **untested in Unity**. The code was written and
reviewed locally; it has not been loaded in Unity, run in Play mode,
or tested against the Netcode package. The full test checklist is in
the package README — run it in Unity 2022.3 LTS before using this
in a real project. The guide (`Documentation~/GUIDE.md`) documents
the real API, the config, integration with the other Sdx17 systems,
and the known limitations, including the remote-player spawn gap
over a real network. Buy it for the architecture and the
server-validated pattern, not for a promise that it is
production-ready on day one.

**Part of the Sdx17 family**

This package is one system in the Sdx17 family. The guide documents
how it integrates with the other systems — Matchmaking, Combat
(server-authoritative), Admin, Inventory, Secure Save, Quest, NPC,
Dialogue, UI, Terrain Tools, and Developer Plugin — using only the
public API in this package. Those systems are separate products and
are not included here.

## Keywords (10)

1. multiplayer
2. networking
3. netcode
4. server authoritative
5. host client server
6. multiplayer architecture
7. validated actions
8. lobby session
9. unity multiplayer
10. network player

## Category

**Tools / Game Toolkits** (suggested — re-check at listing time;
pick the category the Asset Store offers that fits best when you
submit, and update this draft if it changes).

## Suggested price

**$39**

### Price justification — read before listing

- **What $39 buys:** the largest and most complex system in the
  Sdx17 family so far — a full session layer (transport abstraction,
  server-validated actions, player state, events, config, editor
  support, Netcode adapter, demo, and an 18-check self-test), not a
  single helper script. Comparable multiplayer / networking
  frameworks on the Asset Store typically sit above the price of a
  small utility, and a server-authoritative architecture with a
  swappable transport is a framework, not a snippet.
- **The honest flag:** a new publisher should normally price in the
  **$15–$35 band to get first reviews** — $39 is above that band.
  It is justified only as the largest/most complex system in the
  family, and only if the package passes every item in
  `SELLING_REQUIREMENTS.md` first (clean Unity load, playing demo,
  real screenshots, real video). A higher price with zero reviews
  and an honestly-stated "untested in Unity" v0.1.0 will sell
  slowly, if at all.
- **Recommendation:** re-check comparables before listing, and
  consider launching at **$35** instead — still above the typical
  first-review band, but closer to it, and easier to raise later
  once reviews exist than to cut after a slow launch.
- **Publisher revenue (before tax and refunds):** Unity pays the
  publisher 70% of the sale price. At $39 that is **$27.30** per
  sale; at $35 it is **$24.50** per sale. Those are gross publisher
  shares before tax and refunds — not profit, and not a promise of
  any number of sales.
- **No sales are guaranteed.** This draft claims no sales, no
  reviews, and no listing — there are none. The price does not
  guarantee sales. Price, screenshots, video, and the honest
  "untested" status will all affect whether anyone buys.

## What's included

- `Runtime/` — `ITransport.cs` (`ITransport`, `PlayerAction`,
  `ConnectionMode`, `TransportState`, `LocalLoopbackTransport`),
  `MultiplayerConfig.cs`, `MultiplayerManager.cs`,
  `NetworkPlayer.cs`, `NetworkEvents.cs`
  (`MultiplayerEvents`, `MultiplayerEventBridge`), and the runtime
  assembly definition.
- `Runtime/Netcode/` — `NetcodeTransportAdapter.cs` (optional,
  guarded by `#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE`) and
  its own README.
- `Editor/` — custom inspector for `MultiplayerConfig` and
  `Sdx17 > Multiplayer` menu items, with the editor assembly
  definition.
- `Samples~/Demo/` — demo script (`DemoMultiplayer.cs`, keys H/C/S/X/G/I/Q)
  and a demo scene setup guide.
- `Tests~/` — Play-mode self-test (`MultiplayerSelfTest.cs`, 18 checks)
  and its README.
- `Documentation~/GUIDE.md` — full guide: 5-minute quickstart,
  concepts, complete API reference, config reference, integration
  with the other 11 Sdx17 systems, troubleshooting / FAQ,
  performance notes, and honest limitations.
- `README.md`, `CHANGELOG.md`, `LICENSE` (MIT, Copyright 2026 Sdx17),
  `ThirdPartyNotices.txt`.

## Requirements / Dependencies

- **Unity 2022.3 LTS** (the package targets `2022.3`, release `0f1`).
- **URP / Built-in compatible.** The package contains no rendering
  code, so any render pipeline works.
- **No paid dependencies.**
- **`com.unity.netcode.gameobjects` is REQUIRED only for the Netcode
  adapter — and it is NOT bundled.** Install it separately
  (Window > Package Manager > + > Add package by name, or add
  `"com.unity.netcode.gameobjects": "1.8.1"` to
  `Packages/manifest.json`) if you want real networking. Without it,
  the adapter compiles to nothing and the core works on the
  loopback transport. The `#if` guard symbol
  (`UNITY_NETCODE_GAMEOBJECTS` or `UNITY_NETCODE`) needs confirming
  on first Unity load — see the guide's FAQ.
- **No hosted server included.** You run the host or dedicated
  server build yourself.

## Demo scene description

The package ships a demo **script and setup guide**, not a
ready-made `.unity` scene file. Build the demo scene by following
`Samples~/Demo/README.md`: create a `MultiplayerConfig` asset
(**Sdx17 > Multiplayer > Create Multiplayer Config**), create a
`MultiplayerManager` GameObject
(**Sdx17 > Multiplayer > Create Multiplayer Manager**) and assign
the config, then add the `DemoMultiplayer` component to a GameObject
in the scene. Press Play and press **H** to host on the loopback
transport — no network and no extra package needed. Press **X** to
request 10 damage (the Console logs the server validating it, then
the health change to 90/100), **G** to heal, **I** to grant the
item `"health-potion"`, **Q** to shut down. For real networking,
add a NetworkManager and a `NetcodeTransportAdapter` as described
in `Runtime/Netcode/README.md`. The screenshots and video for this
listing must show this real demo scene running in Unity — see
`SELLING_REQUIREMENTS.md`. No AI images may be presented as
screenshots.

## Support contact

**Sdx17 — GitHub: sdx-rf17** (`https://github.com/sdx-rf17`).

No email address is listed here: there is no support email for
this product, and no email address may be invented for the listing.
Support is through the publisher's GitHub profile linked above.
Update this section with a real support contact only when one
exists.

## Version history

- **0.1.0 (2026-11-21)** — First release. Transport abstraction
  (`ITransport`) plus `LocalLoopbackTransport`; `MultiplayerConfig`
  ScriptableObject with validation and clamping; `MultiplayerManager`
  (host / client / server, player list, server-validated actions,
  disconnect handling); `NetworkPlayer` (health, inventory,
  server-only changes, server-directed mirrors); `MultiplayerEvents`
  hub and `MultiplayerEventBridge`; optional Netcode for
  GameObjects adapter (guarded, not bundled); editor support
  (custom inspector, `Sdx17 > Multiplayer` menu); demo script and
  Play-mode self-test (18 checks). **UNTESTED IN UNITY** — see the
  README and guide.
