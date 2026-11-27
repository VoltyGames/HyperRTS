using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Input.Cameras;

/// <summary>
/// Holds the camera on a scripted <see cref="ICameraPath"/> while one plays, over the player's controls, for cutscenes
/// and recordings. The controller's shake still applies, and trees stay drawn rather than fading.
/// </summary>
[AddComponentMenu(HyperRTSMenu.Scene + "Rig/Cinematic Camera")]
[Icon(HyperRTSIcons.CameraController)]
[HelpURL(HyperRTSDocs.Input)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
// Before the sound listener's LateUpdate, so sound is heard from the scripted view in the same frame.
[DefaultExecutionOrder(-10)]
public class CinematicCamera : PresentationBehaviour
{
    [Tooltip("Metres the scripted view keeps above the terrain beneath it.")]
    public float terrainClearance = 1.5f;

    private readonly LiveSingleton<TerrainHeight> _terrain = new();
    private CameraController _controller;

    /// <summary>The move playing; null hands the camera back to the player.</summary>
    public ICameraPath Path { get; set; }

    protected override void OnAwake() => _controller = GetComponent<CameraController>();

    // After the controller's Update has placed the player's view, so the path wins.
    private void LateUpdate()
    {
        if (Path == null)
        {
            return;
        }

        var view = Path.ViewAt(Time.time);
        _terrain.TryGetInDefaultWorld(out var terrain);
        var ground = terrain.Height(new float2(view.position.x, view.position.z));
        view.position.y = CameraController.ClearedHeight(view.position.y, ground, terrainClearance);
        var shake = _controller != null ? _controller.shake.Offset : Pose.identity;
        transform.SetPositionAndRotation(view.position + shake.position, view.rotation * shake.rotation);
        CameraTreeFade.Stop();
    }
}
