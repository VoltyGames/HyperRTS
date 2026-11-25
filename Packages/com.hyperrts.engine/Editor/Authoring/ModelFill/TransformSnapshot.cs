using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Authoring.ModelFill
{
    /// <summary>The local position, rotation and scale of every transform under a root, to put back after a change.</summary>
    public sealed class TransformSnapshot
    {
        private readonly List<(Transform Transform, Vector3 Position, Quaternion Rotation, Vector3 Scale)> _placed;

        public TransformSnapshot(Transform root) => _placed = root.GetComponentsInChildren<Transform>(true)
            .Select(transform => (transform, transform.localPosition, transform.localRotation, transform.localScale))
            .ToList();

        public void Restore()
        {
            foreach (var (transform, position, rotation, scale) in _placed)
            {
                transform.SetLocalPositionAndRotation(position, rotation);
                transform.localScale = scale;
            }
        }
    }
}
