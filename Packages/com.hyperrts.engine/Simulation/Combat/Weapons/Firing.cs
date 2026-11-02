using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>A shooter at the moment one of its weapons fires: who, for whom, from where, and the pose that fired.</summary>
    public readonly struct Firing
    {
        public readonly Entity Shooter;
        public readonly byte Faction;

        /// <summary>Index of the <see cref="WeaponPose"/> this weapon turns on.</summary>
        public readonly byte PoseIndex;

        public readonly LocalTransform Place;
        public readonly WeaponPose Pose;

        public Firing(Entity shooter, byte faction, byte poseIndex, in LocalTransform place, in WeaponPose pose)
        {
            Shooter = shooter;
            Faction = faction;
            PoseIndex = poseIndex;
            Place = place;
            Pose = pose;
        }
    }
}
