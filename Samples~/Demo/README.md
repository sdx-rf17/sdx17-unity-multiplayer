# Demo — minimal demo scene

No `.unity` scene file is included in this package. Build the demo
scene yourself in under 5 minutes with the steps below.

## Scene setup

1. Create a new empty scene (File > New Scene > Basic).
2. Create a Multiplayer Config: **Sdx17 > Multiplayer > Create
   Multiplayer Config**. Leave the defaults (127.0.0.1 : 7777,
   8 players, server authority on).
3. Create a manager: **Sdx17 > Multiplayer > Create Multiplayer
   Manager**. This adds a `MultiplayerManager` GameObject to the scene.
4. Assign the config asset from step 2 to the manager's **Config**
   field in the Inspector.
5. Create an empty GameObject called `Demo`, add the
   `DemoMultiplayer` component to it (this file's script), and drag
   the `MultiplayerManager` GameObject into its **Manager** field.
6. Optional: create a `NetworkPlayer` prefab (an empty GameObject
   with a `NetworkPlayer` component, saved as a prefab) and assign it
   to the manager's **Player Prefab** field. Without a prefab the
   manager creates a plain GameObject with a `NetworkPlayer` for each
   player, which is enough for this demo.
7. Optional: add a `MultiplayerEventBridge` to a UI GameObject and
   wire its UnityEvents to UI text, so joins and health changes show
   on screen instead of only in the Console.

## Running it

1. Press Play.
2. Press **H** to start as host. The Console logs the local player
   spawning with 100/100 health.
3. Press **X** to request 10 damage to the local player. The Console
   logs the server validating the action, then the health change to
   90/100. Notice the damage only happens because the server
   validated it — the demo never changes health directly.
4. Press **G** to request a heal, **I** to request the item
   `health-potion`.
5. Press **Q** to shut the session down. The player list empties.

## Two-instance test (real network, optional)

This needs the Netcode package — see `Runtime/Netcode/README.md`.

1. Install `com.unity.netcode.gameobjects`.
2. Add a NetworkManager to the scene and a
   `NetcodeTransportAdapter` to the manager GameObject; before
   starting, call `manager.SetTransport(adapter)` (add that line in
   a small script, or set it from the demo script).
3. Build the project (File > Build Settings > Build), run the build
   as a client, and run the Editor as host — or the other way round.
4. Connect with the same address and port as the config. Damage and
   heal requests from the client must still be validated by the host:
   check the host Console for the validation log lines.
