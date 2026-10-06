using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Audio
{
    /// <summary>
    /// Keeps the audio listener on the ground point the camera looks at, facing the camera's heading, so distance
    /// falloff and panning match what's on screen. Put it on the only <see cref="AudioListener"/> in the scene.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Audio + "Sound Listener")]
    [Icon(HyperRTSIcons.Audio)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public class SoundListener : MonoBehaviour
    {
        [Tooltip("Camera to follow; empty uses the main camera.")]
        public Camera view;

        private readonly LiveQuery _terrain = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainHeight>()));

        private void LateUpdate()
        {
            var camera = view != null ? view : Camera.main;
            if (camera == null)
            {
                return;
            }

            var eye = camera.transform;
            var heading = Vector3.ProjectOnPlane(eye.forward, Vector3.up);
            var rotation = heading.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(heading) : eye.rotation;
            transform.SetPositionAndRotation(Focus(eye, camera.farClipPlane), rotation);
        }

        /// <summary>Where the view ray meets the ground; the ground below the camera when it doesn't.</summary>
        private float3 Focus(Transform eye, float range) =>
            ViewGround.Focus(MatchTerrain(), eye.position, eye.forward, range);

        /// <summary>The match's baked terrain; the default (flat y = 0) before a world or terrain exists.</summary>
        private TerrainHeight MatchTerrain()
        {
            if (!DefaultWorld.TryGetEntityManager(out var entityManager))
            {
                return default;
            }

            _terrain.In(entityManager).TryGetSingleton(out TerrainHeight terrain);
            return terrain;
        }
    }
}
