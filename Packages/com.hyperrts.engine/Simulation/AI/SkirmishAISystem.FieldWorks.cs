using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Resources;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        // Metres: a line's length, the gap between cover lines (a trench is wide), and how far in front the barrier goes.
        private const float FieldWorkLength = 12f;
        private const float FieldWorkRowGap = 6f;
        private const float BarrierDistance = 5f;

        // Half the depth of the strip around a line's centre in which a work counts as that line.
        private const float FieldWorkStrip = 2.5f;

        /// <summary>
        /// Digs in at each of the slot's defence points: <see cref="AIPlayer.FieldWorkLines"/> cover lines across the
        /// approach, stepping back toward the base, and with <see cref="AIPlayer.FieldWorkBarrier"/> a barrier line
        /// in front. A line that already has a work on it is not laid again.
        /// </summary>
        private void BuildFieldWorks(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var ai = SystemAPI.GetComponent<AIPlayer>(turn.Player);
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
        private void DigRow(ref SystemState state, in Turn turn, in Snapshot snapshot, in AISlotHint slot,
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
                if (HasFieldWork(snapshot, turn.Owner, role, centre, along, outward))
                {
                    continue;
                }

                if (!TryPickDigger(ref state, turn, snapshot, role, centre, out var digger, out var prefab))
                {
                    return;
                }

                var stock = SystemAPI.GetBuffer<ResourceStock>(turn.Player);
                if (!PurchaseRules.CanBuy(state.EntityManager, prefab, turn.Buyer, stock.AsNativeArray()))
                {
                    return;
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
            }
        }

        /// <summary>Whether the owner already has a work of <paramref name="role"/> (site or built) on the line's strip.</summary>
        private static bool HasFieldWork(in Snapshot snapshot, byte owner, FieldWorkRole role, float2 centre,
            float2 along, float2 outward)
        {
            var works = snapshot.FieldWorks;
            for (var i = 0; i < works.Length; i++)
            {
                if (!works.IsOwnedBy(i, owner) || snapshot.FieldWorkData[i].Role != role)
                {
                    continue;
                }

                var offset = works.Position(i).xz - centre;
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
        private bool TryPickDigger(ref SystemState state, in Turn turn, in Snapshot snapshot, FieldWorkRole role,
            float2 centre, out Entity digger, out Entity prefab)
        {
            var builders = snapshot.Builders;
            var nearest = Closest.None;
            prefab = Entity.Null;
            for (var i = 0; i < builders.Length; i++)
            {
                var builder = builders.Entities[i];
                var idle = !SystemAPI.IsComponentEnabled<ActiveOrder>(builder);
                if (!idle || !builders.IsOwnedBy(i, turn.Owner) || turn.Busy.Contains(builder)
                    || IsRecovering(ref state, builder, turn, snapshot))
                {
                    continue;
                }

                var option = FindFieldWork(ref state, SystemAPI.GetBuffer<BuildOption>(builder), role);
                if (option != Entity.Null && nearest.Consider(i, builder, math.distancesq(builders.Position(i).xz, centre)))
                {
                    prefab = option;
                }
            }

            digger = nearest.Entity;
            return nearest.Found;
        }

        private Entity FindFieldWork(ref SystemState state, DynamicBuffer<BuildOption> options, FieldWorkRole role)
        {
            foreach (var option in options)
            {
                var isWork = SystemAPI.HasComponent<AIFieldWork>(option.Prefab)
                             && SystemAPI.HasComponent<LinePlacement>(option.Prefab);
                if (isWork && SystemAPI.GetComponent<AIFieldWork>(option.Prefab).Role == role)
                {
                    return option.Prefab;
                }
            }

            return Entity.Null;
        }
    }
}
