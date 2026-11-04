# Lockpick Minigame Integration

`Lockpick Minigame.prefab` is a reusable gimmick for the main escape-room project. It no longer starts globally or survives scene changes by itself.

## Basic setup

1. Place `Assets/Lockpick/Prefabs/Lockpick Minigame.prefab` in the gameplay scene.
2. Keep its GameObject active. The controller remains invisible and idle until opened.
3. From a door, drawer, or box interaction, call `LockpickGameController.Open()`.
4. Connect the prefab's Inspector events:
   - **On Completed**: unlock/open the target and advance the puzzle state.
   - **On Failed**: alert the yandere AI, consume an item, or increase danger.
   - **On Closed**: restore the owning interaction UI when needed.

## Runtime API

- `Open()` starts a fresh lock-picking attempt.
- `Close()` closes the overlay and restores the previous time scale and cursor state.
- `Restart()` restarts the current attempt.
- `IsOpen` reports whether the gimmick currently owns the screen.
- `Completed`, `Failed`, `Opened`, and `Closed` are available as C# events in addition to Inspector UnityEvents.

## Integration options

- **Pause World While Open** pauses the main game using `Time.timeScale = 0`; turn it off if the yandere should keep approaching during the minigame.
- **Show Cursor While Open** temporarily unlocks and shows the cursor, then restores its prior state.
- Effects and ambience volumes can be adjusted independently on the prefab.
- The minigame uses unscaled time, so its own countdown continues even when the main world is paused.

`SampleScene` uses `LockpickDemoLauncher` only so the gimmick still opens immediately for isolated testing. Do not add that launcher to production scenes.
