using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using UnityEngine;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Bot hints being assembled: slots and lanes appended with their points, flattened into the three buffers of
    /// the <see cref="AIMapHints"/> singleton by <see cref="AIMapHintsAuthoring.Add{TWriter}"/>.
    /// </summary>
    public sealed class AIMapHintsData
    {
        public List<AISlotHint> Slots { get; } = new();

        public List<AILaneHint> Lanes { get; } = new();

        public List<AIHintPoint> Points { get; } = new();

        /// <summary>A slot's expansion order (nearest first) and defence points (nearest its base first).</summary>
        public void AddSlot(int slot, IReadOnlyList<Vector2> expansions, IReadOnlyList<Vector2> defence)
        {
            CheckSlot(slot, "slot");
            if (Slots.Exists(s => s.Slot == slot))
            {
                throw new ArgumentException($"The AI map hints list slot {slot} twice.");
            }

            var hint = new AISlotHint { Slot = (byte)slot };
            hint.ExpansionStart = Append(expansions, out hint.ExpansionCount);
            hint.DefenceStart = Append(defence, out hint.DefenceCount);
            Slots.Add(hint);
        }

        /// <summary>One lane from <paramref name="from"/>'s base to <paramref name="to"/>'s; add the reverse lane too.</summary>
        public void AddLane(int from, int to, IReadOnlyList<Vector2> waypoints)
        {
            CheckSlot(from, "lane start");
            CheckSlot(to, "lane end");
            if (from == to)
            {
                throw new ArgumentException($"An AI lane from slot {from} to itself is not a lane.");
            }

            var lane = new AILaneHint { From = (byte)from, To = (byte)to };
            lane.Start = Append(waypoints, out lane.Count);
            Lanes.Add(lane);
        }

        private static void CheckSlot(int slot, string what)
        {
            if (slot < 1 || slot > Faction.Max)
            {
                throw new ArgumentOutOfRangeException(nameof(slot),
                    $"The AI map hints have a {what} {slot}; slots run from 1 to {Faction.Max}.");
            }
        }

        private ushort Append(IReadOnlyList<Vector2> points, out ushort count)
        {
            var start = Points.Count;
            if (start + points.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException(
                    $"The AI map hints hold more than {ushort.MaxValue} points; ranges would overflow.");
            }

            foreach (var point in points)
            {
                Points.Add(new AIHintPoint { Position = point });
            }

            count = (ushort)points.Count;
            return (ushort)start;
        }
    }
}
