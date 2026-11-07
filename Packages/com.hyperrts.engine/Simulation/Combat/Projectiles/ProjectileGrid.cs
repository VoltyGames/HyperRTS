using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Projectiles
{
    /// <summary>
    /// The projectiles an interceptor could shoot, filed by map cell once per frame so each looks only at the cells
    /// round it. A projectile files where it is and where it is headed, so a turret tracks a shot before it arrives.
    /// </summary>
    public struct ProjectileGrid
    {
        private const float CellSize = 16f;

        private NativeParallelMultiHashMap<int2, Entry> _cells;

        public ProjectileGrid(int projectileCount, AllocatorManager.AllocatorHandle allocator)
        {
            _cells = new NativeParallelMultiHashMap<int2, Entry>(math.max(1, projectileCount) * 2, allocator);
        }

        /// <summary>Files a projectile that interceptors may engage; a downed one, or one of no kind, is left out.</summary>
        public void Add(Entity entity, in Projectile projectile, float3 position)
        {
            if (projectile.Kind == ProjectileKind.None || projectile.Intercepted)
            {
                return;
            }

            var entry = new Entry
            {
                Entity = entity,
                Position = position,
                Aim = projectile.Hit.Position,
                Kind = projectile.Kind,
                Faction = projectile.Hit.SourceFaction,
            };
            var here = Cell(position);
            var there = Cell(entry.Aim);
            _cells.Add(here, entry);
            if (!there.Equals(here))
            {
                _cells.Add(there, entry);
            }
        }

        /// <summary>
        /// The nearest hostile projectile of a kind <paramref name="weapon"/> engages inside its range, else the nearest
        /// one aimed inside it; <c>Entity.Null</c> for none.
        /// </summary>
        public readonly Entity Nearest(float3 center, in Weapon weapon, byte faction, in FactionRelations relations)
        {
            var radiusSq = weapon.Range * weapon.Range;
            var near = Closest.Within(radiusSq);
            var inbound = Closest.None;
            var min = Cell(center - weapon.Range);
            var max = Cell(center + weapon.Range);
            foreach (var cell in new CellRange(min, max))
            {
                foreach (var entry in _cells.GetValuesForKey(cell))
                {
                    if (!weapon.Engages(entry.Kind) || !relations.IsHostile(faction, entry.Faction))
                    {
                        continue;
                    }

                    near.Consider(0, entry.Entity, math.distancesq(entry.Position, center));
                    if (math.distancesq(entry.Aim, center) <= radiusSq)
                    {
                        inbound.Consider(0, entry.Entity, math.distancesq(entry.Position, center));
                    }
                }
            }

            return near.Found ? near.Entity : inbound.Entity;
        }

        private static int2 Cell(float3 position) => (int2)math.floor(position.xz / CellSize);

        private struct Entry
        {
            public Entity Entity;
            public float3 Position;
            public float3 Aim;
            public ProjectileKind Kind;
            public byte Faction;
        }
    }
}
