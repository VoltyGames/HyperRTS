using System;
using System.Collections.Generic;
using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A prop the map builder stretches by moving its end caps out and lengthening only the span between them.
    /// A plain MonoBehaviour, not an <see cref="Common.AuthoringBehaviour"/>, so it doesn't make a prop an entity.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Stretch Parts")]
    [Icon(HyperRTSIcons.Combat)]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class StretchPartsAuthoring : MonoBehaviour
    {
        [Tooltip("Part at the long axis's negative end; moves out and keeps its size.")]
        public Transform startCap;

        [Tooltip("Part at the long axis's positive end; moves out and keeps its size.")]
        public Transform endCap;

        [Tooltip("Part between the caps (with a MeshFilter); scales along the long axis only.")]
        public Transform span;

        [Tooltip("Set by the map builder on a placed instance: how far each cap sits out (root-space metres). 0 on the prefab.")]
        public float extension;

        /// <summary>What keeps this prefab from stretching; empty when it can.</summary>
        public IEnumerable<string> Problems()
        {
            var parts = new[] { ("startCap", startCap), ("endCap", endCap), ("span", span) };
            foreach (var (field, part) in parts)
            {
                if (part == null)
                {
                    yield return $"{field} is not set";
                    continue;
                }

                if (!part.IsChildOf(transform) || part == transform)
                {
                    yield return $"{field} '{part.name}' must be inside the prefab, under its root";
                }
            }

            if (!SharedParent())
            {
                yield return "startCap, endCap and span must share one parent";
            }

            if (span != null && SpanMesh(span) == null)
            {
                yield return $"span '{span.name}' needs a MeshFilter with a mesh";
            }

            if (extension != 0f)
            {
                yield return $"extension {extension} must be 0 on the prefab; the map builder sets it on placed instances";
            }
        }

        public StretchLayout Layout() =>
            StretchLayout.Measure(transform, startCap, endCap, span, RequireSpanMesh().bounds);

        /// <summary>Moves the caps out by <paramref name="by"/> and stretches the span to meet them, once per instance.</summary>
        public void Extend(float by)
        {
            if (extension != 0f)
            {
                throw new InvalidOperationException($"HyperRTS: '{name}' is already stretched (extension {extension}).");
            }

            var layout = Layout();
            startCap.localPosition = layout.StartCapAt(by);
            endCap.localPosition = layout.EndCapAt(by);
            span.localPosition = layout.SpanPositionAt(by);
            span.localScale = layout.SpanScaleAt(by);
            extension = by;
        }

        private bool SharedParent()
        {
            if (startCap == null || endCap == null || span == null)
            {
                return true;
            }

            var parent = startCap.parent;
            return endCap.parent == parent && span.parent == parent;
        }

        private static Mesh SpanMesh(Transform span) =>
            span.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;

        private Mesh RequireSpanMesh()
        {
            var mesh = span != null ? SpanMesh(span) : null;
            if (mesh == null)
            {
                throw new InvalidOperationException($"HyperRTS: '{name}' stretch span needs a MeshFilter with a mesh.");
            }

            return mesh;
        }

        /// <summary>
        /// A stretched instance already has its parts placed, so it only carries its extension for its remains; a
        /// rest one (remains prefabs) bakes its parts to be stretched when it gets one.
        /// </summary>
        public class Baker : Baker<StretchPartsAuthoring>
        {
            public override void Bake(StretchPartsAuthoring authoring)
            {
                var root = GetEntity(TransformUsageFlags.Dynamic);
                if (authoring.extension != 0f)
                {
                    AddComponent(root, new PartStretch { Extension = authoring.extension });
                    return;
                }

                var mesh = GetComponent<MeshFilter>(authoring.span).sharedMesh;
                DependsOn(mesh);
                // A dynamic parent keeps the parts' transforms relative to it, the space the layout is in.
                GetEntity(GetComponent<Transform>(authoring.startCap.parent), TransformUsageFlags.Dynamic);
                var layout = StretchLayout.Measure(authoring.transform, GetComponent<Transform>(authoring.startCap),
                    GetComponent<Transform>(authoring.endCap), GetComponent<Transform>(authoring.span), mesh.bounds);
                AddComponent(root, new StretchParts
                {
                    StartCap = GetEntity(authoring.startCap, TransformUsageFlags.Dynamic),
                    EndCap = GetEntity(authoring.endCap, TransformUsageFlags.Dynamic),
                    Span = GetEntity(authoring.span, TransformUsageFlags.Dynamic),
                    Layout = layout,
                });
            }
        }
    }
}
