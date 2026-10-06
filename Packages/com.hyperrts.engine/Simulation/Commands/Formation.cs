using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// Box formation around a goal, facing the group's travel direction. Units keep their relative places:
    /// the front-most units take the front row, and each row is filled left to right.
    /// </summary>
    public static class Formation
    {
        /// <summary>Writes one goal per unit into <paramref name="slots"/> (same index as its position).</summary>
        public static void Assign(NativeArray<float3> positions, float3 goal, float spacing, NativeArray<float3> slots)
        {
            var count = positions.Length;
            if (count == 1)
            {
                slots[0] = goal;
                return;
            }

            var forward = TravelDirection(positions, goal);
            var right = new float2(forward.y, -forward.x);

            // About twice as wide as deep, like a battle line.
            var columns = math.min(count, (int)math.ceil(math.sqrt(count * 2f)));
            var rows = (count + columns - 1) / columns;

            var order = new NativeArray<int>(count, Allocator.Temp);
            var keys = new NativeArray<float>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                order[i] = i;
                keys[i] = -math.dot(positions[i].xz, forward);
            }

            order.Sort(new ByKey { Keys = keys });
            for (var row = 0; row < rows; row++)
            {
                var start = row * columns;
                var members = order.GetSubArray(start, math.min(columns, count - start));
                var center = goal.xz + forward * (((rows - 1) * 0.5f - row) * spacing);
                PlaceRow(positions, members, keys, new float3(center.x, goal.y, center.y), right, spacing, slots);
            }

            order.Dispose();
            keys.Dispose();
        }

        private static float2 TravelDirection(NativeArray<float3> positions, float3 goal)
        {
            var centroid = float2.zero;
            for (var i = 0; i < positions.Length; i++)
            {
                centroid += positions[i].xz;
            }

            return math.normalizesafe(goal.xz - centroid / positions.Length, new float2(0f, 1f));
        }

        private static void PlaceRow(NativeArray<float3> positions, NativeArray<int> members, NativeArray<float> keys,
            float3 center, float2 right, float spacing, NativeArray<float3> slots)
        {
            for (var k = 0; k < members.Length; k++)
            {
                keys[members[k]] = math.dot(positions[members[k]].xz, right);
            }

            members.Sort(new ByKey { Keys = keys });
            for (var k = 0; k < members.Length; k++)
            {
                var slot = center.xz + right * ((k - (members.Length - 1) * 0.5f) * spacing);
                slots[members[k]] = new float3(slot.x, center.y, slot.y);
            }
        }

        private struct ByKey : IComparer<int>
        {
            public NativeArray<float> Keys;

            public int Compare(int a, int b)
            {
                var byKey = Keys[a].CompareTo(Keys[b]);
                return byKey != 0 ? byKey : a.CompareTo(b);
            }
        }
    }
}
