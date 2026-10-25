using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Each unit of a pair closes at most half the room between their hulls, whatever it is pushed by.</summary>
    public class HullRoomTests
    {
        [Test]
        public void Move_TowardANeighbour_IsCutToHalfTheRoom()
        {
            var room = default(HullRoom);
            room.Add(new float2(-1f, 0f), 0.4f);
            var move = room.Within(new float2(0.5f, 0.1f));
            Assert.AreEqual(0.2f, move.x, 1e-5f);
            Assert.AreEqual(0.1f, move.y, 1e-5f, "the move along the neighbour stands");
        }

        [Test]
        public void Move_AwayOrAlongANeighbour_IsKept()
        {
            var room = default(HullRoom);
            room.Add(new float2(-1f, 0f), 0f);
            Assert.AreEqual(new float2(-0.3f, 0.2f), room.Within(new float2(-0.3f, 0.2f)));
        }

        [Test]
        public void Squeezed_BetweenTwoTouchingNeighbours_SlidesOutSideways()
        {
            var room = default(HullRoom);
            room.Add(new float2(-1f, 0f), 0f);
            room.Add(new float2(1f, 0f), 0f);
            var move = room.Within(new float2(0.15f, 0.05f));
            Assert.AreEqual(0f, move.x, 1e-5f);
            Assert.AreEqual(0.05f, move.y, 1e-5f);
        }

        [Test]
        public void AlreadyOverlapping_MayNotCloseFurther()
        {
            var room = default(HullRoom);
            room.Add(new float2(0f, 1f), -0.2f);
            Assert.IsFalse(room.Keeps(new float2(0f, -0.01f)));
            Assert.IsTrue(room.Keeps(new float2(0.1f, 0f)));
        }

        [Test]
        public void Corner_NoCutFits_Stands()
        {
            var room = default(HullRoom);
            room.Add(math.normalize(new float2(-1f, 1f)), 0f);
            room.Add(math.normalize(new float2(-1f, -1f)), 0f);
            var move = room.Within(new float2(0.2f, 0f));
            Assert.IsTrue(room.Keeps(move), $"cut to {move}");
            Assert.LessOrEqual(move.x, 1e-5f);
        }
    }
}
