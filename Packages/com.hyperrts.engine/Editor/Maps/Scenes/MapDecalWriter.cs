using System.Collections.Generic;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Ground decals as URP decal projectors on the finished ground, projecting along its normal.</summary>
    public static class MapDecalWriter
    {
        /// <summary>Faces steeper than these angles to the projection (props' sides, walls) fade out.</summary>
        private const float StartAngleFade = 50f;
        private const float EndAngleFade = 75f;

        private const float MinNormalSpan = 2f;

        public static void Write(MapScene scene, MapGeneration generation)
        {
            var root = scene.CreateChild("Decals");
            var counts = new Dictionary<string, int>();
            foreach (var decal in generation.Decals)
            {
                var key = decal.Kind.key;
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
                Create(root, $"{key} {counts[key]}", decal, generation);
            }
        }

        private static void Create(Transform root, string name, MapDecal decal, MapGeneration generation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var heights = generation.Heights;
            var point = decal.Position;
            var y = heights.Height(point) - generation.Layout.GroundLevel;
            var normal = heights.Normal(point, Mathf.Max(MinNormalSpan, decal.Diameter * 0.5f));
            go.transform.SetPositionAndRotation(new Vector3(point.x, y, point.y), Facing(normal, decal.Yaw));
            var projector = go.AddComponent<DecalProjector>();
            projector.material = decal.Kind.material;
            projector.pivot = Vector3.zero;
            projector.size = new Vector3(decal.Diameter, decal.Diameter, decal.Kind.depth);
            projector.startAngleFade = StartAngleFade;
            projector.endAngleFade = EndAngleFade;
            projector.uvScale = new Vector2(decal.Mirrored ? -1f : 1f, 1f);
            projector.uvBias = new Vector2(decal.Mirrored ? 1f : 0f, 0f);
        }

        /// <summary>Projecting down the normal, the texture's top turned <paramref name="yaw"/> degrees clockwise from north.</summary>
        private static Quaternion Facing(Vector3 normal, float yaw)
        {
            var north = Vector3.Cross(Vector3.right, normal).normalized;
            var up = Quaternion.AngleAxis(yaw, normal) * north;
            return Quaternion.LookRotation(-normal, up);
        }
    }
}
