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
    /// Road surfaces as ribbon meshes over the final terrain: every road in the shapes (authored roads and the tracks
    /// planners laid, in every copy) with a class material, one mesh per road class. Meshes are saved as sub-assets of
    /// the map's road asset. Where roads overlap, the higher class rank draws on top: each class's renderer takes its
    /// rank as sorting order, which the render pipeline sorts before render queue, so the shared class materials stay
    /// untouched.
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
            for (var i = 0; i < meshes.Count; i++)
            {
                var (roadClass, mesh) = meshes[i];
                if (i == 0)
                {
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, path);
                }

                CreateObject(root, roadClass, mesh);
            }
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
                    data.AddRibbon(road, start, end, heights, groundLevel, p => shapes.RiverEdgeDistance(p) < 0f);
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
