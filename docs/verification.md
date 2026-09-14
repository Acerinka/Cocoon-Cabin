# Publication checks

Checked on **15 September 2026** against the independent Cocoon Cabin publication checkout.

| Check | Result | Scope |
| --- | --- | --- |
| Unity 2022.3.53f1 import and script compilation | Passed | Fresh editor import with the locally installed environment dependency. |
| Authored scene load | Passed | 10,071 scene objects, one taxi state machine and no missing scripts. |
| Required model files | Passed | Model geometry is present rather than unresolved LFS pointers. |
| Repository structure and local documentation links | Passed | `Tools/verify_repository.py`. |
| English portfolio | Passed | Nine pages rendered and visually reviewed; searchable PDF text. |
| Figma package | Passed | Nine 2560 × 1440 SVGs; one embedded 3840 × 2160 PNG per page; 245 native editable text objects. ZIP matches the SVG source files. |

The editor check used the team's existing local Japanese City package. That package is excluded from the public source and must be restored under its own license. This check does not establish compatibility with a different version of that dependency.

Headless editor validation does not exercise VR tracking, shader rendering on a headset, Android deployment or the complete ride in Play Mode. The linked films remain the evidence for the recorded experience. No fresh headset user test or Figma import was performed for this publication pass.

The GitHub workflow repeats repository and portfolio checks. It does not run Unity or download the licensed environment.
