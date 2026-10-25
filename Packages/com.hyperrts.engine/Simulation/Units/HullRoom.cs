using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// The room between one unit's hull and its nearest neighbours'. Each unit of a pair may close at most half of it
    /// in a frame, so the pair never meets however the crowd's pushes add up, and neither needs the other's move.
    /// </summary>
    public struct HullRoom
    {
        // A corner of half-planes that three projection passes can't settle stands still, which always fits.
        private const int Passes = 3;
        private const float Slack = 1e-5f;

        private FixedList128Bytes<Gap> _gaps;

        /// <summary>A neighbour whose hull lies <paramref name="room"/> metres off, <paramref name="away"/> from it.</summary>
        public void Add(float2 away, float room)
        {
            var gap = new Gap { Away = away, Half = math.max(0f, room) * 0.5f };
            if (_gaps.Length < _gaps.Capacity)
            {
                _gaps.Add(gap);
                return;
            }

            var widest = 0;
            for (var i = 1; i < _gaps.Length; i++)
            {
                widest = _gaps[i].Half > _gaps[widest].Half ? i : widest;
            }

            if (gap.Half < _gaps[widest].Half)
            {
                _gaps[widest] = gap;
            }
        }

        /// <summary>The move cut back onto every neighbour's half of the room, or standing when no cut fits all.</summary>
        public readonly float2 Within(float2 move)
        {
            for (var pass = 0; pass < Passes; pass++)
            {
                foreach (var gap in _gaps)
                {
                    move += gap.Away * math.max(0f, gap.Closing(move));
                }
            }

            return Keeps(move) ? move : float2.zero;
        }

        /// <summary>Whether the move closes on no neighbour by more than its half of the room.</summary>
        public readonly bool Keeps(float2 move)
        {
            foreach (var gap in _gaps)
            {
                if (gap.Closing(move) > Slack)
                {
                    return false;
                }
            }

            return true;
        }

        private struct Gap
        {
            public float2 Away;
            public float Half;

            // Hulls are convex, so a move shrinks the gap by no more than it closes along the way out.
            public readonly float Closing(float2 move) => -math.dot(move, Away) - Half;
        }
    }
}
