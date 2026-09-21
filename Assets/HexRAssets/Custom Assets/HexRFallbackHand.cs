using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Scripting;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// An Input System device that supplies the hand interaction values XRI needs -- pinch strength,
/// pinch/poke/aim poses -- derived from plain hand joint tracking.
///
/// Why this exists. Every hand binding in "XRI Default Input Actions" resolves to one of
/// &lt;MetaAimHand&gt;, &lt;HandInteraction&gt;, &lt;HandInteractionPoses&gt; or &lt;XRHandDevice&gt;.
/// The first is Meta-only. The other three all trace back to the same place: Unity's OpenXR
/// HandInteractionProfile, which requires the runtime to support XR_EXT_hand_interaction.
/// (XRHandDevice looks independent but is not -- OpenXRHandProvider fills its pinch/poke/aim
/// fields from the legacy "PinchValue"/"PokePosition"/"PointerPosition" feature usages, and
/// HandInteractionProfile is the only thing in the OpenXR package that publishes those.)
///
/// PICO supports XR_EXT_hand_interaction on the PICO 4 Ultra series only. On a PICO 4 or a Neo3
/// the joints track fine -- the hand meshes animate -- but every interaction value stays at zero,
/// so the hands have no select, no pinch pose to put the near interactor on, no poke pose and no
/// aim pose. The rig looks alive and does nothing, which is exactly the symptom that sends you
/// hunting through the interactors.
///
/// This device fills that gap from <see cref="UnityEngine.XR.Hands.XRHandSubsystem"/> joint data,
/// which works on every PICO that supports hand tracking at all. <see cref="HexRFallbackHandDriver"/>
/// creates and drives it, and only when the native path is genuinely absent -- on a 4 Ultra the
/// stock bindings win and this device is never added, so there is nothing to conflict over.
///
/// Poses are in the same space XR Hands reports joints in: relative to the XR Origin's Camera
/// Offset. That is what TrackedPoseDriver and the action-based controllers already expect, so the
/// values drop straight into the existing Position/Rotation actions with no conversion.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 123)]
public struct HexRFallbackHandState : IInputStateTypeInfo
{
    /// <summary>Memory format identifier for <see cref="HexRFallbackHandState"/>.</summary>
    public static FourCC formatId => new FourCC('H', 'X', 'F', 'H');

    /// <inheritdoc />
    public FourCC format => formatId;

    /// <summary>How closed the pinch is, 0 open to 1 fully pinched.</summary>
    [InputControl(usage = "PinchValue", layout = "Axis", offset = 0)]
    [FieldOffset(0)]
    public float pinchValue;

    /// <summary>Whether the thumb and index are touching, with hysteresis applied.</summary>
    [InputControl(usage = "PinchTouched", layout = "Button", offset = 4)]
    [FieldOffset(4)]
    public bool pinchTouched;

    /// <summary>Whether <see cref="pinchValue"/> is meaningful this frame.</summary>
    [InputControl(usage = "PinchReady", layout = "Button", offset = 5)]
    [FieldOffset(5)]
    public bool pinchReady;

    /// <summary>Position of the pinch point, between the thumb and index tips.</summary>
    [InputControl(usage = "PinchPosition", offset = 6)]
    [FieldOffset(6)]
    public Vector3 pinchPosition;

    /// <summary>Rotation of the pinch point.</summary>
    [InputControl(usage = "PinchRotation", offset = 18)]
    [FieldOffset(18)]
    public Quaternion pinchRotation;

    /// <summary>Position of the poke point, at the index fingertip.</summary>
    [InputControl(usage = "PokePosition", offset = 34)]
    [FieldOffset(34)]
    public Vector3 pokePosition;

    /// <summary>Rotation of the poke point.</summary>
    [InputControl(usage = "PokeRotation", offset = 46)]
    [FieldOffset(46)]
    public Quaternion pokeRotation;

    /// <summary>Position the interaction ray starts from.</summary>
    [InputControl(usage = "PointerPosition", offset = 62)]
    [FieldOffset(62)]
    public Vector3 aimPosition;

