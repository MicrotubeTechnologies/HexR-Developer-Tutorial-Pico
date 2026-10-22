# HexR Unity Integration — OpenXR / PICO ℹ️

The reference project for building HexR haptic glove applications for **PICO**
headsets. It runs on OpenXR, so the same project is the starting point for any
OpenXR runtime; PICO is what it is configured and tested for.

> **Using a Meta Quest?** Go to the
> [HexR Developer Tutorial (Meta OVR)](https://github.com/MicrotubeTechnologies/HexR-Developer-Tutorial-Meta-OVR)
> instead. That project uses Meta's Interaction SDK rather than OpenXR.

## Installation

### Prerequisites

- **Unity 6000.6.0f1.** This is the version the project is saved with. Unity
  projects migrate forward only, so an older editor cannot open it.
- **Git on your `PATH`.** The HexR glove code is not vendored here — it is
  pulled from
  [`com.microtube.hexr`](https://github.com/MicrotubeTechnologies/com.microtube.hexr)
  by `Packages/manifest.json`, and Unity shells out to `git` to fetch it. If
  Package Manager reports it cannot resolve the package, this is almost always
  why.
- **A PICO headset in developer mode**, and the gloves paired — see
  [On-device setup](#on-device-setup) below.

### Steps to get started

1. **Clone this repository's `Pico-Version` branch.** The repository's default branch,
   `main`, is an older, generic OpenXR line, so name the branch:

   ```
   git clone -b Pico-Version https://github.com/MicrotubeTechnologies/HexR-Developer-Tutorial-Pico.git
   ```

   On Windows, clone into a short path such as `C:\Dev\`: Git cannot create a file whose
   full path is longer than 260 characters, and reports *Filename too long*.

2. **Open it in Unity 6000.6.0f1.** First open takes a while: Unity fetches
   `com.microtube.hexr` and imports the project.

3. **Switch to the Android platform** in Build Settings if it is not already.

4. **Open `Assets/Scenes/0.Full Demo.unity`** — the scene with everything in
   it. The numbered tutorial scenes below each isolate one idea.

### What is already configured

You should not need to change any of this, but it is what makes the project a
PICO project rather than a generic OpenXR one:

| Setting | Value |
| --- | --- |
| XR plug-in provider (Android) | OpenXR |
| OpenXR features (Android) | PICO Support, PICO OpenXR Features, the four PICO controller profiles, Hand Tracking Subsystem, Hand Interaction Profile |
| PICO OpenXR plugin | `Packages/Unity OpenXR IntegrationSDK-1.4.0-20250407/` (v1.4.1), vendored because PICO does not publish it to any registry |
| Target architecture | ARM64 (PICO is ARM64 only) |
| Scripting backend | IL2CPP |
| Hand tracking | enabled in `Assets/Resources/PICOProjectSetting.asset` |

To add HexR to a *different* PICO project rather than starting from this one,
install the package by git URL and follow the PICO section of the
[package README](https://github.com/MicrotubeTechnologies/com.microtube.hexr#pico).

### On-device setup

The gloves talk to the headset over Bluetooth LE, which needs permissions that
cannot be granted from Unity:

1. In the headset: **Settings → Apps → [this app] → Permissions**, and allow
   **Nearby devices** and **Location**. BLE scanning returns nothing without
   both, and the failure looks exactly like broken hardware.
2. Power on both gloves before pressing Connect in the app.

> [!NOTE]
> **If the gloves pair but no haptics fire**, the first thing to try is
> flipping **Quest BLE Buffering** on the `HexRManager` component. It selects a
> Bluetooth write-buffering strategy inside the closed-source `HaptGlove.dll`
> and has only ever been tested on Quest — its behaviour on PICO is genuinely
> unknown.
>
> The scenes in this repo currently disagree on it, which is useful for testing
> but worth knowing before you debug: **`0.Full Demo` has it OFF**, and the
> numbered tutorial scenes have it **ON** (they use
> `Assets/HexRAssets/Main Prefab/HexR Main (Open XR).prefab`, which ships with
> it on). If haptics work in one scene and not another, check this before
> assuming anything else. Once PICO behaviour is known, set both the same way.

The second thing to check is that the object you are touching has a
`ProximityCheck`. On OpenXR that is the *only* source of "a hand is near", and
without one `IsHandNear()` is false forever and haptics silently never fire.
See *Determine if hand is near* under **HexR Code Structure** below.

---

<details>
  <summary>🔍 HexR Code Structure</summary>

### Learn more about the HexR code structure and architecture 💡

<details>
  <summary>1. Hand Tracking (PhysicsHandTracking)</summary>

#### The HexR hand supports both the **OpenXR** and **Meta OVR** hand skeleton structure.  
Here’s a summary of the differences in hand structure:
- **OpenXR Hand Skeleton**
- **Meta OVR Hand Skeleton**  
The `PhysicsHandTracking` script mimics the position/rotation of either the OpenXR or Meta OVR hands, the script is attached to the Left/Right hand physics component under HexR Main.

![Hand Skeleton](https://github.com/user-attachments/assets/2585a044-ae44-4814-88e5-abe61c876f8e)

If a custom hand structure is used, you will have to recreate the `PhysicsHandTracking` to track each joint.

</details>

<details>
  <summary>2. HexR Overall Manager (HexRManager)</summary>

#### `HexRManager` is the entry point — one per project, on the HexR rig.

It owns both gloves' `HaptGloveHandler`s, the Bluetooth connect flow, and the
scene wiring. It is a singleton and survives scene loads.

In a project built from this repo it is already in every scene. When setting up
a **new** scene or project, the flow is menu-driven:

1. **HexR → HexR Tools → Project Setup** — pick **PICO** and install anything it
   reports missing. It detects the PICO OpenXR plugin and links to the download
   if it is absent; it cannot install that one for you, because PICO does not
   publish it to a registry.
2. Add a hand-tracking rig — an XR Origin with `com.unity.xr.hands`. The package
   does not create one.
3. **HexR → Create HexR Rig → Open XR (Quest, PICO, SteamVR)**.
4. **HexR → Add Pressure Controller**, then **HexR → Auto Setup Scene**.
5. **HexR → Validate Scene Setup** — this is the one that tells you what is
   still wrong, including a missing `ProximityCheck`.

If setup succeeded there are no missing links in the inspector for HexR Main,
Left Hand Physics and Right Hand Physics.

![Setup Image](https://github.com/user-attachments/assets/f09f713f-fa81-484e-8646-bbe830ecce35)

#### HexRManager settings

- **XR Framework** — leave on **OpenXR**. This names a *hand-joint naming
  convention*, not a headset: OpenXR covers Unity XR Hands on any OpenXR
  runtime, PICO included. There is deliberately no PICO option. `MetaOVR` is
  for Meta's Interaction SDK skeleton and will not find the hands in this
  project.
- **Quest BLE Buffering** (`isQuest`) — ships **on**. Selects a Bluetooth
  write-buffering strategy inside `HaptGlove.dll`. Untested on PICO; the first
  thing to turn off if the gloves connect but stay silent.
- **Right / Left Hand Physics** — the two `HaptGloveHandler` objects. Auto Setup
  finds these by name, so don't rename them.
- **HexR Hand Menu** — the wrist-mounted panel carrying the Connect buttons and
  the Bluetooth/pump indicators. Auto Setup wires its buttons to
  `HexRManager.ConnectLeftBT` / `ConnectRightBT`.

</details>

<details>
  <summary>3. Haptics Controller (PressureTrackerMain)</summary>

#### The `PressureTrackerMain` script contains all of the functions to trigger haptics.
#### There are 6 channels in the HexR glove allowing haptics to be triggered for each finger and the palm

- Overview
  - Functions are categorized by **single-channel** or **multi-channel** triggers.
  - Haptics intensity range from 0.1 (no haptics) to 1 (Max haptics).
  - Refer to the demo scene to see examples of how these functions are used.

- Function : IsHandNear()
  - This is use to check if the user left or right hand is grabbing or near the target object, so that haptics is correctly triggered at the right time and by the right hand.
    
- Function : CustomSingleHaptics ( Haptics.Finger finger, bool states, float intensity, float speed, bool ByPassHandCheck )
  - Haptics.Finger = which finger is to be triggered: index,middle,ring,pinky,thumb,palm
  - states : true = haptics in , false = haptics out
  - intensity : 0.1 - 1 , min haptics - max haptics
  - speed : 0.1 - 1 , slowly increase haptics vs fast increase haptics
  - ByPassHandCheck : true = will trigger haptics without checking IsHandNear()

- Function : CustomSingleVibrations(Haptics.Finger finger, bool states, float intensity, float frequency, bool ByPassHandCheck)
  - Haptics.Finger = which finger is to be triggered: index,middle,ring,pinky,thumb,palm
  - states : true = haptics in , false = haptics out
  - frequency : 0.1 - 2 
  - intensity : 0.1 - 1 , min haptics - max haptics
  - ByPassHandCheck : true = will trigger haptics without checking IsHandNear()
</details>

<details>
  <summary>4. HexR Grab and Pinch (HexRGrabbable)</summary>

#### The `HexRGrabbable` script enables objects to be picked up by the HexR hands.
#### This is optional as you can also use the grab/pinch provided by **OpenXR**, however the haptics trigger and physics of grab will be different. Give both a try to see which is more suitable for you.
To set up `HexRGrabbable`:
1. Ensure the object has a **Collider (Trigger)** and **Rigidbody** attached to the same GameObject.
2. Since the interaction is physics-based, adjust the size of the collider to improve grab/pinch behavior.
3. Optionally, attach an additional collider if you want the object to interact with other GameObjects.

![Grabbable Example](https://github.com/user-attachments/assets/3fadad3e-80d7-4f57-9186-a63d4ebc125f)

#### HexRGrabbable Settings:
- **Type of Grab:**  
  - **Palm Grab:** Requires the palm and at least one finger to touch the object (thumb not required).
  - **Pinch Grab:** Requires the thumb and at least one finger to touch the object (palm not required).

- **Gravity Bool:**  
  If enabled, gravity will affect the object when released.

- **Haptic Slider:**  
  Controls the strength of the haptic feedback during grab or pinch.  
  - `0`: No haptics  
  - `60`: Maximum haptics strength

- **On Grab Event:**  
  Trigger an event when the object is grabbed or pinched.

- **On Release Event:**  
  Trigger an event when the object is released.

</details>

<details>
  <summary>5. Creating Haptic Zones (SpecialHaptics)</summary>

#### The `SpecialHaptics` script enables objects to trigger a custom haptic effect when touch.

![image](https://github.com/user-attachments/assets/15bc96c7-db42-452c-adeb-68b657984802)

To set up `SpecialHaptics`:
1. Ensure the object has a **Collider (Trigger)** attached to the same GameObject.
2. Since the interaction is physics-based, adjust the size of the collider for the haptic zone.
3. Select the type of Haptics in the inspector.

#### SpecialHaptics Settings:
- **Custom Vibrations:**  
  - When activated will create the vibration effects.
  - *Frequency Speed:* the frequency of the vibrations.
  - *Haptic Strength:* the strength of the vibrations.
- **Custom Haptics:**
  - When activated/touch will trigger a constant haptic.
  - *Haptic Pressure:* slider to adjust strength of haptic. 10 = weakest, 60 = strongest.
- **Fountain Effect:**  
  - When activated will simulate running water.
 
- **Raindrop Effect:**  
  - When activated will simulate raindrops with random haptics trigger.
    
- **Heart Beat Effect:**  
  - When activated will simulate beating heart, but only affects fingers and not palm.
    
- **Hand Squeeze Effect:**  
  - When activated will allows the player to trigger an event by squeezing the hand
  - `0.1`: Fully closed hand  
  - `1`: Fully open hand
</details> 

<details>
  <summary>6. Determine if hand is near (ProximityCheck)</summary>

#### The `ProximityCheck` script checks if the left or right hand is near the target object.
#### Haptics is only trigger when the hand is near the object.
#### Place the `ProximityCheck` prefab as a child of the target object and click the auto set up.
#### You should adjust the size of your trigger collider to ensure that it is optimise.


</details> 
</details>

&nbsp;


<details>
<summary> Demo Scene : 1. Basic Tutorial </summary>
 
## Demo scene 1 — Basic Tutorial

#### The **Basic Tutorial** demo scene contains the implementation to grab and pinch object using HexR grabbing and pinching.

![image](https://github.com/user-attachments/assets/a5ecd879-2c42-4e4b-a056-69a30dbceaec)

- Apple Object 🍎
  - The HexRGrabbable script is attach to the apple to allow it to be pick up. Palm grab have been selected and a haptics of 50 is triggered upon grab.
  - Gravity bool have been turned on, hence when you release the apple, it will be affected by gravity.

- Key Object 🔑
  - The HexRGrabbable script is attach to the apple to allow it to be pick up. Pinch grab have been selected and a haptics of 30 is triggered upon grab.
  - Gravity bool have been turned off.

- Torch Object 🔥
  - The HexRGrabbable script is attach to the torch to allow it to be pick up. Palm grab have been selected and a haptics of 40 is triggered upon grab.
  - Gravity bool have been turned off.
  - The SpecialHaptics is attach to the haptic zone(child gameobject) to allow vibrations to be triggered when touching the fire.

- Button Object 🎮
  - Button objects uses XR interaction and haptics is triggered from the events when the buttons is push.
  - Take a look at Open XR documentation to understand how to implement their hands interactions.
</details>

<details>
<summary> Demo Scene : 2. Special Haptics </summary>
 
## Demo scene 2 — Special Haptics ⛲

#### The **Special Haptics Tutorial** demo scene contains the haptics implementations for using triggers and colliders to trigger haptics. 
#### There is a haptic zone in the fountain, Heart and rain clouds.
#### To create a haptic zone simply attach the `SpecialHaptics` Script and a collider(trigger) to a gameobject.

![image](https://github.com/user-attachments/assets/49262fdc-6391-4753-815a-d2d5c7988306)



</details>

<details>
<summary> Demo Scene : 3. Button </summary>
 
## Demo scene 3 — Button

#### The **Button Tutorial** demo scene contains the haptics implementations by using event trigger. 
#### The haptics function are triggered by the interactable Events in XR simple Interactable in each buttons.
#### A Proximity Check is place in the buttons to determine if the left or right hand have triggered the event.

![image](https://github.com/user-attachments/assets/472501fa-952c-40bc-8c0b-89ed692bd22b)



</details>

## Demo scene 0 — Full Demo

The scene the build opens with, and the only one that shows everything working
together: the grab and pinch demos, the medical/CPR scenario with the squeezable
heart, the fountain and rain haptic zones, the push button, and the hand menu.

Start here to see what the gloves can do, then use the numbered scenes below to
see how one piece is built.

> [!NOTE]
> This scene's HexR rig is unpacked rather than a prefab instance, so edits to
> `HexR Main (Open XR)` in the package do not propagate into it. Prefer the
> numbered tutorial scenes when you want a rig that tracks the package.

## Demo scene — Use Interaction Tutorial

Demonstrates `HexRUsable`: triggering haptics when an object is *used* (a
trigger pulled, a tool activated) rather than merely grabbed. The torch is the
worked example.

---

## Licence

MIT for Microtube Technologies' own work. The demo art, the PICO SDK, MRTK and
the Unity sample content are third-party and are **not** all redistributable —
see [`LICENSE`](LICENSE) and [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)
before reusing anything from this repository.
