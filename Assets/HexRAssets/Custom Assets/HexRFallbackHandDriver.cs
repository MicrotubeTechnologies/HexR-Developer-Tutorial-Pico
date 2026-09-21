using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR;

/// <summary>
/// Creates and drives <see cref="HexRFallbackHand"/> from hand joint data, but only on devices
/// that do not report hand interaction natively.
///
/// Put one of these anywhere in the scene -- it is a singleton and finds everything it needs
/// itself. It has no inspector wiring and no dependency on the rig's shape, so it survives the
/// rig being rebuilt or swapped for a newer XRI sample.
///
/// The decision. On a PICO 4 Ultra the OpenXR runtime supports XR_EXT_hand_interaction, so
/// XRHandDevice reports pinchReady and every stock XRI binding already works; adding a second
/// device that binds the same actions would just create a conflict for the Input System to
/// resolve. So the driver asks the runtime directly whether that extension is enabled, which is
/// the whole question and gives an answer on the first frame. If OpenXR is not the active
/// provider -- in the editor, say -- it falls back to watching whether the native path ever
/// produces a ready pinch, latched so a momentary tracking dropout cannot make it flap.
///
/// Either way it logs the verdict once, so a build on an unfamiliar headset says in logcat
/// which path it took rather than leaving you to infer it from whether grabbing works.
///
/// The pinch model. Thumb tip to index tip distance, divided by the wrist-to-middle-knuckle
/// length so it scales with the user's hand rather than assuming an adult span. That ratio is
/// mapped through open/closed thresholds into 0..1, and the boolean gets separate press and
/// release thresholds because a raw threshold on a noisy distance chatters badly right at the
/// point where you are trying to grab something. The release threshold sits well below the press
/// one on purpose: the fingers holding an object are as far apart as the object is thick, so a
/// release threshold set near the press one reads an ordinary hold as letting go.
///
/// Tracking dropouts. A hand closed around something hides its own fingers from the cameras, so
/// the joints drop out exactly when a grab is in progress. Reporting that immediately releases the
/// pinch and the object falls out of a hand that never opened, so a dropout holds the last good
/// state for <see cref="trackingLossGraceSeconds"/> before the loss is reported.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class HexRFallbackHandDriver : MonoBehaviour
{
    /// <summary>How the driver decides whether to supply fallback hand interaction data.</summary>
    public enum FallbackMode
    {
        /// <summary>Use the fallback only when the runtime reports no native pinch. The normal choice.</summary>
        Auto,

        /// <summary>Always supply the fallback, even if the runtime has its own. For testing.</summary>
        AlwaysFallback,

        /// <summary>Never supply the fallback. For confirming what the bare runtime does.</summary>
        NeverFallback,
    }

    [Tooltip("Whether to supply fallback hand interaction data. Auto is correct for shipping: it " +
             "steps aside on a PICO 4 Ultra and fills in on a PICO 4 or Neo3.")]
    public FallbackMode mode = FallbackMode.Auto;

    [Tooltip("Seconds of tracked hand data to observe before deciding whether the runtime supplies " +
             "pinch natively. Too short and a slow-starting runtime looks like it has none.")]
    public float decisionDelaySeconds = 1f;

    [Tooltip("Thumb-to-index distance, as a fraction of hand size, at which the pinch reads as fully " +
             "closed. Below this the value stays at 1.")]
    public float closedRatio = 0.25f;

    [Tooltip("Thumb-to-index distance, as a fraction of hand size, at which the pinch reads as fully " +
             "open. Above this the value stays at 0.")]
    public float openRatio = 0.7f;

    [Tooltip("Pinch value at or above which the hand counts as pinching.")]
    [Range(0f, 1f)]
    public float pressThreshold = 0.85f;

    [Tooltip("Pinch value at or below which an established pinch is released. Kept well under the " +
             "press threshold so a held grab does not flicker, and low enough that the thickness of " +
             "whatever is being held does not read as letting go.")]
    [Range(0f, 1f)]
    public float releaseThreshold = 0.3f;

    [Tooltip("How long a pinch survives a hand-tracking dropout before it is released. A hand " +
             "closed around an object occludes its own fingers, so short dropouts are normal and " +
             "releasing on the first bad frame drops the object for no visible reason.")]
    public float trackingLossGraceSeconds = 0.25f;

    private enum Decision
    {
        Undecided,
        UseNative,
        UseFallback,
    }

    /// <summary>
    /// The OpenXR extension that every stock XRI hand binding ultimately depends on. PICO
    /// supports it on the 4 Ultra series only, so on a PICO 4 (including Enterprise) or a Neo3
    /// it is absent and this driver is the only thing supplying hand interaction values.
    /// </summary>
    private const string HandInteractionExtension = "XR_EXT_hand_interaction";

    private static readonly List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

    private XRHandSubsystem subsystem;

    private Decision decision = Decision.Undecided;
    private float trackedSince = -1f;
    private bool sawNativePinch;
    private bool loggedDecision;

    /// <summary>
    /// What has to survive between frames for one hand: the pinch latch, and the last state good
    /// enough to re-send while tracking is briefly lost.
    /// </summary>
    private struct HandLatch
    {
        public bool pinching;
        public HexRFallbackHandState lastGood;
        public bool hasLastGood;
        public bool lost;
        public float lostSince;
    }

    // Hysteresis has to persist between frames, and the two hands latch independently.
    private HandLatch leftLatch;
    private HandLatch rightLatch;

    private void OnEnable()
    {
        TryBindSubsystem();
    }

    private void OnDisable()
    {
        if (subsystem != null)
        {
            subsystem.updatedHands -= OnUpdatedHands;
            subsystem = null;
        }

        RemoveDevices();
        decision = Decision.Undecided;
        trackedSince = -1f;
        sawNativePinch = false;
        loggedDecision = false;
        leftLatch = default;
        rightLatch = default;
    }

    private void Update()
    {
        // Deliberately not a one-shot bind in OnEnable. The hand subsystem is often not running
        // yet when a scene loads on device, and a component that gives up at that point stays
        // dead for the whole session while looking perfectly healthy in the inspector.
        if (subsystem == null || !subsystem.running)
        {
            TryBindSubsystem();
        }
    }

    private void TryBindSubsystem()
    {
        if (subsystem != null)
        {
            subsystem.updatedHands -= OnUpdatedHands;
            subsystem = null;
        }

        SubsystemManager.GetSubsystems(s_Subsystems);
        for (int i = 0; i < s_Subsystems.Count; i++)
        {
            if (s_Subsystems[i] != null && s_Subsystems[i].running)
            {
                subsystem = s_Subsystems[i];
                subsystem.updatedHands += OnUpdatedHands;
                return;
            }
        }
    }

    private void OnUpdatedHands(XRHandSubsystem handSubsystem, XRHandSubsystem.UpdateSuccessFlags successFlags,
        XRHandSubsystem.UpdateType updateType)
    {
        // Dynamic runs once per frame before script Update; BeforeRender runs again for late
        // latching. Queueing from both would push two state events per frame for no benefit.
        if (updateType != XRHandSubsystem.UpdateType.Dynamic)
        {
            return;
        }

        bool leftTracked = handSubsystem.leftHand.isTracked;
        bool rightTracked = handSubsystem.rightHand.isTracked;

        UpdateDecision(leftTracked || rightTracked);

        if (decision != Decision.UseFallback)
        {
            return;
        }

        EnsureDevices();

        if (HexRFallbackHand.leftHand != null)
        {
            PushHand(handSubsystem.leftHand, HexRFallbackHand.leftHand, ref leftLatch);
        }

        if (HexRFallbackHand.rightHand != null)
        {
            PushHand(handSubsystem.rightHand, HexRFallbackHand.rightHand, ref rightLatch);
        }
    }

    private void UpdateDecision(bool anyHandTracked)
    {
        if (mode == FallbackMode.AlwaysFallback)
        {
            decision = Decision.UseFallback;
            LogDecisionOnce("forced by the mode field");
            return;
        }

        if (mode == FallbackMode.NeverFallback)
        {
            decision = Decision.UseNative;
            RemoveDevices();
            LogDecisionOnce("forced by the mode field");
            return;
        }

        if (decision == Decision.Undecided && TryAskRuntime(out bool extensionEnabled))
        {
            // The runtime knows the answer outright, so there is no reason to sit through the
            // observation window.
            decision = extensionEnabled ? Decision.UseNative : Decision.UseFallback;
            if (decision == Decision.UseNative)
            {
                RemoveDevices();
            }

            LogDecisionOnce(HandInteractionExtension + (extensionEnabled ? " is enabled" : " is not available"));
            return;
        }

        if (!anyHandTracked)
        {
            // Hands went away. Drop back to undecided so plugging in a different runtime, or
            // simply starting tracking properly this time, gets a fresh look.
            if (decision == Decision.Undecided)
            {
                trackedSince = -1f;
            }

            return;
        }

        if (decision != Decision.Undecided)
        {
            return;
        }

        if (trackedSince < 0f)
        {
            trackedSince = Time.unscaledTime;
            sawNativePinch = false;
        }

        // XRHandDevice.pinchReady is the honest signal: OpenXRHandProvider sets it only when the
        // runtime actually answered TryGetPinchValue, which is exactly the XR_EXT_hand_interaction
        // question we care about.
        if (NativePinchReady(XRHandDevice.leftHand) || NativePinchReady(XRHandDevice.rightHand))
        {
            sawNativePinch = true;
        }

        if (Time.unscaledTime - trackedSince < decisionDelaySeconds)
        {
            return;
        }

        decision = sawNativePinch ? Decision.UseNative : Decision.UseFallback;

        if (decision == Decision.UseNative)
        {
            RemoveDevices();
        }

        LogDecisionOnce(sawNativePinch
            ? "the runtime produced a ready pinch on its own"
            : "no ready pinch appeared while hands were tracked");
    }

    /// <summary>
    /// Asks the OpenXR runtime whether it supports hand interaction. Returns false when OpenXR
    /// is not the active provider, in which case there is nothing authoritative to ask.
    /// </summary>
    private static bool TryAskRuntime(out bool extensionEnabled)
    {
        extensionEnabled = false;

        if (string.IsNullOrEmpty(OpenXRRuntime.name))
        {
            return false;
        }

        extensionEnabled = OpenXRRuntime.IsExtensionEnabled(HandInteractionExtension);
        return true;
    }

    private void LogDecisionOnce(string reason)
    {
        if (loggedDecision || decision == Decision.Undecided)
        {
            return;
        }

        loggedDecision = true;

        string runtime = string.IsNullOrEmpty(OpenXRRuntime.name)
            ? "no OpenXR runtime"
            : OpenXRRuntime.name + " " + OpenXRRuntime.version;

        Debug.Log(decision == Decision.UseFallback
            ? "[HexRFallbackHand] Supplying hand pinch and poses from joint tracking -- " + reason
              + " (" + runtime + "). Grab, poke and the hand ray run off this driver."
            : "[HexRFallbackHand] Standing aside; the runtime supplies hand interaction natively -- "
              + reason + " (" + runtime + ").");
    }

    private static bool NativePinchReady(XRHandDevice device)
    {
        return device != null && device.added && device.pinchReady.isPressed;
    }

    private static void EnsureDevices()
    {
        if (HexRFallbackHand.leftHand == null)
        {
            HexRFallbackHand.leftHand = InputSystem.AddDevice<HexRFallbackHand>();
            InputSystem.SetDeviceUsage(HexRFallbackHand.leftHand, CommonUsages.LeftHand);
        }

        if (HexRFallbackHand.rightHand == null)
        {
            HexRFallbackHand.rightHand = InputSystem.AddDevice<HexRFallbackHand>();
            InputSystem.SetDeviceUsage(HexRFallbackHand.rightHand, CommonUsages.RightHand);
        }
    }

    private static void RemoveDevices()
    {
        if (HexRFallbackHand.leftHand != null)
        {
            InputSystem.RemoveDevice(HexRFallbackHand.leftHand);
            HexRFallbackHand.leftHand = null;
        }

        if (HexRFallbackHand.rightHand != null)
        {
            InputSystem.RemoveDevice(HexRFallbackHand.rightHand);
            HexRFallbackHand.rightHand = null;
        }
    }

    private void PushHand(XRHand hand, HexRFallbackHand device, ref HandLatch latch)
    {
        HexRFallbackHandState state = default;

        if (!hand.isTracked
            || !TryGetPose(hand, XRHandJointID.Wrist, out Pose wrist)
            || !TryGetPose(hand, XRHandJointID.ThumbTip, out Pose thumbTip)
            || !TryGetPose(hand, XRHandJointID.IndexTip, out Pose indexTip)
            || !TryGetPose(hand, XRHandJointID.MiddleProximal, out Pose middleProximal))
        {
            HoldThroughDropout(device, ref latch);
            return;
        }

        latch.lost = false;

        int fullyTracked = (int)(UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation);

        state.isTracked = true;
        state.trackingState = fullyTracked;
        state.devicePosition = wrist.position;
        state.deviceRotation = wrist.rotation;

        // Hand size, so the thresholds mean the same thing on a small hand and a large one.
        float handScale = Vector3.Distance(wrist.position, middleProximal.position);
        float pinchDistance = Vector3.Distance(thumbTip.position, indexTip.position);
        float ratio = handScale > 0.0001f ? pinchDistance / handScale : 1f;

        // Inverted on purpose: closer fingers means a higher value.
        float value = Mathf.InverseLerp(openRatio, closedRatio, ratio);

        latch.pinching = latch.pinching ? value > releaseThreshold : value >= pressThreshold;

        state.pinchValue = value;
        state.pinchTouched = latch.pinching;
        state.pinchReady = true;

        Vector3 pinchPoint = Vector3.Lerp(thumbTip.position, indexTip.position, 0.5f);
        state.pinchPosition = pinchPoint;
        state.pinchRotation = wrist.rotation;

        state.pokePosition = indexTip.position;
        state.pokeRotation = TryGetPose(hand, XRHandJointID.IndexDistal, out Pose indexDistal)
            ? indexDistal.rotation
            : indexTip.rotation;

        // A ray cast out along the wrist-through-pinch axis. Meta anchors its aim ray at the head
        // instead, which needs the head pose converted into this same tracking space; staying
        // inside the hand's own joints keeps the maths honest and the ray stable when the user
        // turns their head while holding something.
        Vector3 aimForward = pinchPoint - wrist.position;
        state.aimPosition = pinchPoint;
        state.aimRotation = aimForward.sqrMagnitude > 0.000001f
            ? Quaternion.LookRotation(aimForward.normalized, wrist.up)
            : wrist.rotation;

        latch.lastGood = state;
        latch.hasLastGood = true;
        InputSystem.QueueStateEvent(device, state);
    }

    /// <summary>
    /// Keeps a hand alive through a short tracking dropout.
    ///
    /// Hand tracking loses the fingers whenever they are hidden from the cameras, and a hand
    /// closed around an object hides them by definition -- so the frames where a grab matters most
    /// are exactly the frames most likely to drop. Reporting the loss immediately makes the pinch
    /// go false, which makes XRI deselect, which drops the object; the user sees it fall out of a
    /// hand that never opened. So the last good state is re-sent for a grace period, and the loss
    /// is only reported if tracking really is gone.
    /// </summary>
    private void HoldThroughDropout(HexRFallbackHand device, ref HandLatch latch)
    {
        if (!latch.lost)
        {
            latch.lost = true;
            latch.lostSince = Time.unscaledTime;
        }

        if (latch.hasLastGood && Time.unscaledTime - latch.lostSince < trackingLossGraceSeconds)
        {
            InputSystem.QueueStateEvent(device, latch.lastGood);
            return;
        }

        latch.pinching = false;
        latch.hasLastGood = false;

        HexRFallbackHandState lostState = default;
        lostState.trackingState = (int)UnityEngine.XR.InputTrackingState.None;
        InputSystem.QueueStateEvent(device, lostState);
    }

    private static bool TryGetPose(XRHand hand, XRHandJointID id, out Pose pose)
    {
        return hand.GetJoint(id).TryGetPose(out pose);
    }
}
