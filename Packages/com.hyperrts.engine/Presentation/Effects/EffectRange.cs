using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>Distance culling of rigs against the camera.</summary>
    public static class EffectRange
    {
        /// <summary>
        /// Whether a rig at the position is within <paramref name="reach"/> × its max distance of the camera; nothing
        /// plays without a camera, since nobody would see it.
        /// </summary>
        public static bool InRange(Camera camera, EffectRig rig, Vector3 position, float reach = 1f)
        {
            return camera != null && InRange(camera.transform.position, rig, position, reach);
        }

        /// <summary>Whether a rig at the position is within <paramref name="reach"/> × its max distance.</summary>
        public static bool InRange(Vector3 eye, EffectRig rig, Vector3 position, float reach = 1f)
        {
            var limit = rig.maxDistance * reach;
            return (position - eye).sqrMagnitude <= limit * limit;
        }

        /// <summary>The main camera, looked up again only once the cached one is gone.</summary>
        public static Camera Main(Camera cached) => cached != null ? cached : Camera.main;
    }
}
