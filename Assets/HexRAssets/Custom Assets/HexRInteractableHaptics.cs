using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using HexR;

/// <summary>
/// Adds HexR haptics to a stock XR Interaction Toolkit interactable.
///
/// This is the whole point of the integration: you build the PICO app the ordinary way, with
/// XRGrabInteractable and XRSimpleInteractable and the hand interactors XRI ships, and then drop
/// one of these alongside to make it felt. Nothing about the interactable changes, and none of
/// HexR's own grab detection is involved -- XRI decides what is grabbed or pressed, HexR only
/// decides what that should feel like.
///
/// Contrast with HexRGrabbable, which this replaces on the tutorial props. That component ran its
/// own physics-based grab off the glove's finger colliders and wrote straight to the glove,
/// bypassing PressureTrackerMain entirely. It works, but it means the object is grabbable *only*
/// by a HexR hand -- so the app can't be built or tested without gloves, and it behaves differently
/// from every other interactable in the scene.
///
/// Haptics still only fire while PressureTrackerMain.IsHandNear() is true. HexRHandNearSource on
/// each Pressure Controller is what makes that true from XRI's own grab and poke state.
/// </summary>
[DisallowMultipleComponent]
public class HexRInteractableHaptics : MonoBehaviour
{
    public enum FireOn
    {
        Select,             // grabbed, or pressed
        Hover,              // hand near enough for XRI to highlight it
        SelectAndHover
    }

    [Header("When")]
    public FireOn fireOn = FireOn.Select;

    [Header("Which fingers")]
    public bool thumb = true;
    public bool index = true;
    public bool middle = true;
    public bool ring = false;
    public bool pinky = false;
    public bool palm = true;

    [Header("How hard")]
    [Tooltip("Pressure applied to each selected finger. The glove's usable range is 10 (barely " +
             "there) to 60 (firm). HexRGrabbable used 30 for small props and 40 for the torch.")]
    [Range(0f, 60f)]
    public float strength = 30f;

    [Header("Hands")]
    [Tooltip("Left and right Pressure Controllers. Found by name at Start when left empty.")]
    public PressureTrackerMain leftPressureTracker;
    public PressureTrackerMain rightPressureTracker;

    private XRBaseInteractable interactable;
    private PressureTrackerMain activeTracker;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
        {
            Debug.LogWarning("[HexR] " + name + ": no XR interactable on this object, so there is nothing " +
                             "to attach haptics to. Add an XRGrabInteractable or XRSimpleInteractable.");
        }
    }

    private void Start()
    {
        // Find the two Pressure Controllers by which hand they sit on rather than by an exact
        // object name. The package's own auto-find looks for "Left/Right Pressure Controller",
        // but the rig in this project names them "Left/Right Hand Physics" -- matching on the
        // side works for either, and for a rig someone has renamed.
        if (leftPressureTracker == null || rightPressureTracker == null)
        {
            PressureTrackerMain[] trackers = FindObjectsByType<PressureTrackerMain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PressureTrackerMain t in trackers)
            {
                string n = t.gameObject.name;
                if (leftPressureTracker == null &&
                    n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    leftPressureTracker = t;
                }
                else if (rightPressureTracker == null &&
                         n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rightPressureTracker = t;
                }
            }
        }
        if (leftPressureTracker == null && rightPressureTracker == null)
        {
            Debug.LogWarning("[HexR] " + name + ": found no Pressure Controllers, so this object will be " +
                             "grabbable but will not be felt. Run HexR > Auto Setup Scene.");
        }
    }

    private void OnEnable()
    {
        if (interactable == null) return;
        if (fireOn != FireOn.Hover)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }
        if (fireOn != FireOn.Select)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
    }

    private void OnDisable()
    {
        if (interactable == null) return;
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        Release();
    }

    private void OnSelectEntered(SelectEnterEventArgs args) { Apply(TrackerFor(args.interactorObject as MonoBehaviour)); }
    private void OnSelectExited(SelectExitEventArgs args) { Release(); }
    private void OnHoverEntered(HoverEnterEventArgs args) { Apply(TrackerFor(args.interactorObject as MonoBehaviour)); }
    private void OnHoverExited(HoverExitEventArgs args) { Release(); }

    /// <summary>
    /// Which hand did this. XRI tells us the interactor, and on the rig the interactors sit under
    /// objects named "Left Hand"/"Right Hand", so the ancestry answers it without either side
    /// needing to know about the other.
    /// </summary>
    private PressureTrackerMain TrackerFor(MonoBehaviour interactor)
    {
        if (interactor == null) return rightPressureTracker != null ? rightPressureTracker : leftPressureTracker;

        Transform t = interactor.transform;
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0) return leftPressureTracker;
            if (n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0) return rightPressureTracker;
            t = t.parent;
        }
        return rightPressureTracker != null ? rightPressureTracker : leftPressureTracker;
    }

    private void Apply(PressureTrackerMain tracker)
    {
        if (tracker == null) return;
        Release();              // never leave the other hand holding pressure
        activeTracker = tracker;

        if (thumb) tracker.SingleThumbHaptic(strength);
        if (index) tracker.SingleIndexHaptic(strength);
        if (middle) tracker.SingleMiddleHaptic(strength);
        if (ring) tracker.SingleRingHaptic(strength);
        if (pinky) tracker.SinglePinkyHaptic(strength);
        if (palm) tracker.SinglePalmHaptic(strength);
    }

    private void Release()
    {
        if (activeTracker == null) return;

        if (thumb) activeTracker.RemoveThumbHaptics();
        if (index) activeTracker.RemoveIndexHaptics();
        if (middle) activeTracker.RemoveMiddleHaptics();
        if (ring) activeTracker.RemoveRingHaptics();
        if (pinky) activeTracker.RemovePinkyHaptics();
        if (palm) activeTracker.RemovePalmHaptics();

        activeTracker = null;
    }
}
