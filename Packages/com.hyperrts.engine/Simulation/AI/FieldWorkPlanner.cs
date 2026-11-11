using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Digs in at each of the slot's defence points: <see cref="AIPlayer.FieldWorkLines"/> cover lines stepping back
    /// toward the base, and with <see cref="AIPlayer.FieldWorkBarrier"/> a barrier line in front. A line that already
    /// has a work is not laid again.
    /// </summary>
    internal struct FieldWorkPlanner
    {
        // Metres: a line's length, the gap between cover lines (a trench is wide), and how far in front the barrier goes.
        private const float FieldWorkLength = 12f;
        private const float FieldWorkRowGap = 6f;
        private const float BarrierDistance = 5f;

        // Half the depth of the strip around a line's centre in which a work counts as that line.
        private const float FieldWorkStrip = 2.5f;

        private EntityQuery _query;
        private AIGroup _works;
        private NativeArray<AIFieldWork> _workData;
        private BuilderPlanner _builders;
        private SquadPlanner _squads;

        public static FieldWorkPlanner Create(ref SystemState state) => new()
        {
            _query = new EntityQueryBuilder(Allocator.Temp).WithAll<AIFieldWork, Owner, LocalTransform>()
                .WithNone<Dead>().Build(ref state),
        };

        public void Update(in BuilderPlanner builders, in SquadPlanner squads)
        {
            _works = AIGroup.From(_query);
            _workData = _query.ToComponentDataArray<AIFieldWork>(Allocator.Temp);
            _builders = builders;
            _squads = squads;
        }

        public readonly void Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var ai = state.EntityManager.GetComponentData<AIPlayer>(turn.Player);
            if (ai.FieldWorkLines <= 0 || !snapshot.Hints.TryGetSlot(turn.Owner, out var slot))
            {
                return;
            }

            for (var row = 0; row < ai.FieldWorkLines; row++)
            {
                DigRow(ref state, turn, snapshot, slot, FieldWorkRole.Cover, -row * FieldWorkRowGap);
            }

            if (ai.FieldWorkBarrier)
            {
                DigRow(ref state, turn, snapshot, slot, FieldWorkRole.Barrier, BarrierDistance);
            }
        }

        /// <summary>One line of <paramref name="role"/> at every defence point, <paramref name="distance"/> metres out from it.</summary>
        private readonly void DigRow(ref SystemState state, in AITurn turn, in AISnapshot snapshot, in AISlotHint slot,
            FieldWorkRole role, float distance)
        {
            for (var i = 0; i < slot.DefenceCount; i++)
            {
                var point = snapshot.Hints.DefencePoint(slot, i);
                var outward = math.normalizesafe(point - turn.Home.xz);
                if (math.all(outward == float2.zero))
                {
                    continue;
                }

                // Along the line, whichever way puts the segments' front toward the approach.
                var across = new float2(-outward.y, outward.x);
                var along = math.dot(PlacementLine.Front(across), outward) > 0f ? across : -across;
                var centre = point + outward * distance;
                if (HasFieldWork(turn.Owner, role, centre, along, outward))
                {
                    continue;
                }

                if (!TryDig(ref state, turn, snapshot, role, centre, along))
                {
                    return;
                }
            }
        }

        /// <summary>Orders the line dug; false when no builder offers the work or the player can't pay for it.</summary>
        private readonly bool TryDig(ref SystemState state, in AITurn turn, in AISnapshot snapshot, FieldWorkRole role,
            float2 centre, float2 along)
        {
            if (!TryPickDigger(ref state, turn, snapshot, role, centre, out var digger, out var prefab))
            {
                return false;
            }

            var entities = state.EntityManager;
            var stock = entities.GetBuffer<ResourceStock>(turn.Player);
            if (!turn.Buyer.CanBuy(entities, prefab, new Stockpile(stock)))
            {
                return false;
            }

            var half = along * (FieldWorkLength * 0.5f);
            var start = centre - half;
            var end = centre + half;
            turn.Commands.Add(new PlayerCommand
            {
                Type = CommandType.PlaceLine, Unit = digger, Prefab = prefab,
                Position = new float3(start.x, 0f, start.y), End = new float3(end.x, 0f, end.y),
            });
            turn.Busy.Add(digger);
            return true;
        }

        /// <summary>Whether the owner already has a work of <paramref name="role"/> (site or built) on the line's strip.</summary>
        private readonly bool HasFieldWork(byte owner, FieldWorkRole role, float2 centre, float2 along, float2 outward)
        {
            for (var i = 0; i < _works.Length; i++)
            {
                if (!_works.IsOwnedBy(i, owner) || _workData[i].Role != role)
                {
                    continue;
                }

                var offset = _works.Position(i).xz - centre;
                var onLine = math.abs(math.dot(offset, along)) <= FieldWorkLength * 0.5f + FieldWorkStrip;
                if (onLine && math.abs(math.dot(offset, outward)) <= FieldWorkStrip)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The idle builder nearest the line that offers a line-placed work of <paramref name="role"/> (not one still
        /// recovering), and the first such work on its build options.
        /// </summary>
        private readonly bool TryPickDigger(ref SystemState state, in AITurn turn, in AISnapshot snapshot,
            FieldWorkRole role, float2 centre, out Entity digger, out Entity prefab)
        {
            var entities = state.EntityManager;
            var builders = _builders.Builders;
            var nearest = Closest.None;
            prefab = Entity.Null;
            for (var i = 0; i < builders.Length; i++)
            {
                var builder = builders.Entities[i];
                var idle = !entities.IsComponentEnabled<ActiveOrder>(builder);
                if (!idle || !builders.IsOwnedBy(i, turn.Owner) || turn.Busy.Contains(builder)
                    || _squads.IsRecovering(ref state, turn, snapshot, builder))
                {
                    continue;
                }

                var option = FindFieldWork(entities, entities.GetBuffer<BuildOption>(builder), role);
                if (option != Entity.Null && nearest.Consider(i, builder, math.distancesq(builders.Position(i).xz, centre)))
                {
                    prefab = option;
                }
            }

            digger = nearest.Entity;
            return nearest.Found;
        }

        private static Entity FindFieldWork(EntityManager entities, DynamicBuffer<BuildOption> options, FieldWorkRole role)
        {
            foreach (var option in options)
            {
                var isWork = entities.HasComponent<AIFieldWork>(option.Prefab)
                             && entities.HasComponent<LinePlacement>(option.Prefab);
                if (isWork && entities.GetComponentData<AIFieldWork>(option.Prefab).Role == role)
                {
                    return option.Prefab;
                }
            }

            return Entity.Null;
        }
    }
}
