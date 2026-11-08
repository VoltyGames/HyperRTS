using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>
    /// A track whose lower run bends over the lift of its road wheels. The wheels must be in the rig's wheel list, and
    /// the track's mesh needs a <see cref="TrackMeshAuthoring"/>.
    /// </summary>
    [Serializable]
    public class TrackSlot
    {
        [Tooltip("The track part, which carries its own mesh.")]
        public Transform part;

        [Tooltip("The road wheels the track runs over, in the rig's wheel list; Fill from model pairs Track_L with Wheel_L.")]
        public List<Transform> wheels = new();

        /// <summary>Why this track can't bake on <paramref name="rig"/>, or null when it can.</summary>
        public string Problem(MotionRigAuthoring rig)
        {
            if (part == null)
            {
                return "A TrackSlot has no part.";
            }

            if (!rig.IsBelow(part))
            {
                return $"Track '{part.name}' must be a child of '{rig.name}'.";
            }

            if (wheels.Count == 0)
            {
                return $"Track '{part.name}' has no wheels.";
            }

            if (wheels.Any(wheel => wheel == null || rig.wheels.All(slot => slot.part != wheel)))
            {
                return $"Track '{part.name}' lists a wheel that is not in the rig's wheels.";
            }

            return MeshProblem(rig);
        }

        private string MeshProblem(MotionRigAuthoring rig)
        {
            if (!part.TryGetComponent<MeshFilter>(out _))
            {
                return $"Track '{part.name}' has no mesh.";
            }

            if (!part.TryGetComponent<TrackMeshAuthoring>(out _))
            {
                return $"Track '{part.name}' has no Track Mesh component; Fill from model adds it.";
            }

            var turned = Quaternion.Angle(part.rotation, rig.transform.rotation) > 1f;
            var scaled = Vector3.Distance(part.lossyScale, rig.transform.lossyScale) > 0.01f;
            return turned || scaled ? $"Track '{part.name}' must be unrotated and unscaled against '{rig.name}'." : null;
        }

        /// <summary>
        /// The wheels in order from the front, with where each sits along the track from station 0 (front) to 7 (back).
        /// </summary>
        public IEnumerable<(Transform Wheel, float Station)> Stations(Transform rig)
        {
            var bounds = PartSlot.MeshBounds(part, rig);
            var length = Mathf.Max(bounds.size.z, 0.01f);
            return wheels.Select(wheel => (wheel, along: (bounds.max.z - rig.InverseTransformPoint(wheel.position).z) / length))
                .OrderBy(placed => placed.along)
                .Select(placed => (placed.wheel, Mathf.Clamp01(placed.along) * (MotionRigTrack.Stations - 1)));
        }

        /// <summary>
        /// The <c>_TrackSpan</c> in the track mesh's object space: front z, back z, the wheels' mean axle height and
        /// mean radius.
        /// </summary>
        public Vector4 Span(MotionRigAuthoring rig)
        {
            var bounds = PartSlot.MeshBounds(part, rig.transform);
            var axle = wheels.Average(wheel => rig.transform.InverseTransformPoint(wheel.position).y);
            var radius = rig.wheels.Where(slot => wheels.Contains(slot.part)).Average(slot => slot.radius);
            Vector3 ToMesh(float height, float z) => part.InverseTransformPoint(rig.transform.TransformPoint(new Vector3(0f, height, z)));
            var level = ToMesh(axle, 0f).y;
            return new Vector4(ToMesh(axle, bounds.max.z).z, ToMesh(axle, bounds.min.z).z, level, ToMesh(axle + radius, 0f).y - level);
        }
    }
}
