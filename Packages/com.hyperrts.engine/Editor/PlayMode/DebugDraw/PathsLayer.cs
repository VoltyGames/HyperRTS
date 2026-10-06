using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Each unit's remaining waypoints and a dotted line to its attack target.</summary>
    public sealed class PathsLayer : DebugLayer
    {
        public override string Label => "Paths and targets";

        public override void Draw(EntityManager entityManager)
        {
            DrawPaths(entityManager);
            DrawAttackTargets(entityManager);
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
    }
}
