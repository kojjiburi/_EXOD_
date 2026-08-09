# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:\Users\진희원\Desktop\My project`
- Last analyzed: 2026-08-09
- Last analyzed commit: unavailable (`git` executable is not available in this environment)
- Current state: Unity 2D template with a reusable single-lock gimmick styled for a yandere escape-room group project

## Confirmed Environment

- Unity version: Unity 6.3, `6000.3.16f1`
- Render pipeline: Universal Render Pipeline using the 2D Renderer
- Input system: Unity Input System only (`activeInputHandler: 1`)
- Target platforms: not explicitly constrained; quality profiles include desktop, mobile, and WebGL defaults

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 17.3.0 with a 2D Renderer asset | Confirmed | `Packages/manifest.json`, `Assets/Settings/Renderer2D.asset`, `ProjectSettings/QualitySettings.asset` |
| Input | Input System 1.19.0 with a default actions asset | Confirmed | `Packages/manifest.json`, `Assets/InputSystem_Actions.inputactions`, `ProjectSettings/ProjectSettings.asset` |
| UI | uGUI 2.0.0 is available | Confirmed | `Packages/manifest.json` |
| Audio | Runtime-generated 2D ambience and lock interaction effects; no mixer or imported clips | Confirmed | `Assets/Lockpick/Scripts/LockpickGameController.cs` |
| Testing | Unity Test Framework 1.6.0 is installed | Confirmed | `Packages/manifest.json` |
| Unity MCP | Official Unity AI Assistant 2.16.0-pre.1 is installed and console reads work | Confirmed | `Packages/manifest.json`, successful Unity console probe |
| Networking | Multiplayer Center is installed, but no first-party multiplayer usage exists | Confirmed | `Packages/manifest.json`, `Assets/` inspection |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scenes` | Project scenes | Confirmed | Contains `SampleScene.unity` |
| `Assets/Settings` | URP and 2D renderer configuration | Confirmed | Renderer and pipeline assets |
| `Assets/Lockpick/Scripts` | Lock-picking rules, input adapter, and prototype presentation | Confirmed | `LockpickGameModel.cs`, `LockpickGameController.cs` |
| `Assets/Lockpick/Prefabs` | Reusable production-ready minigame entry point | Confirmed | `Lockpick Minigame.prefab` |

## Assembly Boundaries

No first-party `.asmdef` or `.asmref` files exist. Runtime code currently compiles into the default `Assembly-CSharp` assembly.

## Scenes And Startup Flow

- Build scenes: `Assets/Scenes/SampleScene.unity` (enabled)
- Likely startup scene: `Assets/Scenes/SampleScene.unity`
- Scene loading flow: none detected

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Runtime structure | Plain C# model owns lock rules; a reusable controller owns presentation, input, audio, lifecycle, and integration signals | Confirmed | `Assets/Lockpick/Scripts/LockpickGameModel.cs`, `Assets/Lockpick/Scripts/LockpickGameController.cs` |
| Integration | Main-game interactions call `Open()`; completion, failure, opening, and closing are exposed as UnityEvents and C# events | Confirmed | `Assets/Lockpick/Scripts/LockpickGameController.cs`, `Assets/Lockpick/README.md` |
| Scene composition | SampleScene contains a demo-only launcher; production scenes use the prefab without the launcher | Confirmed | `Assets/Scenes/SampleScene.unity`, `Assets/Lockpick/Scripts/LockpickDemoLauncher.cs` |

## Coding Conventions

- Namespace style: new gameplay code uses `LockpickPrototype`
- Serialized fields: prefab exposes pause, cursor, audio-volume, and UnityEvent integration settings
- Async: none established
- Comments/docs: default Unity template comments only

## Testing And Validation

- EditMode tests: none detected
- PlayMode tests: none detected
- CI/build validation: none detected; Unity dynamic validation covers single-lock completion and restart behavior

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| `unity.connection.status` | available | Successful Unity MCP console probe |
| `unity.editor.version` | available from repository | `ProjectSettings/ProjectVersion.txt` |
| `unity.console.read` | available | Successful Unity MCP console probe |
| `unity.scene.inspect` | unverified | No read-only hierarchy tool exposed in the current tool set |
| `unity.buildsettings.read` | available from repository | `ProjectSettings/EditorBuildSettings.asset` |
| `unity.tests.run` | unverified | No test tool exposed in the current tool set |
| `unity.playmode.read` | unverified | No play-mode status tool exposed in the current tool set |

## Important Constraints

- Preserve the existing URP 2D and Input System setup.
- Avoid adding packages for the initial prototype.
- The minigame must not auto-create globally or persist itself across scenes; the owning gameplay interaction controls its lifetime.
- `LockpickDemoLauncher` is restricted to isolated demo scenes.

## Unknowns And Confidence

- Confirmed art direction: intimate psychological-horror/yandere escape-room presentation with aged door hardware, faded rose accents, handwritten notes, and an approaching-footsteps timer.
- Confirmed audio direction: quiet room ambience, metallic hairpin interactions, unlock clunk, approaching footsteps on failure, and a low-time heartbeat.
- Final target platform, difficulty curve, volume settings, and progression integration are not specified.
- `SampleScene.unity` currently references a removed placeholder script from the former `Assets/coding/helloworld.cs`, producing one missing-script warning. The lock-picking prototype does not depend on that object.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/ProjectSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Lockpick/Scripts/LockpickGameModel.cs`
- `Assets/Lockpick/Scripts/LockpickGameController.cs`
- `Assets/Lockpick/Scripts/LockpickDemoLauncher.cs`
- `Assets/Lockpick/Prefabs/Lockpick Minigame.prefab`
- `Assets/Lockpick/README.md`

<!-- unity-onboarding:generated:end -->
