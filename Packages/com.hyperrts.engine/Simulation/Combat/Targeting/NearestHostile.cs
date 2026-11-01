using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Finds the closest hostile a weapon can hit, worth its damage (<see cref="Weapon.MinEffect"/>), beyond its
    /// <see cref="Weapon.MinRange"/> and spotted by the shooter's team.
    /// </summary>
    public struct NearestHostile : ISpatialVisitor
    {
        public Entity Best;
        public float BestDistance;

        private FactionRelations _relations;
        private TargetLookup _targets;
        private Weapon _weapon;
        private byte _faction;
        private byte _team;
        private float3 _center;
        private float _radius;

        public NearestHostile(in FactionRelations relations, in TargetLookup targets, in Weapon weapon, byte faction,
            float3 center, float radius)
        {
            Best = Entity.Null;
            BestDistance = float.MaxValue;
            _relations = relations;
            _targets = targets;
            _weapon = weapon;
            _faction = faction;
            _team = relations.GetTeam(faction);
            _center = center;
            _radius = radius;
        }

        public void Visit(in SpatialEntry entry)
        {
            if (!_relations.IsHostile(_faction, entry.Faction) || !Weapon.CanHit(_weapon.Targets, entry.Layer))
            {
                return;
            }

            if (!_targets.IsSpotted(entry.Entity, entry.Position, _team) || !_targets.Affects(entry.Entity, _weapon))
            {
                return;
            }

            var distance = math.distance(entry.Position.xz, _center.xz) - entry.Radius;
            if (distance - _radius < _weapon.MinRange)
            {
                return;
            }

            if (Closest.IsCloser(distance, entry.Entity, BestDistance, Best))
            {
                Best = entry.Entity;
                BestDistance = distance;
            }
        }
    }
}