    /// <summary>Direction the interaction ray points.</summary>
    [InputControl(usage = "PointerRotation", offset = 74)]
    [FieldOffset(74)]
    public Quaternion aimRotation;

    /// <summary><see cref="UnityEngine.XR.InputTrackingState"/> for the device pose.</summary>
    [InputControl(usage = "TrackingState", layout = "Integer", offset = 90)]
    [FieldOffset(90)]
    public int trackingState;

    /// <summary>Whether the hand is currently tracked.</summary>
    [InputControl(usage = "IsTracked", layout = "Button", offset = 94)]
    [FieldOffset(94)]
    public bool isTracked;

    /// <summary>Position of the device, at the wrist.</summary>
    [InputControl(usage = "DevicePosition", offset = 95)]
    [FieldOffset(95)]
    public Vector3 devicePosition;

    /// <summary>Rotation of the device, at the wrist.</summary>
    [InputControl(usage = "DeviceRotation", offset = 107)]
    [FieldOffset(107)]
    public Quaternion deviceRotation;
}

/// <summary>
/// The device itself. Bind to it as &lt;HexRFallbackHand&gt;{LeftHand}/pinchValue and so on --
/// the same control names XRHandDevice uses, so the bindings read the same way in the editor.
/// </summary>
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
[Preserve]
[InputControlLayout(stateType = typeof(HexRFallbackHandState), displayName = "HexR Fallback Hand",
    commonUsages = new[] { "LeftHand", "RightHand" })]
public class HexRFallbackHand : TrackedDevice
{
    /// <summary>The left hand instance, or null when the native hand interaction path is in use.</summary>
    public static HexRFallbackHand leftHand { get; internal set; }

    /// <summary>The right hand instance, or null when the native hand interaction path is in use.</summary>
    public static HexRFallbackHand rightHand { get; internal set; }

    /// <summary>How closed the pinch is, 0 open to 1 fully pinched.</summary>
    public AxisControl pinchValue { get; private set; }

    /// <summary>Whether the thumb and index are touching.</summary>
    public ButtonControl pinchTouched { get; private set; }

    /// <summary>Whether <see cref="pinchValue"/> is meaningful this frame.</summary>
    public ButtonControl pinchReady { get; private set; }

    /// <summary>Position of the pinch point.</summary>
    public Vector3Control pinchPosition { get; private set; }

    /// <summary>Rotation of the pinch point.</summary>
    public QuaternionControl pinchRotation { get; private set; }

    /// <summary>Position of the poke point.</summary>
    public Vector3Control pokePosition { get; private set; }

    /// <summary>Rotation of the poke point.</summary>
    public QuaternionControl pokeRotation { get; private set; }

    /// <summary>Position the interaction ray starts from.</summary>
    public Vector3Control aimPosition { get; private set; }

    /// <summary>Direction the interaction ray points.</summary>
    public QuaternionControl aimRotation { get; private set; }

    /// <inheritdoc />
    protected override void FinishSetup()
    {
        base.FinishSetup();

        pinchValue = GetChildControl<AxisControl>(nameof(pinchValue));
        pinchTouched = GetChildControl<ButtonControl>(nameof(pinchTouched));
        pinchReady = GetChildControl<ButtonControl>(nameof(pinchReady));
        pinchPosition = GetChildControl<Vector3Control>(nameof(pinchPosition));
        pinchRotation = GetChildControl<QuaternionControl>(nameof(pinchRotation));
        pokePosition = GetChildControl<Vector3Control>(nameof(pokePosition));
        pokeRotation = GetChildControl<QuaternionControl>(nameof(pokeRotation));
        aimPosition = GetChildControl<Vector3Control>(nameof(aimPosition));
        aimRotation = GetChildControl<QuaternionControl>(nameof(aimRotation));
    }

    static HexRFallbackHand()
    {
        RegisterLayout();
    }

    // The static constructor covers the editor via [InitializeOnLoad]; a player needs this.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterLayout()
    {
        InputSystem.RegisterLayout<HexRFallbackHand>();
    }
}
