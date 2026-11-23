using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// Road surfaces as one ribbon mesh per road class, plus the spills where a lower road meets a higher one, saved
    /// as sub-assets of the map's road asset. A higher class rank draws on top through the renderer's sorting order,
    /// leaving shared materials untouched.
    /// </summary>
    public static class MapRoadMeshWriter
    {
        /// <summary>
        /// Writes the road meshes to the map's road asset and a "Roads" object under the scene's generated root with one
        /// child per road class; nothing when no road class has a material.
        /// </summary>
        public static void Write(MapScene scene, MapGeneration generation)
        {
            var path = generation.Project.Paths.Roads;
            AssetDatabase.DeleteAsset(path);
            var meshes = Build(generation.Shapes, generation.Heights, generation.Layout.GroundLevel);
            if (meshes.Count == 0)
            {
                return;
            }

            var root = scene.CreateChild("Roads");
            meshes.AddRange(BuildSpills(generation.Shapes, generation.Heights, generation.Layout.GroundLevel));
            for (var i = 0; i < meshes.Count; i++)
            {
                var (roadClass, mesh, order) = meshes[i];
                if (i == 0)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, path);
                }

                CreateObject(root, roadClass, mesh, order);
            }
        }

        /// <summary>One ribbon mesh per road class that has a material, in the order the classes first appear.</summary>
        private static List<(MapRoadClass Class, Mesh Mesh, int Order)> Build(MapShapes shapes, MapHeights heights, float groundLevel)
        {
            var meshes = new List<(MapRoadClass, Mesh, int)>();
            var roads = shapes.Roads;
            foreach (var group in roads.Where(r => r.Class.material != null).GroupBy(r => r.Class))
            {
                var data = new MapRoadMesh(heights, groundLevel);
                foreach (var road in group)
                {
                    var (start, end) = MapRoadFades.For(road, roads);
                    data.AddRibbon(road, start, end, p => shapes.WaterEdgeDistance(p) < 0f || shapes.UnderSpan(p));
                }

                if (data.Count > 0)
                {
                    meshes.Add((group.Key, data.ToMesh($"Road {group.Key.key}"), group.Key.rank));
                }
            }

            return meshes;
        }

        /// <summary>
        /// Per lower class and higher rank, the ends that meet a higher-ranked road, drawn just above it so the lower
        /// surface fades onto it instead of stopping at its edge.
        /// </summary>
        private static List<(MapRoadClass Class, Mesh Mesh, int Order)> BuildSpills(MapShapes shapes, MapHeights heights,
            float groundLevel)
        {
            var spills = new Dictionary<(MapRoadClass, int), MapRoadMesh>();
            var roads = shapes.Roads;
            foreach (var road in roads.Where(r => r.Class.material != null))
            {
                var points = road.Line.Points;
                foreach (var atStart in new[] { true, false })
                {
                    var higher = MapRoadFades.HigherAt(road, atStart ? points[0] : points[^1], roads);
                    if (higher == null)
                    {
                        continue;
                    }

                    var key = (road.Class, higher.Class.rank + 1);
                    if (!spills.TryGetValue(key, out var data))
                    {
                        data = spills[key] = new MapRoadMesh(heights, groundLevel);
                    }

                    data.AddSpill(road, higher, atStart);
                }
            }

            return spills.Select(s => (s.Key.Item1, s.Value.ToMesh($"Spill {s.Key.Item1.key}"), s.Key.Item2)).ToList();
        }

        private static void CreateObject(Transform root, MapRoadClass roadClass, Mesh mesh, int order)
        {
            var go = new GameObject(order == roadClass.rank ? $"Road {roadClass.key}" : $"Road {roadClass.key} spill {order}");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadClass.material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
