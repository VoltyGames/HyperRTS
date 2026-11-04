using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Squads;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Squad helpers on top of <see cref="TestWorld"/>.</summary>
    public static class SquadTestKit
    {
        public const float MemberHealth = 25f;

        /// <summary>A squad of members at <see cref="MemberHealth"/> each, firing the given mounts.</summary>
        public static Entity SpawnSquad(this TestWorld world, byte faction, float3 position, byte[] weapons,
            float speed = 5f)
        {
            var health = MemberHealth * weapons.Length;
            var squad = world.SpawnUnit(faction, position, speed: speed, maxHealth: health);
            SquadAuthoring.Add(world.Writer(squad), health, weapons);
            return squad;
        }

        public static Entity SpawnSquad(this TestWorld world, byte faction, float3 position, int size = 4,
            float speed = 5f) =>
            world.SpawnSquad(faction, position, new byte[size], speed);

        public static Entity SpawnArmedSquad(this TestWorld world, float3 position, params byte[] weapons)
        {
            var squad = world.SpawnSquad(1, position, weapons);
            world.AddWeapon(squad, range: 6f, damage: 10f, cooldown: 100f);
            return squad;
        }

        public static void AssertHealthIsMemberSum(this TestWorld world, Entity squad)
        {
            var members = world.Buffer<SquadMember>(squad);
            Assert.AreEqual(SquadMember.Total(members), world.GetHealth(squad), 1e-3f);
            Assert.AreEqual(SquadMember.TotalMax(members), world.Get<Health>(squad).Max, 1e-3f);
        }
    }
}
