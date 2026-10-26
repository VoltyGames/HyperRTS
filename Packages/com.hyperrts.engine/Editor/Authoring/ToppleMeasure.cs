using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Combat;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Measures a Topple's trunk from the standing model and its lie from the remains model.</summary>
    public static class ToppleMeasure
    {
        // Averaged over a band so one branch tip doesn't steer the trunk top.
        private const float TopBand = 0.05f;

        // Closer than this, the remains' mass gives no clear direction.
        private const float MinReach = 0.25f;

        /// <summary>Sets trunk and lie from the LODGroup model in its children and its Destructible's remains.</summary>
        public static void Measure(ToppleAuthoring topple, Material trunk)
        {
            var model = topple.GetComponentInChildren<LODGroup>();
            if (model == null)
            {
                throw new InvalidOperationException($"{topple.name} has no LODGroup model in its children to measure the trunk from.");
            }

            var remains = topple.GetComponent<DestructibleAuthoring>().remains;
            if (remains == null)
            {
                throw new InvalidOperationException($"{topple.name}: Destructible 'remains' is empty, so there is no lie to measure.");
            }

            var top = Trunk(topple.transform, model, trunk);
            topple.lie = Lie(remains);
            topple.trunk = top;
        }

        /// <summary>From the origin of <paramref name="root"/> to the top of the LOD0 faces drawn with <paramref name="trunk"/>.</summary>
        public static Vector3 Trunk(Transform root, LODGroup model, Material trunk)
        {
            var points = new List<Vector3>();
            foreach (var renderer in model.GetLODs()[0].renderers)
            {
                AddVertices(root, renderer, trunk, points);
            }

            if (points.Count == 0)
            {
                throw new InvalidOperationException($"{model.name} LOD0 has no faces drawn with '{trunk.name}'.");
            }

            var top = float.MinValue;
            var bottom = float.MaxValue;
            foreach (var point in points)
            {
                top = Mathf.Max(top, point.y);
                bottom = Mathf.Min(bottom, point.y);
            }

            var floor = top - (top - bottom) * TopBand;
            var sum = Vector3.zero;
            var count = 0;
            foreach (var point in points)
            {
                if (point.y >= floor)
                {
                    sum += point;
                    count++;
                }
            }

            return sum / count;
        }

        /// <summary>Degrees from +Z toward +X, from the stump at the origin to the mass of <paramref name="remains"/>.</summary>
        public static float Lie(GameObject remains)
        {
            var points = new List<Vector3>();
            foreach (var renderer in remains.GetComponentsInChildren<MeshRenderer>())
            {
                AddVertices(remains.transform, renderer, null, points);
            }

            if (points.Count == 0)
            {
                throw new InvalidOperationException($"{remains.name} has no mesh to measure its lie from.");
            }

            var centre = Vector3.zero;
            foreach (var point in points)
            {
                centre += point;
            }

            centre /= points.Count;
            if (new Vector2(centre.x, centre.z).magnitude < MinReach)
            {
                throw new InvalidOperationException(
                    $"{remains.name} lies around its origin: put the stump at the origin, the canopy away.");
            }

            return Mathf.Atan2(centre.x, centre.z) * Mathf.Rad2Deg;
        }

        /// <summary>Vertices in <paramref name="root"/> space of faces drawn with <paramref name="material"/>, or all if null.</summary>
        private static void AddVertices(Transform root, Renderer renderer, Material material, List<Vector3> into)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var materials = renderer.sharedMaterials;
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (material != null && materials[sub] != material)
                {
                    continue;
                }

                foreach (var index in mesh.GetIndices(sub))
                {
                    into.Add(root.InverseTransformPoint(renderer.transform.TransformPoint(vertices[index])));
                }
            }
        }
    }
}
