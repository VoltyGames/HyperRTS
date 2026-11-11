using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
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
        public static Entity SpawnSquad(this TestWorld world, byte owner, float3 position, byte[] weapons,
            float speed = 5f)
        {
            var health = MemberHealth * weapons.Length;
            var squad = world.SpawnUnit(owner, position, new TestUnit { Speed = speed, MaxHealth = health });
            SquadAuthoring.Add(world.Writer(squad), health, weapons);
            return squad;
        }

        public static Entity SpawnSquad(this TestWorld world, byte owner, float3 position, int size = 4,
            float speed = 5f) =>
            world.SpawnSquad(owner, position, new byte[size], speed);

        public static Entity SpawnArmedSquad(this TestWorld world, float3 position, params byte[] weapons)
        {
            var squad = world.SpawnSquad(1, position, weapons);
            world.AddWeapon(squad, new TestWeapon { Range = 6f, Damage = 10f, Cooldown = 100f });
            return squad;
        }

        public static void HitSquad(this TestWorld world, Entity target) => world.HitSquad(target, new TestHit());

        /// <summary>Queues a hit on <paramref name="target"/>, or splashing round it, from a shooter of owner 2.</summary>
        public static void HitSquad(this TestWorld world, Entity target, TestHit hit) =>
            world.QueueDamage(new DamageEvent
            {
                Target = target,
                Position = hit.At,
                Origin = hit.At + hit.From,
                SourceOwner = 2,
                Layers = WeaponTargets.Surface,
                Profile = new DamageProfile
                {
                    Amount = hit.Amount, Radius = hit.Radius, EdgeFactor = hit.Edge, Suppression = hit.Suppression,
                    CoverPiercing = hit.Piercing,
                },
            });

        public static void AssertHealthIsMemberSum(this TestWorld world, Entity squad)
        {
            var members = world.Buffer<SquadMember>(squad);
            Assert.AreEqual(new SquadRoster(members).Total(), world.GetHealth(squad), 1e-3f);
            Assert.AreEqual(new SquadRoster(members).TotalMax(), world.Get<Health>(squad).Max, 1e-3f);
        }
    }
}
