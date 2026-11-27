using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Audio;

/// <summary>
/// Keeps the audio listener between the ground point the camera looks at and the camera, facing the camera's
/// heading, so panning matches the screen and zooming out hears the field from farther away. Put it on the only
/// <see cref="AudioListener"/> in the scene.
/// </summary>
[AddComponentMenu(HyperRTSMenu.Scene + "Rig/Sound Listener")]
[Icon(HyperRTSIcons.SoundListener)]
[HelpURL(HyperRTSDocs.Audio)]
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioListener))]
public class SoundListener : PresentationBehaviour
{
    [Tooltip("Camera to follow; empty uses the main camera.")]
    public Camera view;

    [Tooltip("Share of the way from the ground focus back to the camera where the listener sits. 0 hears every " +
             "zoom as if standing at the focus; higher makes zoomed-out views quieter and more distant.")]
    [Range(0f, 1f)]
    public float lift = 0.8f;

    private readonly LiveSingleton<TerrainHeight> _terrain = new();

    private void LateUpdate()
    {
        var camera = MainCamera.Find(view);
        if (camera == null)
        {
            return;
        }

        var eye = camera.transform;
        var heading = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
        var rotation = heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading) : eye.rotation;
        var position = Vector3.Lerp(Focus(eye, camera.farClipPlane), eye.position, lift);
        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>Where the view ray meets the ground; the ground below the camera when it doesn't.</summary>
    private float3 Focus(Transform eye, float range) =>
        ViewGround.Focus(MatchTerrain(), eye.position, eye.forward, range);

    /// <summary>The match's baked terrain; the default (flat y = 0) before a world or terrain exists.</summary>
    private TerrainHeight MatchTerrain()
    {
        _terrain.TryGetInDefaultWorld(out var terrain);
        return terrain;
    }
}
