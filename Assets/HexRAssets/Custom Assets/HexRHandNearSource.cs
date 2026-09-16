using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using HexR;

/// <summary>
/// Feeds the XR Interaction Toolkit's grab and poke state into a <see cref="PressureTrackerMain"/>'s
/// hand-near gating -- the OpenXR counterpart of the package's MetaOVRHandNearSource.
///
/// Why this exists. Haptics only fire when PressureTrackerMain.IsHandNear() is true, and that ORs
/// three flags: HandGrabbing, PokeHovering and CollisionNearHand. On Meta OVR the first two are
/// filled in by MetaOVRHandNearSource reading the Interaction SDK's interactors. On OpenXR nothing
/// filled them in at all, so the only way to make a hand "near" was to put a ProximityCheck trigger
/// volume on every object -- and forgetting one produced silence that looks exactly like a dead
/// glove. This closes that gap: XRI already knows when a hand is grabbing or hovering something,
/// so ask it.
///
/// The result is that a PICO project is built the ordinary way -- XRGrabInteractable, XRSimpleInteractable,
/// the hand interactors that ship with XRI -- and HexR reads that existing interaction state rather
/// than asking you to model proximity a second time. ProximityCheck is still the right tool for a
/// haptic *zone* you reach into, which is not an interactable and has no interactor state to read.
///
/// It reuses PressureTrackerMain's handGrabInteractor/pokeInteractor fields, which are typed
/// MonoBehaviour precisely so either backend can put its own interactor type in them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PressureTrackerMain))]
public class HexRHandNearSource : MonoBehaviour
{
    [Tooltip("Where to look for this hand's interactors. Leave empty to search up from the hand " +
             "this Pressure Controller belongs to.")]
    public Transform searchRoot;

    [Tooltip("Treat hovering a grabbable as 'hand near', not just actually holding it. Usually " +
             "what you want: the glove should respond as the hand closes on an object.")]
    public bool grabHoverCountsAsNear = true;

    private PressureTrackerMain tracker;

    // Cast once and cached, re-resolved only when the underlying reference changes -- casting
    // both interactors every frame on both hands is pure waste.
    private MonoBehaviour cachedGrabSource;
    private MonoBehaviour cachedPokeSource;
    private XRBaseInteractor grabInteractor;
    private XRBaseInteractor pokeInteractor;

    private void Awake()
    {
        tracker = GetComponent<PressureTrackerMain>();
    }

    private void Start()
    {
        if (tracker == null || (tracker.handGrabInteractor != null && tracker.pokeInteractor != null))
        {
            return;
        }

        Transform root = ResolveSearchRoot();
        if (root == null)
        {
            Debug.LogWarning("[HexR] " + name + ": no hand root to search for XRI interactors, so grab and " +
                             "poke gating stays off for this hand. Assign searchRoot, or set " +
                             "handGrabInteractor/pokeInteractor on the Pressure Controller by hand.");
            return;
        }

        AutoFind(root);
    }

    /// <summary>
    /// The interactors are siblings of the tracked hand visual rather than children of it, so
    /// walking up from the hand root is what actually finds them.
    /// </summary>
    private Transform ResolveSearchRoot()
    {
        if (searchRoot != null)
        {
            return searchRoot;
        }

        PhysicsHandTracking tracking = GetComponentInParent<PhysicsHandTracking>();
        Transform t = tracking != null ? tracking.handRoot : null;
        if (t == null)
        {
            return null;
        }

        // Climb until something in this subtree owns an interactor.
        while (t != null)
        {
            if (t.GetComponentInChildren<XRBaseInteractor>(true) != null)
            {
                return t;
            }
            t = t.parent;
        }
        return null;
    }

    /// <summary>Assigns whichever interactors aren't already set. Inactive children are searched
    /// too, because the rig keeps interactors disabled until the hand is tracked.</summary>
    public bool AutoFind(Transform handRoot)
    {
        if (tracker == null)
        {
            tracker = GetComponent<PressureTrackerMain>();
        }
        if (tracker == null || handRoot == null)
        {
            return false;
        }

        if (tracker.handGrabInteractor == null)
        {
            tracker.handGrabInteractor = handRoot.GetComponentInChildren<XRDirectInteractor>(true);
        }
        if (tracker.pokeInteractor == null)
        {
            tracker.pokeInteractor = handRoot.GetComponentInChildren<XRPokeInteractor>(true);
        }

        if (tracker.handGrabInteractor == null || tracker.pokeInteractor == null)
        {
            Debug.LogWarning("[HexR] " + name + ": couldn't find an XRDirectInteractor and XRPokeInteractor " +
                             "under \"" + handRoot.name + "\". Grab or poke gating will stay off for this hand.");
            return false;
        }

        return true;
    }

    private void Update()
    {
        if (tracker == null)
        {
            return;
        }

        Resolve();

        bool grabbing = grabInteractor != null &&
                        (grabInteractor.hasSelection || (grabHoverCountsAsNear && grabInteractor.hasHover));
        bool poking = pokeInteractor != null && pokeInteractor.hasHover;

        tracker.HandGrabbingCheck(grabbing);
        tracker.PokeHoveringCheck(poking);
    }

    private void Resolve()
    {
        if (!ReferenceEquals(cachedGrabSource, tracker.handGrabInteractor))
        {
            cachedGrabSource = tracker.handGrabInteractor;
            grabInteractor = cachedGrabSource as XRBaseInteractor;
            WarnOnMismatch(cachedGrabSource, grabInteractor, "handGrabInteractor");
        }

        if (!ReferenceEquals(cachedPokeSource, tracker.pokeInteractor))
        {
            cachedPokeSource = tracker.pokeInteractor;
            pokeInteractor = cachedPokeSource as XRBaseInteractor;
            WarnOnMismatch(cachedPokeSource, pokeInteractor, "pokeInteractor");
        }
    }

    // The fields accept any MonoBehaviour, so a wrong drag in the Inspector would otherwise fail
    // completely silently: the cast yields null and gating just stays off.
    private void WarnOnMismatch(MonoBehaviour assigned, Object resolved, string fieldName)
    {
        if (assigned != null && resolved == null)
        {
            Debug.LogWarning("[HexR] " + name + ": " + fieldName + " holds a " + assigned.GetType().Name +
                             ", which is not an XRBaseInteractor. Gating stays off for this hand.");
        }
    }
}
