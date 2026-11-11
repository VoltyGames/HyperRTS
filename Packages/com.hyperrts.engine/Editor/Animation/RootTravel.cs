using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Animation
{
    /// <summary>The root bone's ground travel over each clip, measured once and removed from in place poses.</summary>
    internal sealed class RootTravel
    {
        /// <summary>Ground speed in m/s below which a clip counts as having none.</summary>
        public const float MinGroundSpeed = 0.1f;

        private readonly GameObject _root;
        private readonly Transform _rootBone;
        private readonly Dictionary<AnimationClip, (Vector3 Start, Vector3 End)> _ends = new();

        public RootTravel(GameObject root, Transform rootBone)
        {
            (_root, _rootBone) = (root, rootBone);
        }

        /// <summary>Samples <paramref name="clip"/> on the rig; in place removes the straight line of ground travel up to <paramref name="time"/>.</summary>
        public void Sample(AnimationClip clip, float time, bool inPlace)
        {
            // Measured before sampling: the first measure of a clip samples its ends, which would replace this pose.
            var drift = inPlace ? Drift(clip, time) : Vector3.zero;
            SampleAt(clip, time);
            _rootBone.position -= _root.transform.TransformVector(drift);
        }

        /// <summary>Metres per second the root bone travels over <paramref name="clip"/> played <paramref name="speed"/> times faster. Under <see cref="MinGroundSpeed"/> is sway, not travel, and reads 0.</summary>
        public float GroundSpeed(AnimationClip clip, float speed)
        {
            var (start, end) = Travel(clip);
            var drift = _root.transform.TransformVector(end - start);
            drift.y = 0f;
            var ground = drift.magnitude * speed / clip.length;
            return ground < MinGroundSpeed ? 0f : ground;
        }

        private void SampleAt(AnimationClip clip, float time) => clip.SampleAnimation(_rootBone.parent.gameObject, time);

        private Vector3 Drift(AnimationClip clip, float time)
        {
            var (start, end) = Travel(clip);
            var drift = Vector3.Lerp(start, end, Mathf.Clamp01(time / clip.length)) - start;
            drift.y = 0f;
            return drift;
        }

        private (Vector3 Start, Vector3 End) Travel(AnimationClip clip)
        {
            if (!_ends.TryGetValue(clip, out var ends))
            {
                SampleAt(clip, 0f);
                var start = _root.transform.InverseTransformPoint(_rootBone.position);
                SampleAt(clip, clip.length);
                ends = (start, _root.transform.InverseTransformPoint(_rootBone.position));
                _ends.Add(clip, ends);
            }

            return ends;
        }
    }
}
