using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>A shooter at the moment one of its weapons fires: who, for whom, from where, and the pose that fired.</summary>
    public struct Firing
    {
        public Entity Shooter;
        public byte Owner;

        /// <summary>Index of the <see cref="WeaponPose"/> this weapon turns on.</summary>
        public byte PoseIndex;

        /// <summary>The shooter's place, or the firing port it shoots out of, turned to face the target.</summary>
        public LocalTransform Place;

        public WeaponPose Pose;

        /// <summary>The container a garrisoned passenger fires from, else the shooter; its own rounds never stop on it.</summary>
        public Entity Housing;

        /// <summary>Fires out of a port, so every round leaves from <see cref="Place"/>.</summary>
        public bool Ported;
    }
}
