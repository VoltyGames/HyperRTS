using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Animation.Clips
{
    /// <summary>The named clips and all-row bounds of one animation texture.</summary>
    public abstract class ClipTable : ScriptableObject
    {
        public List<NamedClip> clips = new();

        [Tooltip("Object-space minimum over every row of every clip.")]
        public Vector3 boundsMin;

        [Tooltip("Object-space maximum over every row of every clip.")]
        public Vector3 boundsMax;

        public AABB Bounds => new MinMaxAABB { Min = boundsMin, Max = boundsMax };

        public ClipRange Range(string clipName) =>
            TryRange(clipName, out var range)
                ? range
                : throw new KeyNotFoundException($"Clip table '{name}' has no clip '{clipName}'.");

        public bool TryRange(string clipName, out ClipRange range)
        {
            foreach (var clip in clips)
            {
                if (clip.name == clipName)
                {
                    range = clip.Range;
                    return true;
                }
            }

            range = default;
            return false;
        }
    }
}
