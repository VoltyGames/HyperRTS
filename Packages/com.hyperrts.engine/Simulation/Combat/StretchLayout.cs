using System;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Rest pose of a prop's stretch parts in their shared parent's space: a start cap and an end cap along one axis
    /// with a span between them. Stretching moves the caps out by an extension (root-space metres), keeping their
    /// size, and scales the span along the axis only so it still meets both.
    /// </summary>
    public struct StretchLayout
    {
        private static Vector3[] Units() => new[] { Vector3.right, Vector3.up, Vector3.forward };

        /// <summary>The long axis in the parts' space: +X, +Y or +Z, from the start cap toward the end cap.</summary>
        public float3 Axis;

        public float3 StartCap;
        public float3 EndCap;
        public float3 SpanPosition;
        public float3 SpanScale;

        /// <summary>The span's own axis that lies along <see cref="Axis"/>, as a 0/1 mask.</summary>
        public float3 SpanAxis;

        /// <summary>The span's ends along <see cref="Axis"/>, in the parts' space.</summary>
        public float SpanMin;
        public float SpanMax;

        /// <summary>Root-space metres per parts-space unit along <see cref="Axis"/> (1 when the parts are the root's children).</summary>
        public float RootUnits;

        /// <summary>How far each cap moves out when a prop <paramref name="length"/> long is stretched by <paramref name="stretch"/>.</summary>
        public static float Extension(float length, float stretch) => length * (stretch - 1f) * 0.5f;

        public float3 StartCapAt(float extension) => StartCap - Axis * Local(extension);

        public float3 EndCapAt(float extension) => EndCap + Axis * Local(extension);

        /// <summary>The span's scale along the axis that makes it reach both moved caps.</summary>
        public float SpanFactor(float extension)
        {
            var length = SpanMax - SpanMin;
            return (length + 2f * Local(extension)) / length;
        }

        /// <summary>The span pivot after scaling, so the span's start end lands on the moved start cap.</summary>
        public float3 SpanPositionAt(float extension)
        {
            var along = math.dot(SpanPosition, Axis);
            var moved = SpanMin - Local(extension) + (along - SpanMin) * SpanFactor(extension);
            return SpanPosition + Axis * (moved - along);
        }

        public float3 SpanScaleAt(float extension) => SpanScale * (1f + SpanAxis * (SpanFactor(extension) - 1f));

        private float Local(float extension) => extension / RootUnits;

        /// <summary>Reads the rest pose from the parts' local transforms (one parent under the root) and the span mesh's bounds.</summary>
        public static StretchLayout Measure(Transform root, Transform startCap, Transform endCap, Transform span, Bounds spanBounds)
        {
            var axis = LongAxis(Centre(startCap), Centre(endCap));
            var rootUnits = root.InverseTransformVector(startCap.parent.TransformVector(axis)).magnitude;
            var spanAxis = AxisAlong(span.localRotation, axis);
            var center = Vector3.Dot(span.localPosition + span.localRotation * Vector3.Scale(span.localScale, spanBounds.center), axis);
            var half = Mathf.Abs(Vector3.Dot(Vector3.Scale(span.localScale, spanBounds.extents), spanAxis));
            if (half <= 0f)
            {
                throw new InvalidOperationException($"HyperRTS: stretch span '{span.name}' has no length along the long axis.");
            }

            return new StretchLayout
            {
                Axis = axis, StartCap = startCap.localPosition, EndCap = endCap.localPosition,
                SpanPosition = span.localPosition, SpanScale = span.localScale, SpanAxis = spanAxis,
                SpanMin = center - half, SpanMax = center + half, RootUnits = rootUnits,
            };
        }

        // Art often keeps every part's pivot at the prop origin, so a cap's place is its mesh, when it has one.
        private static Vector3 Centre(Transform cap)
        {
            if (!cap.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
            {
                return cap.localPosition;
            }

            return cap.localPosition + cap.localRotation * Vector3.Scale(cap.localScale, filter.sharedMesh.bounds.center);
        }

        /// <summary>The axis the caps lie furthest apart along; the end cap must be on its positive side.</summary>
        private static Vector3 LongAxis(Vector3 start, Vector3 end)
        {
            var apart = end - start;
            var axis = Vector3.right;
            foreach (var unit in Units())
            {
                axis = Mathf.Abs(Vector3.Dot(apart, unit)) > Mathf.Abs(Vector3.Dot(apart, axis)) ? unit : axis;
            }

            if (Vector3.Dot(apart, axis) <= 0f)
            {
                throw new InvalidOperationException(
                    "HyperRTS: stretch endCap must lie further than startCap along the parts' long axis (+X, +Y or +Z).");
            }

            return axis;
        }

        /// <summary>The span's own axis that its rotation turns onto <paramref name="axis"/>.</summary>
        private static Vector3 AxisAlong(Quaternion rotation, Vector3 axis)
        {
            foreach (var unit in Units())
            {
                var turned = rotation * unit;
                if (Mathf.Abs(Vector3.Dot(turned, axis)) > 0.999f)
                {
                    return unit;
                }
            }

            throw new InvalidOperationException(
                "HyperRTS: stretch span must be turned so one of its own axes lies along the long axis.");
        }
    }
}
