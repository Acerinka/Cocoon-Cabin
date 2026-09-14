# Assets and attribution

## Japanese City streetscape

The recorded Unity scene uses **Japanese City by KraftMaru**, a separately licensed Unity Asset Store environment:

- [Official asset page](https://assetstore.unity.com/packages/3d/environments/urban/japanese-city-240217)
- [Unity Asset Store terms and EULA](https://unity.com/legal/as-terms)

The public repository excludes `Assets/JapaneseCity` and its root `.meta` file. It retains the Cocoon scene's references so a licensed local copy can restore the authored environment. The portfolio and films show the original combined prototype.

Install your licensed copy into `Assets/JapaneseCity`, keeping the package's original `.meta` GUIDs. Use the package version compatible with Unity 2022.3. The currently listed store version may differ from the one used in the project; buying or installing a newer package is not a guarantee that its asset GUIDs match the recorded scene. The repository does not redistribute this environment's meshes, textures, prefabs or demo scenes, and provides no substitute license.

Without the package, the code can be inspected but the recorded streetscape and its referenced environment objects will be incomplete. Do not treat an incomplete scene as a verified VR build.

## Project models and artwork

`Assets/ImportedAssets/CocoonTaxi` contains the vehicle, luggage, seat and wheelchair assets used in the team's academic prototype. `Assets/CocoonPrototype/Resources` contains the project's screen artwork. These are included as project context and implementation assets; the repository's MIT code license does not grant a separate license to reuse the artwork or models. Original ownership, source licenses and applicable third-party rights remain with their respective holders.

The Seat V2 portfolio render presents the original model geometry. It does not represent a separate manufactured or validated mechanism. Vehicle names or marks appearing in source assets or screenshots do not imply affiliation or endorsement.

## Software and typography

Unity packages are restored from the versions in `Packages/manifest.json` and `Packages/packages-lock.json`. Their own licenses apply. The web gesture application is maintained in the [team's separate repository](https://github.com/MoeMaelGit/AHMI_HandGesture_Demo_00); its source is linked, not copied into this project.

The English portfolio uses [Inter](https://rsms.me/inter/). Its editable SVG package references the font by name; font files are not bundled. Install Inter Regular and SemiBold before editing the SVGs in Figma. Bitmap screenshots retain any typography already present in the captured interfaces.

## Team

Report credits: Duygu Can, Vincenzo Lomuscio, Ma-El-Ainin Mohammad Oussama, Huiyu Yang and Yuchen Zhang. Group 6 is the course group number.

Yuchen Zhang's stated contribution is all Unity development, the interaction concept, selected UI work and user research. The web implementation and cabin modelling are team contributions, not claimed as individual Unity-developer work.
