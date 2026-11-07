using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Read access to the <see cref="AIMapHints"/> singleton's three buffers: slots, lanes and their points.</summary>
    public readonly struct AIMapHintsView
    {
        private readonly DynamicBuffer<AISlotHint> _slots;
        private readonly DynamicBuffer<AILaneHint> _lanes;
        private readonly DynamicBuffer<AIHintPoint> _points;

        public AIMapHintsView(DynamicBuffer<AISlotHint> slots, DynamicBuffer<AILaneHint> lanes,
            DynamicBuffer<AIHintPoint> points)
        {
            _slots = slots;
            _lanes = lanes;
            _points = points;
        }

        public float2 Point(int index) => _points[index].Position;

        /// <summary>The <paramref name="index"/>th defence point (below <c>DefenceCount</c>) of a slot, nearest its base first.</summary>
        public float2 DefencePoint(in AISlotHint slot, int index) => _points[slot.DefenceStart + index].Position;

        public bool TryGetSlot(byte slot, out AISlotHint hint)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Slot == slot)
                {
                    hint = _slots[i];
                    return true;
                }
            }

            hint = default;
            return false;
        }

        /// <summary>The slots <paramref name="from"/> has at least one lane to, as a set of <see cref="Faction.Bit"/>s.</summary>
        public uint LaneEnemies(byte from)
        {
            var enemies = 0u;
            for (var i = 0; i < _lanes.Length; i++)
            {
                if (_lanes[i].From == from)
                {
                    enemies |= Faction.Bit(_lanes[i].To);
                }
            }

            return enemies;
        }

        /// <summary>How many distinct lanes lead from one slot's base to another's.</summary>
        public int LaneCount(byte from, byte to)
        {
            var count = 0;
            for (var i = 0; i < _lanes.Length; i++)
            {
                if (_lanes[i].From == from && _lanes[i].To == to)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The <paramref name="index"/>th lane (below <see cref="LaneCount"/>) from one slot to another, in baked order.</summary>
        public AILaneHint Lane(byte from, byte to, int index)
        {
            var seen = 0;
            for (var i = 0; i < _lanes.Length; i++)
            {
                if (_lanes[i].From != from || _lanes[i].To != to)
                {
                    continue;
                }

                if (seen == index)
                {
                    return _lanes[i];
                }

                seen++;
            }

            throw new System.InvalidOperationException("The AI map hints have no such lane.");
        }

        public float2 Waypoint(in AILaneHint lane, int index) => _points[lane.Start + index].Position;

        /// <summary>The waypoint of a lane nearest a position; the lane's <c>Count</c> when it has none.</summary>
        public int NearestWaypoint(in AILaneHint lane, float2 position)
        {
            var nearest = Closest.None;
            for (var k = 0; k < lane.Count; k++)
            {
                nearest.Consider(k, Entity.Null, math.distancesq(Waypoint(lane, k), position));
            }

            return nearest.Found ? nearest.Index : lane.Count;
        }
    }
}
