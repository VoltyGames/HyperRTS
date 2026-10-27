using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>
    /// Each unit's remaining waypoints, planned manoeuvres in orange with reverse legs dashed and gear changes as
    /// dots, each vehicle's rear axle, and a dotted line to its attack target.
    /// </summary>
    public sealed class PathsLayer : DebugLayer
    {
        private const float GearChangeSize = 0.35f;
        private const float AxleSize = 0.25f;
        private static readonly Color Manoeuvre = new(1f, 0.55f, 0f);

        public override string Label => "Paths and targets";

        public override void Draw(EntityManager entityManager)
        {
            DrawPaths(entityManager);
            DrawRearAxles(entityManager);
            DrawAttackTargets(entityManager);
        }

        private static void DrawPaths(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(typeof(PathWaypoint), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var path = entityManager.GetBuffer<PathWaypoint>(entity, true);
                var from = (Vector3)entityManager.GetComponentData<LocalTransform>(entity).Position;
                for (var i = 0; i < path.Length; i++)
                {
                    DrawLeg(from, path[i]);
                    if (i + 1 < path.Length && path[i + 1].Reverse != path[i].Reverse)
                    {
                        Handles.DrawSolidDisc(path[i].Position, Vector3.up, GearChangeSize);
                    }

                    from = path[i].Position;
                }
            }
        }

        private static void DrawLeg(Vector3 from, in PathWaypoint waypoint)
        {
            Handles.color = waypoint.Tight ? Manoeuvre : Color.cyan;
            if (waypoint.Reverse)
            {
                Handles.DrawDottedLine(from, waypoint.Position, 4f);
            }
            else
            {
                Handles.DrawLine(from, waypoint.Position);
            }
        }

        private static void DrawRearAxles(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(typeof(NavAgent), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            Handles.color = Manoeuvre;
            foreach (var entity in entities)
            {
                var chassis = entityManager.GetComponentData<NavAgent>(entity).Chassis;
                if (chassis.IsWheeled)
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    var facing = math.normalizesafe(transform.Forward().xz);
                    var rear = chassis.Pose(transform.Position.xz, facing).Position;
                    Handles.DrawWireDisc(new Vector3(rear.x, transform.Position.y, rear.y), Vector3.up, AxleSize);
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
