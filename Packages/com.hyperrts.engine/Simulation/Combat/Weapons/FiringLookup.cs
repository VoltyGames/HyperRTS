using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>
    /// Where a shooter fires from: its own place, or the port of the container it is garrisoned in, and the muzzle
    /// each round leaves from.
    /// </summary>
    public struct FiringLookup
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private BufferLookup<WeaponMuzzle> _muzzles;
        [ReadOnly] private ComponentLookup<Inside> _inside;
        [ReadOnly] private BufferLookup<FiringPort> _ports;

        public FiringLookup(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _muzzles = state.GetBufferLookup<WeaponMuzzle>(true);
            _inside = state.GetComponentLookup<Inside>(true);
            _ports = state.GetBufferLookup<FiringPort>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _muzzles.Update(ref state);
            _inside.Update(ref state);
            _ports.Update(ref state);
        }

        /// <summary>
        /// The shooter as it fires at <paramref name="target"/>; a garrisoned passenger fires from the port of its
        /// container that faces the target best.
        /// </summary>
        public Firing Start(Entity shooter, byte owner, byte poseIndex, in WeaponPose pose, float3 target)
        {
            var housing = _inside.HasEnabled(shooter) ? _inside[shooter].Container : shooter;
            var ported = TryFindPort(shooter, housing, target, out var port);
            return new Firing
            {
                Shooter = shooter,
                Owner = owner,
                PoseIndex = poseIndex,
                Place = ported ? port : _transforms[shooter],
                Pose = pose,
                Housing = housing,
                Ported = ported,
            };
        }

        /// <summary>Where the weapon's next round leaves from, taking its muzzles in turn.</summary>
        public ShotOrigin NextOrigin(in Firing firing, in Weapon weapon, ref WeaponCycle cycle)
        {
            var muzzle = weapon.MuzzleCount > 1 ? _muzzles[firing.Shooter][cycle.TakeMuzzle(weapon)].Point : weapon.Aim.Muzzle;
            return firing.Ported ? ShotOrigin.FromPort(firing.Place.Position) : weapon.Aim.Origin(firing.Place, firing.Pose, muzzle);
        }

        private bool TryFindPort(Entity shooter, Entity housing, float3 target, out LocalTransform port)
        {
            port = default;
            if (housing == shooter)
            {
                return false;
            }

            if (!_ports.TryGetBuffer(housing, out var ports) || !_transforms.TryGetComponent(housing, out var place))
            {
                return false;
            }

            return FiringPort.TryChoose(ports, place, target, out port);
        }
    }
}
