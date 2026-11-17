using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment.Ground.Craters
{
    /// <summary>The buildings the drawn terrain is levelled under, and the ground their changes touch.</summary>
    public sealed class BuildingLevels
    {
        private readonly Dictionary<Entity, BuildingLevel> _current = new();
        private readonly List<BuildingLevel> _list = new();

        public List<BuildingLevel> All => _list;

        /// <summary>Swaps in the wanted set and grows the XZ box to cover every building that came, went or changed; true when any did.</summary>
        public bool Replace(Dictionary<Entity, BuildingLevel> wanted, ref float2 min, ref float2 max)
        {
            var changed = false;
            foreach (var (entity, level) in _current)
            {
                if (!wanted.TryGetValue(entity, out var next) || !next.Equals(level))
                {
                    Cover(level, ref min, ref max);
                    changed = true;
                }
            }

            foreach (var (entity, level) in wanted)
            {
                if (!_current.TryGetValue(entity, out var last) || !last.Equals(level))
                {
                    Cover(level, ref min, ref max);
                    changed = true;
                }
            }

            _current.Clear();
            _list.Clear();
            foreach (var (entity, level) in wanted)
            {
                _current[entity] = level;
                _list.Add(level);
            }

            return changed;
        }

        public void Clear()
        {
            _current.Clear();
            _list.Clear();
        }

        private static void Cover(in BuildingLevel level, ref float2 min, ref float2 max)
        {
            min = math.min(min, level.Area.Center - level.Reach);
            max = math.max(max, level.Area.Center + level.Reach);
        }
    }
}
