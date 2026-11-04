using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Squads;

namespace HyperRTS.Presentation.HUD.Commands
{
    /// <summary>Finds the working retreat and reinforce points a player's squads can use.</summary>
    public sealed class SquadPoints
    {
        private readonly LiveQuery _retreatPoints = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<RetreatPoint>(), ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>()));

        private readonly LiveQuery _reinforcePoints = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ReinforcePoint>(), ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>()));

        public bool HasRetreat(HUDContext context, byte faction)
        {
            var entityManager = context.EntityManager;
            using var entities = _retreatPoints.In(entityManager).ToEntityArray(Allocator.Temp);
            foreach (var point in entities)
            {
                if (IsWorking(entityManager, point) && IsAllied(context, faction, point))
                {
                    return true;
                }
            }

            return false;
        }

        public NativeList<(float2 Position, float Range)> FindReinforce(HUDContext context, byte faction)
        {
            var entityManager = context.EntityManager;
            var found = new NativeList<(float2, float)>(Allocator.Temp);
            using var entities = _reinforcePoints.In(entityManager).ToEntityArray(Allocator.Temp);
            foreach (var point in entities)
            {
                if (IsWorking(entityManager, point) && IsAllied(context, faction, point))
                {
                    var position = entityManager.GetComponentData<LocalTransform>(point).Position.xz;
                    found.Add((position, entityManager.GetComponentData<ReinforcePoint>(point).Range));
                }
            }

            return found;
        }

        public static bool IsInRange(EntityManager entityManager, Entity squad,
            NativeList<(float2 Position, float Range)> points)
        {
            var here = entityManager.GetComponentData<LocalTransform>(squad).Position.xz;
            foreach (var (position, range) in points)
            {
                if (math.distancesq(here, position) <= range * range)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAllied(HUDContext context, byte faction, Entity point) =>
            context.View.Relations.IsAllied(faction, context.EntityManager.GetComponentData<Faction>(point).Value);

        private static bool IsWorking(EntityManager entityManager, Entity point) =>
            !entityManager.HasEnabled<Dead>(point) && !entityManager.HasEnabled<Offline>(point);
    }
}
