using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Docks;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Movement
{
    /// <summary>
    /// Each unit's remaining waypoints, planned manoeuvres, rear axles, attack target lines and dock bays; its status
    /// totals the pathfinder's work.
    /// </summary>
    public sealed class PathsLayer : DebugLayer
    {
        private const float GearChangeSize = 0.35f;
        private const float AxleSize = 0.25f;
        private static readonly Color32 Waypoint = new(0, 255, 255, 255);
        private static readonly Color32 Manoeuvre = new(255, 140, 0, 255);
        private static readonly Color32 Target = new(255, 40, 40, 255);
        private static readonly Color32 RunIn = new(40, 230, 40, 255);
        private static readonly Color32 Exit = new(255, 230, 0, 255);

        public override string Label => "Paths";

        public override string Detail =>
            "Cyan waypoints, orange tight manoeuvres and rear axles, dashed when reversing; red dashes to attack " +
            "targets; dock bays in green (run-in) and yellow (exit).";

        public override string Group => "Movement";

        public override string Glyph => "route";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            DrawPaths(entityManager, shapes);
            DrawRearAxles(entityManager, shapes);
            DrawAttackTargets(entityManager, shapes);
            DrawBays(entityManager, shapes);
        }

        // The totals cover every unit: the pathfinder keeps no count per unit.
        public override string Describe(EntityManager entityManager)
        {
            if (!TryGetPathfinding(entityManager, out var pathfinding))
            {
                return null;
            }

            var stats = pathfinding.Stats;
            var causes = stats.SearchesByCause;
            return $"Searches {stats.Searches}: {causes.x} orders, {causes.y} chases, {causes.z} grid changes, " +
                $"{causes.w} replans. Traces {stats.Traces}, retraces {stats.Retraces}, fields {stats.Fields}, " +
                $"expansions {stats.Expansions}, deferred drives {stats.DeferredDrives}.";
        }

        internal static bool TryGetPathfinding(EntityManager entityManager, out PathfindingSystem pathfinding)
        {
            var world = entityManager.World.Unmanaged;
            var handle = world.GetExistingUnmanagedSystem<PathfindingSystem>();
            pathfinding = default;
            if (handle == SystemHandle.Null)
            {
                return false;
            }

            pathfinding = world.GetUnsafeSystemRef<PathfindingSystem>(handle);
            return true;
        }

        private static void DrawPaths(EntityManager entityManager, DebugShapes shapes)
        {
            using var query = entityManager.CreateEntityQuery(typeof(PathWaypoint), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var path = entityManager.GetBuffer<PathWaypoint>(entity, true);
                var from = entityManager.GetComponentData<LocalTransform>(entity).Position;
                for (var i = 0; i < path.Length; i++)
                {
                    var waypoint = path[i];
                    var color = waypoint.Tight ? Manoeuvre : Waypoint;
                    if (waypoint.Reverse)
                    {
                        shapes.DashedLine(from, waypoint.Position, color);
                    }
                    else
                    {
                        shapes.Line(from, waypoint.Position, color);
                    }

                    if (i + 1 < path.Length && path[i + 1].Reverse != waypoint.Reverse)
                    {
                        shapes.Circle(waypoint.Position, GearChangeSize, Manoeuvre);
                    }

                    from = waypoint.Position;
                }
            }
        }

        private static void DrawRearAxles(EntityManager entityManager, DebugShapes shapes)
        {
            using var query = entityManager.CreateEntityQuery(typeof(NavAgent), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var chassis = entityManager.GetComponentData<NavAgent>(entity).Chassis;
                if (chassis.IsWheeled)
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    var facing = math.normalizesafe(transform.Forward().xz);
                    var rear = chassis.Pose(transform.Position.xz, facing).Position;
                    shapes.Circle(new float3(rear.x, transform.Position.y, rear.y), AxleSize, Manoeuvre);
                }
            }
        }

        private static void DrawBays(EntityManager entityManager, DebugShapes shapes)
        {
            using var query = entityManager.CreateEntityQuery(typeof(DockBay), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var transform = entityManager.GetComponentData<LocalTransform>(entity);
                foreach (var bay in entityManager.GetBuffer<DockBay>(entity, true))
                {
                    var pose = bay.WorldPose(transform);
                    shapes.Line(pose.Stop - new float3(pose.Facing.x, 0f, pose.Facing.y) * pose.RunIn, pose.Stop, RunIn);
                    shapes.Circle(pose.Stop, BayPose.StopReach, RunIn);
                    shapes.Line(pose.Stop, pose.Exit, Exit);
                    var occupant = bay.Reservation.Holder;
                    if (entityManager.Exists(occupant) && entityManager.HasComponent<LocalTransform>(occupant))
                    {
                        shapes.DashedLine(pose.Stop, entityManager.GetComponentData<LocalTransform>(occupant).Position, Exit);
                    }
                }
            }
        }

        private static void DrawAttackTargets(EntityManager entityManager, DebugShapes shapes)
        {
            using var query = entityManager.CreateEntityQuery(typeof(AttackTarget), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                var target = entityManager.GetComponentData<AttackTarget>(entity).Value;
                if (entityManager.Exists(target) && entityManager.HasComponent<LocalTransform>(target))
                {
                    shapes.DashedLine(entityManager.GetComponentData<LocalTransform>(entity).Position,
                        entityManager.GetComponentData<LocalTransform>(target).Position, Target);
                }
            }
        }
    }
}
