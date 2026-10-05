using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Spatial;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>Draws live simulation state in the Scene view during Play mode; toggled from the debug overlay.</summary>
    [InitializeOnLoad]
    internal static class DebugDraw
    {
        private const float Height = 0.1f;
        private const double RepaintSeconds = 0.1;

        public static bool NavGrid;
        public static bool Fog;
        public static bool Paths;
        public static bool Spatial;

        private static double _nextRepaint;
        private static Material _material;

        static DebugDraw()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += RepaintWhileActive;
        }

        private static bool Active => NavGrid || Fog || Paths || Spatial;

        // Entity changes don't repaint the Scene view on their own.
        private static void RepaintWhileActive()
        {
            if (Active && Application.isPlaying && EditorApplication.timeSinceStartup > _nextRepaint)
            {
                _nextRepaint = EditorApplication.timeSinceStartup + RepaintSeconds;
                SceneView.RepaintAll();
            }
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!Active || Event.current.type != EventType.Repaint || !PlayWorld.TryGet(out var entityManager))
            {
                return;
            }

            if (NavGrid && PlayWorld.TryGetSingleton(entityManager, out NavGrid grid) && grid.IsCreated)
            {
                DrawBlockedCells(grid);
            }

            if (Fog && PlayWorld.TryGetSingleton(entityManager, out FogOfWar fog) && fog.IsCreated &&
                PlayWorld.TryGetSingleton(entityManager, out LocalFogView local))
            {
                DrawVisibleCells(fog, local.Team);
            }

            if (Spatial && PlayWorld.TryGetSingleton(entityManager, out SpatialIndex index) && index.Cells.IsCreated)
            {
                DrawSpatialCells(index);
            }

            if (Paths)
            {
                DrawPaths(entityManager);
                DrawAttackTargets(entityManager);
            }
        }

        private static void DrawBlockedCells(in NavGrid grid)
        {
            BeginQuads(new Color(1f, 0.2f, 0.2f, 0.35f));
            for (var y = 0; y < grid.Size.y; y++)
            {
                for (var x = 0; x < grid.Size.x; x++)
                {
                    if (!grid.IsWalkable(new int2(x, y)))
                    {
                        Quad(grid.Min + new float2(x, y) * grid.CellSize, grid.CellSize);
                    }
                }
            }

            GL.End();
        }

        private static void DrawVisibleCells(in FogOfWar fog, byte team)
        {
            BeginQuads(new Color(0.3f, 1f, 0.4f, 0.2f));
            for (var i = 0; i < fog.Visible.Length; i++)
            {
                if ((fog.Visible[i] & (1 << team)) != 0)
                {
                    Quad(fog.Min + new float2(i % fog.Size.x, i / fog.Size.x) * fog.CellSize, fog.CellSize);
                }
            }

            GL.End();
        }

        private static void DrawSpatialCells(in SpatialIndex index)
        {
            var (keys, count) = index.Cells.GetUniqueKeyArray(Allocator.Temp);
            BeginQuads(new Color(0.3f, 0.6f, 1f, 0.25f));
            for (var i = 0; i < count; i++)
            {
                // Inverse of SpatialIndex.Key: low 16 bits are signed x, high bits y.
                var cell = new int2((short)(keys[i] & 0xFFFF), keys[i] >> 16);
                Quad((float2)cell * index.CellSize, index.CellSize);
            }

            GL.End();
            keys.Dispose();
        }

        private static void DrawPaths(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(typeof(PathWaypoint), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            Handles.color = Color.cyan;
            foreach (var entity in entities)
            {
                var path = entityManager.GetBuffer<PathWaypoint>(entity, true);
                var from = (Vector3)entityManager.GetComponentData<LocalTransform>(entity).Position;
                foreach (var waypoint in path)
                {
                    Handles.DrawLine(from, waypoint.Position);
                    from = waypoint.Position;
                }
            }
        }

        private static void DrawAttackTargets(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(typeof(AttackTarget), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            Handles.color = Color.red;
            foreach (var entity in entities)
            {
                var target = entityManager.GetComponentData<AttackTarget>(entity).Value;
                if (entityManager.Exists(target) && entityManager.HasComponent<LocalTransform>(target))
                {
                    Handles.DrawDottedLine(entityManager.GetComponentData<LocalTransform>(entity).Position,
                        entityManager.GetComponentData<LocalTransform>(target).Position, 4f);
                }
            }
        }

        private static void BeginQuads(Color color)
        {
            if (_material == null)
            {
                _material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                _material.SetInt("_Cull", 0);
                _material.SetInt("_ZWrite", 0);
            }

            _material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(color);
        }

        private static void Quad(float2 min, float size)
        {
            GL.Vertex3(min.x, Height, min.y);
            GL.Vertex3(min.x, Height, min.y + size);
            GL.Vertex3(min.x + size, Height, min.y + size);
            GL.Vertex3(min.x + size, Height, min.y);
        }
    }
}
