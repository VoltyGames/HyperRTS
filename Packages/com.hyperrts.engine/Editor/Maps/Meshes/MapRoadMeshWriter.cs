using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Editor.Maps.Meshes
{
    /// <summary>
    /// Road surfaces as ribbon meshes over the final terrain: every road in the shapes (authored roads and the tracks
    /// planners laid, in every copy) with a class material, one mesh per road class. Meshes are saved as sub-assets of
    /// the map's road asset. Where roads overlap, the higher class rank draws on top: each class's renderer takes its
    /// rank as sorting order, which the render pipeline sorts before render queue, so the shared class materials stay
    /// untouched.
    /// </summary>
    public static class MapRoadMeshWriter
    {
        /// <summary>
        /// Writes the road meshes to <c>paths.Roads</c> and creates a "Roads" object under <paramref name="parent"/>
        /// with one child per road class; returns it, or null when no road class has a material.
        /// </summary>
        public static Transform Write(MapPaths paths, MapLayout layout, MapShapes shapes, MapHeights heights,
            Transform parent)
        {
            AssetDatabase.DeleteAsset(paths.Roads);
            var meshes = Build(shapes, heights, layout.GroundLevel);
            if (meshes.Count == 0)
            {
                return null;
            }

            var root = new GameObject("Roads");
            root.transform.SetParent(parent, false);
            for (var i = 0; i < meshes.Count; i++)
            {
                var (roadClass, mesh) = meshes[i];
                if (i == 0)
                {
                    AssetDatabase.CreateAsset(mesh, paths.Roads);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, paths.Roads);
                }

                CreateObject(root.transform, roadClass, mesh);
            }

            return root.transform;
        }

        /// <summary>One ribbon mesh per road class that has a material, in the order the classes first appear.</summary>
        public static List<(MapRoadClass Class, Mesh Mesh)> Build(MapShapes shapes, MapHeights heights, float groundLevel)
        {
            var meshes = new List<(MapRoadClass, Mesh)>();
            var roads = shapes.Roads;
            foreach (var group in roads.Where(r => r.Class.material != null).GroupBy(r => r.Class))
            {
                var data = new MapRoadMesh();
                foreach (var road in group)
                {
                    var (start, end) = MapRoadFades.For(road, roads);
                    MapRoadRibbon.Add(data, road, start, end, heights, groundLevel, p => shapes.RiverEdgeDistance(p) < 0f);
                }

                if (data.Count > 0)
                {
                    meshes.Add((group.Key, data.ToMesh($"Road {group.Key.key}")));
                }
            }

            return meshes;
        }

        private static void CreateObject(Transform root, MapRoadClass roadClass, Mesh mesh)
        {
            var go = new GameObject($"Road {roadClass.key}");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = roadClass.material;
            renderer.sortingOrder = roadClass.rank;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
