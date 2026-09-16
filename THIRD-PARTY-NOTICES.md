# Third-party notices

This repository is a developer tutorial. It redistributes third-party material
alongside Microtube Technologies' own work, and the two are not under the same
terms. [`LICENSE`](LICENSE) covers only Microtube's part; everything below is
someone else's.

Entries marked **UNRESOLVED** are ones whose redistribution rights have not
been established. They ship today because the demo scenes reference them, but
they are the open items on this list — treat them as blockers for any
commercial redistribution of this repo.

---

## Packages

### PICO Unity OpenXR Integration SDK — `Packages/Unity OpenXR IntegrationSDK-1.4.0-20250407/`

`com.unity.xr.openxr.picoxr` v1.4.1, from PICO (ByteDance). Vendored rather
than fetched, because it is not published to any package registry — it is a
zip download from <https://developer.picoxr.com/resources/>.

Proprietary. Redistributed under PICO's developer terms; **not** MIT, and not
sublicensed by this repository. Includes native binaries
(`libopenxr_pico.so`, `libpxrplatformloader.so`, `VolcEngineRTC.dll` and
others) and PICO's own source with Chinese-language developer comments.

### MRTK Graphics Tools — `Packages/com.microsoft.mrtk.graphicstools.unity/`

v0.7.1, Microsoft Corporation. MIT — see the package's own `LICENSE.md`.

Embedded rather than consumed as a tarball because it carries **two local
patches**, both marked `// Local patch:` in source:

- `Runtime/MeshCombiner/MeshUtility.cs` — `Object.GetInstanceID()` is
  obsolete-as-error from Unity 6.3, so the id comes from `EntityId` instead.
- `Runtime/Utilities/MaterialRestorer.cs` — passes `EntityId` so the
  non-obsolete `AssetDatabase.TryGetGUIDAndLocalFileIdentifier` overload binds.

Neither can be suppressed with a `#pragma`, and upstream has no fix: the
newest release (v0.8.1, Nov 2024) predates Unity 6.3, and even `main` still
calls `GetInstanceID()`. The fork is therefore load-bearing, not incidental —
`Assets/Gameobjects/org.mixedrealitytoolkit.extendedassets/Materials/HumanHeart.mat`
binds a Graphics Tools shader and appears in `0.Full Demo`.

### HexR — `com.microtube.hexr`

Pulled from <https://github.com/MicrotubeTechnologies/com.microtube.hexr> by
`Packages/manifest.json`, not vendored here. MIT, but it bundles third-party
Bluetooth binaries under their own terms — see that package's own `LICENSE`
and `THIRD-PARTY-NOTICES.md`, which are the authority for anything under it.

---

## Assets

### MRTK Extended Assets — `Assets/Gameobjects/org.mixedrealitytoolkit.extendedassets/`

`org.mixedrealitytoolkit.extendedassets`, Mixed Reality Toolkit Contributors.
BSD 3-Clause — see the folder's own `LICENSE.md`. Supplies the `HumanHeart`
model and material used by the medical demo in `0.Full Demo`.

### Unity Technologies Effect Examples — `Assets/UnityTechnologies/`

Unity Technologies. Under the Unity Companion License
(<https://unity3d.com/legal/licenses/Unity_Companion_License>). Particle and
VFX samples used by the fountain and weather demos.

### TextMesh Pro — `Assets/TextMesh Pro/`

Unity Technologies. Unity Companion License. Includes the full example set,
not only the essentials.

### XR Interaction Toolkit and XR Hands samples — `Assets/Samples/`

Unity Technologies, imported from `com.unity.xr.interaction.toolkit` and
`com.unity.xr.hands`. Unity Companion License.

Note the deliberate version split: `XR Interaction Toolkit/3.6.0/Starter
Assets` matches the installed package, but `XR Interaction Toolkit/2.5.4/Hands
Interaction Demo` is kept at the older version on purpose. Every tutorial
scene instantiates its `XR Interaction Hands Setup` prefab with per-child
`fileID` overrides, and reimporting at 3.6.0 would regenerate both the asset
GUID and those fileIDs, detaching the rig from all five scenes. It compiles
against XRI 3.6.0 unmodified apart from namespace fixes for the 3.x
interactor/interactable move.

**Local patch.** The two hand Direct Interactors in `XR Interaction Hands Setup`
were set to take their Select input from an Input Action Reference and had none
assigned, so they could never select anything -- objects were hoverable but could
not be grabbed, while poking still worked because a poke needs no input. They now
reference `XRI Left/Right Interaction/Select` and `Select Value` from the 3.6.0
Starter Assets asset, which is the asset this project's Input Action Managers
already enable, and whose bindings include the hand-tracking pinch.

### Demo art and props — `Assets/Gameobjects/`

**UNRESOLVED.** `CoffeeCup`, `Confetti`, `Drill`, `Extinguisher`,
`Fountain_Set2`, `Hospital`, `Key`, `Kitchen Asset`, `Lightbulb`, `PBR Table`
and `Torch` are third-party models, textures and materials used by the demo
scenes. Their individual origins and redistribution rights have not been
established.

### Fainted-person scenario — `Assets/FaintedPerson/`

`RobotSphere/` is a third-party asset with its own demo scene and scripts —
**UNRESOLVED**. The rest of the folder (`Script/`, the prefabs and audio) is
Microtube's own work and is covered by `LICENSE`.

### Noto Sans SC — `Assets/HexRAssets/Asset/NotoSansSC-HexR-Subset.ttf`

Google, under the SIL Open Font License 1.1 — the licence travels with the font
as `Asset/NotoSansSC-OFL.txt`. Source:
<https://github.com/google/fonts/tree/main/ofl/notosanssc>.

This is a **modified version**: the upstream variable font is instanced at
weight 400 and then subsetted to the ~130 characters the tutorial's bilingual
labels actually use, which takes it from about 10 MB to 130 KB. `Tools/subset-noto-sans-sc.py`
regenerates it and is the place to add a character if new Chinese text renders
as boxes.

The family is renamed to *Noto Sans SC HexR Subset*. Noto Sans SC descends from
Adobe's Source Han Sans and the licence declares the Reserved Font Name
"Source"; renaming a modified version keeps that question from arising, and is
honest that this is not the whole font. The OFL permits modification and
redistribution on these terms, so unlike most of the art here this one is
settled.

It is registered as a project-wide TextMesh Pro fallback in
`Assets/TextMesh Pro/Resources/TMP Settings.asset`, which is what lets the
Latin fonts render the Chinese half of each label.

### HexR demo assets — `Assets/HexRAssets/`

Microtube Technologies' own work, covered by `LICENSE`, with two exceptions:
`Asset/Electronic Highway Sign.TTF` is a third-party font whose licence has
not been established — **UNRESOLVED** — and `Asset/NotoSansSC-HexR-Subset.ttf`
is under the OFL, as above.

---

## Not in this repository

The HexR glove's Bluetooth stack — `HaptGlove.dll`,
`ArduinoBluetoothAPILocal.dll`, `BleWinrtDll.dll`, `classes.jar` and the macOS
bundle — used to be duplicated under `Assets/Plugins/`. It now lives solely in
the `com.microtube.hexr` package, which is where its notices are. Only the
app's own `Assets/Plugins/Android/AndroidManifest.xml` remains here; the
package merges its Bluetooth permissions into it at build time.
