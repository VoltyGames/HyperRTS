using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Commands
{
    /// <summary>
    /// Box formation around a goal, facing the group's travel direction. Units keep their relative places:
    /// the front-most units take the front row, and each row is filled left to right. Each unit gets a slot its own
    /// size, and a row is as deep as its deepest member.
    /// </summary>
    public static class Formation
    {
        /// <summary>
        /// Writes one goal per unit into <paramref name="slots"/> (same index as its position), each taking its
        /// <paramref name="spacings"/> across (x) and along (y) the travel direction.
        /// </summary>
        public static void Assign(NativeArray<float3> positions, float3 goal, NativeArray<float2> spacings,
            NativeArray<float3> slots)
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
            var depth = 0f;
            for (var row = 0; row < rows; row++)
            {
                depth += RowDepth(order, row, columns, spacings);
            }

            var front = depth * 0.5f;
            for (var row = 0; row < rows; row++)
            {
                var start = row * columns;
                var members = order.GetSubArray(start, math.min(columns, count - start));
                var rowDepth = RowDepth(order, row, columns, spacings);
                var center = goal.xz + forward * (front - rowDepth * 0.5f);
                front -= rowDepth;
                PlaceRow(positions, members, keys, new float3(center.x, goal.y, center.y), right, spacings, slots);
            }

            order.Dispose();
            keys.Dispose();
        }

        /// <summary>How far apart the slots can lie: twice the farthest slot's distance from the goal.</summary>
        public static float Extent(NativeArray<float3> slots, float3 goal)
        {
            var farthest = 0f;
            foreach (var slot in slots)
            {
                farthest = math.max(farthest, math.distance(slot.xz, goal.xz));
            }

            return farthest * 2f;
        }

        private static float RowDepth(NativeArray<int> order, int row, int columns, NativeArray<float2> spacings)
        {
            var depth = 0f;
            var end = math.min(order.Length, (row + 1) * columns);
            for (var k = row * columns; k < end; k++)
            {
                depth = math.max(depth, spacings[order[k]].y);
            }

            return depth;
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
            float3 center, float2 right, NativeArray<float2> spacings, NativeArray<float3> slots)
        {
            var width = 0f;
            for (var k = 0; k < members.Length; k++)
            {
                keys[members[k]] = math.dot(positions[members[k]].xz, right);
                width += spacings[members[k]].x;
            }

            members.Sort(new ByKey { Keys = keys });
            var left = -width * 0.5f;
            for (var k = 0; k < members.Length; k++)
            {
                var span = spacings[members[k]].x;
                var slot = center.xz + right * (left + span * 0.5f);
                left += span;
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
