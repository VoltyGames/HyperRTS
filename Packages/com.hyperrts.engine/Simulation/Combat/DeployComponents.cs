using Unity.Entities;
using Unity.Mathematics;
using Unity.Netcode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A unit that must deploy before its <see cref="Weapon.NeedsDeploy"/> weapons fire, and can't move until it packs
    /// up again. Replicated so client machines can follow <see cref="Progress"/>.
    /// </summary>
    public struct Deployable : IComponentData
    {
        public float DeployTime;
        public float PackUpTime;

        [GhostField] public DeployState State;

        /// <summary>0 packed up to 1 deployed, rising while deploying and falling while packing up.</summary>
        [GhostField(Quantization = 100)] public float Progress;

        /// <summary>Anything but packed up holds the unit in place.</summary>
        public readonly bool Immobile => State != DeployState.Packed;

        public readonly bool Deployed => State == DeployState.Deployed;

        /// <summary>Starts deploying from where it is; true when it was packed up or packing.</summary>
        public bool Deploy()
        {
            if (State is DeployState.Deploying or DeployState.Deployed)
            {
                return false;
            }

            State = DeployState.Deploying;
            return true;
        }

        public void PackUp()
        {
            if (State is DeployState.Deploying or DeployState.Deployed)
            {
                State = DeployState.Packing;
            }
        }

        public void Advance(float deltaTime)
        {
            if (State == DeployState.Deploying)
            {
                Progress = math.min(1f, Progress + deltaTime / DeployTime);
                State = Progress >= 1f ? DeployState.Deployed : State;
            }
            else if (State == DeployState.Packing)
            {
                Progress = math.max(0f, Progress - deltaTime / PackUpTime);
                State = Progress <= 0f ? DeployState.Packed : State;
            }
        }

        /// <summary>A weapon that needs deployment holds fire until its unit is deployed.</summary>
        public static bool Holds(in ComponentLookup<Deployable> lookup, Entity unit, in Weapon weapon)
        {
            if (!weapon.NeedsDeploy)
            {
                return false;
            }

            return !lookup.TryGetComponent(unit, out var deployable) || !deployable.Deployed;
        }

        public static bool IsImmobile(in ComponentLookup<Deployable> lookup, Entity unit) =>
            lookup.TryGetComponent(unit, out var deployable) && deployable.Immobile;
    }

    /// <summary>Where a <see cref="Deployable"/> unit is between packed up and deployed.</summary>
    public enum DeployState : byte
    {
        Packed = 0,
        Deploying = 1,
        Deployed = 2,
        Packing = 3,
    }
}
