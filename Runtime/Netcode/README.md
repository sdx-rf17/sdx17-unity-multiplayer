# Netcode adapter

This folder holds the optional Netcode for GameObjects adapter.
It is separate from the core files on purpose.

## Install first

1. In Unity: **Window > Package Manager > + > Add package by name**
   and add `com.unity.netcode.gameobjects` (use the version listed for
   Unity 2022.3 LTS in the Package Manager).
2. Or add this line to your project's `Packages/manifest.json`
   dependencies: `"com.unity.netcode.gameobjects": "1.8.1"`.
3. Restart the Editor if the package asks for it.

## How it compiles

`NetcodeTransportAdapter.cs` is wrapped in:

```csharp
#if UNITY_NETCODE_GAMEOBJECTS || UNITY_NETCODE
// ... adapter code, references Unity.Netcode types ...
#endif
```

- **Without the package installed**, the file compiles to nothing.
  The core files in `Runtime/` compile standalone and the
  `LocalLoopbackTransport` works for offline development and tests.
- **With the package installed**, the adapter compiles and implements
  `ITransport` on top of `NetworkManager`, so `MultiplayerManager`
  works over a real network with no changes to game code.

## Use

1. Add a `NetworkManager` to your scene (GameObject > Netcode >
   NetworkManager) and configure its transport (the default
   UnityTransport uses the address and port from your
   `MultiplayerConfig` — set them to match).
2. Add this adapter component to the same GameObject as your
   `MultiplayerManager`, or to the NetworkManager GameObject.
3. Before starting a session, hand the adapter to the manager:

```csharp
var adapter = FindObjectOfType<NetcodeTransportAdapter>();
manager.SetTransport(adapter);
manager.StartHost();
```

The adapter forwards `NetworkManager` connection events into the
`ITransport` events the manager already listens to, and it forwards
validated actions through Netcode RPCs. Server validation still
happens in `MultiplayerManager.ValidateAction` — the transport never
decides whether an action is allowed.

## What is NOT here

There is no movement sync, no NetworkVariable health sync and no
scene management in this adapter. Those belong in game code built on
`NetworkPlayer` and `MultiplayerEvents`. The adapter only moves
connection events and validated actions.
