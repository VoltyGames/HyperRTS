using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Spatial;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Targeting
{
    /// <summary>
    /// Finds the closest hostile a weapon can hit: in range and firing arc, spotted by the shooter's team and, for direct fire, in clear line.
    /// </summary>
    public struct NearestHostile : ISpatialVisitor
    {
        public Entity Best;
        public float BestDistance;

        private OwnerRelations _relations;
        private TargetLookup _targets;
        private Weapon _weapon;
        private byte _owner;
        private byte _team;
        private float3 _center;
        private float _radius;
        private Entity _shooter;
        private bool _inPlace;

        public NearestHostile(in OwnerRelations relations, in TargetLookup targets, in Weapon weapon, byte owner,
            Entity shooter, float3 center)
        {
            _shooter = shooter;
            _inPlace = false;
            Best = Entity.Null;
            BestDistance = float.MaxValue;
            _relations = relations;
            _targets = targets;
            _weapon = weapon;
            _owner = owner;
            _team = relations.GetTeam(owner);
            _center = center;
            _radius = targets.Radius(shooter);
        }

        /// <summary>Visits what is within <paramref name="range"/> of the shooter's edge; <c>inPlace</c> as in the type summary.</summary>
        public void Search(in SpatialIndex index, float range, bool inPlace)
        {
            _inPlace = inPlace;
            index.Query(_center, range + _radius, ref this);
        }

        public void Visit(in SpatialEntry entry)
        {
            if (!_relations.IsHostile(_owner, entry.Owner) || !Weapon.CanHit(_weapon.Targets, entry.Layer))
            {
                return;
            }

            if (!_targets.IsSpotted(entry.Entity, entry.Position, _team) || !_targets.Affects(entry.Entity, _weapon))
            {
                return;
            }

            if (_weapon.FiringArc > 0f && !_targets.InFiringArc(_shooter, _weapon, entry.Entity))
            {
                return;
            }

            var distance = math.distance(entry.Position.xz, _center.xz) - entry.Radius;
            if (distance - _radius < _weapon.MinRange)
            {
                return;
            }

            // Last, as it marches a line: a blocked direct-fire target isn't picked at all.
            var closer = Closest.IsCloser(distance, entry.Entity, BestDistance, Best);
            var reached = !_inPlace || _targets.Reaches(_shooter, _center, _weapon, entry.Entity);
            if (closer && reached && _targets.HasLineOfFire(_shooter, _center, _weapon, entry.Entity))
            {
                Best = entry.Entity;
                BestDistance = distance;
            }
        }
    }
}
