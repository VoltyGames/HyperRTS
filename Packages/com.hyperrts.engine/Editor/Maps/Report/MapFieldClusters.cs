using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// Groups resource stacks into fields: stacks within <see cref="Link"/> metres of each other (directly or through a chain
    /// of stacks) are one field, the way a player sees a cluster of crates as one place to send trucks.
    /// </summary>
    public static class MapFieldClusters
    {
        /// <summary>How close two stacks must be to count as one field (metres).</summary>
        public const float Link = 20f;

        public static int Count(IReadOnlyList<Vector2> stacks)
        {
            var field = new int[stacks.Count];
            for (var i = 0; i < field.Length; i++)
            {
                field[i] = -1;
            }

            var fields = 0;
            for (var i = 0; i < stacks.Count; i++)
            {
                if (field[i] >= 0)
                {
                    continue;
                }

                Fill(stacks, field, i, fields);
                fields++;
            }

            return fields;
        }

        private static void Fill(IReadOnlyList<Vector2> stacks, int[] field, int start, int id)
        {
            var open = new Stack<int>();
            field[start] = id;
            open.Push(start);
            while (open.Count > 0)
            {
                var at = stacks[open.Pop()];
                for (var j = 0; j < stacks.Count; j++)
                {
                    var joined = field[j] < 0 && (stacks[j] - at).magnitude <= Link;
                    if (!joined)
                    {
                        continue;
                    }

                    field[j] = id;
                    open.Push(j);
                }
            }
        }
    }
}
