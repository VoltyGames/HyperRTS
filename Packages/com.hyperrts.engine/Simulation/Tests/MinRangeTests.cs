using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Weapons with a minimum range skip close targets and back off from ordered ones.</summary>
    public class MinRangeTests
    {
        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test]
        public void TargetInsideMinRange_IsSkipped_ForOneBeyondIt()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var near = _world.SpawnUnit(2, new float3(2f, 0f, 0f));
            var far = _world.SpawnUnit(2, new float3(-6f, 0f, 0f));

            _world.Run(1f);

            Assert.AreEqual(far, _world.Get<AttackTarget>(gun).Value);
            Assert.AreEqual(100f, _world.GetHealth(near));
            Assert.Less(_world.GetHealth(far), 100f);
        }

        [Test]
        public void OrderedTargetInsideMinRange_BacksOffToMidRange_ThenFires()
        {
            var gun = _world.AddWeapon(_world.SpawnUnit(1, float3.zero), range: 8f, minRange: 3f, cooldown: 100f);
            var enemy = _world.SpawnUnit(2, new float3(2f, 0f, 0f));
            _world.Command(1, new PlayerCommand
            {
                Type = CommandType.Attack, Unit = gun, Target = enemy, Position = _world.GetPosition(enemy),
            });

            _world.Run(4f);

            // Both radii are 0.5, so a 5.5 edge gap, midway between 3 and 8, is 6.5 between centres.
            var apart = math.distance(_world.GetPosition(gun).xz, _world.GetPosition(enemy).xz);
            Assert.AreEqual(6.5f, apart, 0.75f, "backed off to about mid range");
            Assert.Less(_world.GetHealth(enemy), 100f, "fired once clear of the minimum range");
        }
    }
}
