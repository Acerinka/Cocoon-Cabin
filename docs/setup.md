# Setup and operation

## Versions

| Dependency | Project version |
| --- | --- |
| Unity Editor | **2022.3.53f1** |
| Universal Render Pipeline | 14.0.11 |
| Input System | 1.11.2 |
| XR Interaction Toolkit | 2.5.4 |
| OpenXR | 1.14.2 |
| XR Hands | 1.4.1 |
| Meta OpenXR | 1.0.1 |
| XR Plug-in Management | 4.4.1 |

The dependency manifests are authoritative. Use this editor version first rather than automatically upgrading the project.

## Clone and import

```bash
git lfs install
git clone --branch main --single-branch https://github.com/Acerinka/Cocoon-Cabin.git
cd Cocoon-Cabin
git lfs pull
```

The large model and image assets use Git LFS. A small text file beginning `version https://git-lfs.github.com/spec/v1` is a pointer, not the actual model/image. Resolve pointers before opening Unity.

Import the separately licensed Japanese City environment at `Assets/JapaneseCity`, retaining the original `.meta` files. The public checkout preserves the authored scene, which depends on this package. [Details and version caveat](third-party-assets.md).

Open `Assets/Scenes/Cocoon_OnboardingVR.unity`. Preserve the model transforms and screen planes. The **Cocoon → Setup Quest VR Prototype** command preserves an existing authored scene; it is not a reason to regenerate or replace that scene.

## Quest

Use a Meta Quest with tracked controllers for the documented control path. The project includes OpenXR hand-tracking support, but controller buttons are used for locomotion, luggage and seating interactions.

Install Android Build Support, SDK/NDK and OpenJDK with Unity Hub. Select Android, check OpenXR project validation, connect a development-enabled headset and build. **Cocoon → Build Quest APK** writes a development APK to `Builds/CocoonQuestPrototype.apk`.

Follow the in-world prompts: request and confirm, wait for the taxi to stop, tap the right-hand virtual card, enter with luggage, allow storage, sit, request a drop-off and leave.

## Controls

| Input | Context and effect |
| --- | --- |
| Raised hand/controller | Two-stage request. The first hold also checks facing, range and approach. |
| Right-hand card touching `DOORUI` | Confirm entry once the taxi is stopped. |
| Left grip | Luggage handling. |
| Left stick | Continuous movement when enabled. |
| Right stick | Snap turning. |
| Left trigger, hold and release | Teleport aim and execution. |
| Right A | Sit or stand at eligible stages; reset after completed drop-off. |
| Right B while seated | Begin the destination drop-off sequence. |

Desktop fallbacks exist for development: **Space** supplies the raised-hand input when target conditions are met, **A** supplies seat input, **B** requests a drop-off while seated, and **R** resets the prototype. These do not replace a full tracked VR interaction test.

## Validation

Run `python Tools/verify_repository.py` to check repository structure, documentation links and portfolio packaging. It does not run Unity or validate headset behaviour.

For a licensed local checkout with all external assets restored, the Unity editor validation entry point is:

```text
Unity -batchmode -nographics -quit -projectPath <checkout> -executeMethod CocoonPrototype.Editor.CocoonProjectValidation.Validate -logFile <log-file>
```

It checks required assets, scene loading, missing scripts and the core state machine. A successful editor check does not replace building and testing on a Quest.

## Troubleshooting

- **Missing city objects:** import the external environment package, preserving its GUIDs. A newer package version may not match the scene.
- **Models fail to import:** run `git lfs pull`; check that the OBJ files contain geometry rather than LFS pointer text.
- **Pink materials:** confirm URP 14.0.11 was restored and the project's pipeline settings remain assigned.
- **Gesture does not advance:** face the approaching taxi and keep the tracked hand/controller raised for the required hold. This checks spatial context, not only a button press.
- **Door stays closed:** use the virtual card at the door-side reader and allow the ramp sequence to complete.
- **Seating does not start:** wait for luggage storage and seat clearance conditions before pressing A.
- **Android build fails:** inspect OpenXR validation, Android SDK/NDK installation and the first relevant Unity error. Do not regenerate the authored scene as a general repair.
