using UnityEngine;
using Unity.XR.OpenXR.Features.PICOSupport;

/// <summary>
/// Runs the scene in passthrough, so the demo props sit in your real room rather than in a
/// black void. Put one of these anywhere in the scene.
///
/// Two things have to happen together, which is why they live in one component:
///
///   1. PICO's OpenXR Passthrough feature has to be told to show the camera feed. The feature
///      itself is ticked under Project Settings > XR Plug-in Management > OpenXR > Android;
///      that makes it available, it does not start it.
///   2. The XR camera has to stop drawing a skybox over the top of it. Under the Built-in
///      render pipeline that means Clear Flags = Solid Color with a fully transparent
///      background -- anything opaque, skybox included, hides the passthrough layer completely.
///
/// Doing (2) from here rather than editing the camera in the XR rig prefab is deliberate. That
/// rig comes from the XR Interaction Toolkit sample vendored under Assets/Samples/, which is
/// pinned at 2.5.4 because reimporting it regenerates its GUID and detaches the rig from every
/// scene in this project. Leaving it untouched keeps that hazard where it is, and it makes the
/// VR/MR switch genuinely reversible: the original camera settings are restored on the way back.
/// </summary>
public class PassthroughToggle : MonoBehaviour
{
    [Tooltip("Start the scene in passthrough. Untick to start in VR and switch over at runtime.")]
    public bool startInPassthrough = true;

    private Camera xrCamera;
    private CameraClearFlags vrClearFlags;
    private Color vrBackgroundColor;
    private bool isPassthrough;

    /// <summary>True once the PICO passthrough extension is live, which only happens on a
    /// headset. In the Editor, and on any runtime without it, this stays false and the
    /// component leaves the camera alone rather than blacking out the Game view.</summary>
    public static bool IsAvailable
    {
        get { return PassthroughFeature.isExtensionEnable; }
    }

    private void Awake()
    {
        xrCamera = Camera.main;
        if (xrCamera == null)
        {
            Debug.LogWarning("PassthroughToggle: no camera tagged MainCamera in this scene, so " +
                             "there is nothing to make transparent. Passthrough will not show.");
            return;
        }

        // Remember how the scene looked in VR so switching back is exact.
        vrClearFlags = xrCamera.clearFlags;
        vrBackgroundColor = xrCamera.backgroundColor;
    }

    private void Start()
    {
        SetPassthrough(startInPassthrough);
    }

    /// <summary>Wired to the hand menu's passthrough button. Takes no arguments so it can be
    /// dropped straight onto a UnityEvent.</summary>
    public void TogglePassthrough()
    {
        SetPassthrough(!isPassthrough);
    }

    public void SetPassthrough(bool on)
    {
        if (!IsAvailable)
        {
            // Not an error. This is the normal state in the Editor, where there is no headset
            // and no camera feed to show.
            isPassthrough = false;
            return;
        }

        if (xrCamera != null)
        {
            if (on)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                xrCamera.clearFlags = vrClearFlags;
                xrCamera.backgroundColor = vrBackgroundColor;
            }
        }

        PassthroughFeature.EnableVideoSeeThrough = on;
        isPassthrough = on;
    }

    /// <summary>Whether the scene is currently showing the room.</summary>
    public bool IsPassthroughOn()
    {
        return isPassthrough;
    }
}
