# Tests

`MultiplayerSelfTest.cs` is a Play-mode self-test, not a Unity Test
Framework test assembly. That is deliberate: it runs with no extra
packages, in any scene, against the loopback transport.

## Running it

1. Create an empty GameObject in a test scene.
2. Add the `MultiplayerSelfTest` component to it.
3. Press Play and read the Console.

## What it checks

| Check | What it proves |
|---|---|
| ConfigValid | The default config passes validation (port 7777, 8 players). |
| ConfigCloneForLocalDemo | A local-demo clone uses loopback with server authority on. |
| StartHost | Host mode starts and reports running / server / host. |
| LocalPlayerSpawned | The local player spawns at 100 health, alive. |
| PlayerListHasLocalPlayer | The player list contains the local client id. |
| ValidatedDamageApplies | A damage request through the server reduces health 100 → 75. |
| InvalidDamageRefused | A zero-damage request is refused and health is unchanged. |
| ClientCannotDirectDamage | A non-server player refuses `ApplyDamage`. |
| ClientCannotDirectAddItem | A non-server player refuses `AddItem`. |
| ServerAddItem | The server player can add an item. |
| DuplicateItemRejected | The same item cannot be added twice. |
| ServerRemoveItem | The server player can remove an item it owns. |
| UnknownSenderRejected | An action from an unknown client id fails validation. |
| StaleActionRejected | An action older than the staleness window fails validation. |
| ValidatedHealApplies | A heal request through the server restores health 75 → 85. |
| RemoteClientJoins | A simulated remote client appears in the player list. |
| DisconnectRemovesPlayer | Disconnecting that client removes it from the player list. |
| ShutdownClearsPlayers | Shutdown stops the session and empties the player list. |

Every check logs `PASS:` or `FAIL:` plus a final summary line
with the pass / fail counts. A failure is logged with
`Debug.LogError`, so it also shows in the Console error count.
