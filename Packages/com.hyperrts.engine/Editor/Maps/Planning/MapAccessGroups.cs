using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// Props sharing one access track: same road class, each within the smaller <see cref="MapPaletteEntry.sharedAccessDistance"/>
    /// of itself and another in the group.
    /// </summary>
    public static class MapAccessGroups
    {
        /// <summary>The groups, in the order their first prop comes in <paramref name="props"/>.</summary>
        public static List<List<MapPlacement>> Group(IReadOnlyList<MapPlacement> props)
        {
            var groups = new List<List<MapPlacement>>();
            var grouped = new bool[props.Count];
            for (var i = 0; i < props.Count; i++)
            {
                if (grouped[i])
                {
                    continue;
                }

                grouped[i] = true;
                var group = new List<MapPlacement> { props[i] };
                for (var next = 0; next < group.Count; next++)
                {
                    Gather(props, group, grouped, group[next]);
                }

                groups.Add(group);
            }

            return groups;
        }

        /// <summary>Adds the ungrouped props within shared access distance of <paramref name="from"/> that ask for its road class.</summary>
        private static void Gather(IReadOnlyList<MapPlacement> props, List<MapPlacement> group, bool[] grouped,
            MapPlacement from)
        {
            for (var j = 0; j < props.Count; j++)
            {
                if (grouped[j] || props[j].Entry.access != from.Entry.access)
                {
                    continue;
                }

                var distance = Mathf.Min(from.Entry.sharedAccessDistance, props[j].Entry.sharedAccessDistance);
                if (distance > 0f && (props[j].Position - from.Position).magnitude <= distance)
                {
                    grouped[j] = true;
                    group.Add(props[j]);
                }
            }
        }
    }
}
