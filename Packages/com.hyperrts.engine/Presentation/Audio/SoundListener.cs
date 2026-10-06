using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
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
            var focus = TryTerrainFocus(eye, camera.farClipPlane, out var ground) ? ground : GroundFocus(eye);
            transform.SetPositionAndRotation(focus, rotation);
        }

        /// <summary>Where the view ray meets the baked terrain, when the match has one.</summary>
        private bool TryTerrainFocus(Transform eye, float range, out Vector3 point)
        {
            point = default;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            if (!_terrain.In(world.EntityManager).TryGetSingleton(out TerrainHeight terrain))
            {
                return false;
            }

            var hit = terrain.Raycast(eye.position, eye.forward, range, out var ground);
            point = ground;
            return hit;
        }

        /// <summary>Where the view ray meets the y = 0 ground; below the camera when it doesn't look down.</summary>
        private static Vector3 GroundFocus(Transform eye)
        {
            var position = eye.position;
            var forward = eye.forward;
            if (forward.y > -0.01f)
            {
                return new Vector3(position.x, 0f, position.z);
            }

            return position + forward * (position.y / -forward.y);
        }
    }
}
