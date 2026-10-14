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
            if (camera == null)
            {
                return false;
            }

            var limit = rig.maxDistance * reach;
            return (position - camera.transform.position).sqrMagnitude <= limit * limit;
        }

        /// <summary>The main camera, looked up again only once the cached one is gone.</summary>
        public static Camera Main(Camera cached) => cached != null ? cached : Camera.main;
    }
}
