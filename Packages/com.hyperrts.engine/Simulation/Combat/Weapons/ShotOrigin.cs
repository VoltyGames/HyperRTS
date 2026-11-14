using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Where one round leaves from and where its hit counts from for directional armor, in world space.</summary>
    public readonly struct ShotOrigin
    {
        public readonly float3 Launch;
        public readonly float3 Hit;

        /// <summary>It leaves from a modelled point, a muzzle or a firing port, where the fire cue plays.</summary>
        public readonly bool AtMuzzle;

        public ShotOrigin(float3 launch, float3 hit, bool atMuzzle)
        {
            Launch = launch;
            Hit = hit;
            AtMuzzle = atMuzzle;
        }

        /// <summary>A round fired out of a container's port leaves from the port itself.</summary>
        public static ShotOrigin FromPort(float3 port) => new(port, port, true);
    }
}
