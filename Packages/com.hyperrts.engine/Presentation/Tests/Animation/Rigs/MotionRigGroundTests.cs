using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Rigs
{
    /// <summary>Road wheels meeting the ground and the track stations they set.</summary>
    public class MotionRigGroundTests : MotionRigFixture
    {
        [Test]
        public void GroundWheel_LiftsByTheBump_NoMoreThanItsTravel_AndASprocketStays()
        {
            AddBump(0.2f);
            var wheel = Rig(GroundWheel(0.3f), new MotionRigPart { Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f });

            Update(20);

            Assert.AreEqual(0.2f, Part(0).Lift, 1e-3f, "up by the bump");
            MathAssert.Near(new float3(0f, 0.2f, 0f), Pose(wheel).Position, 1e-3f);
            Assert.AreEqual(0f, Part(1).Lift, "a sprocket does not lift");

            Rig(GroundWheel(0.12f));
            Update(20);
            Assert.AreEqual(0.12f, Part(0).Lift, 1e-3f, "held to its travel");
        }

        [Test]
        public void TrackStations_InterpolateTheWheelLifts_AndFadeToNothingAtTheEnds()
        {
            var wheels = new NativeArray<TrackWheel>(2, Allocator.Temp);
            wheels[0] = new TrackWheel { Station = 2f, Lift = 0.1f };
            wheels[1] = new TrackWheel { Station = 5f, Lift = 0.04f };
            var track = new MotionRigTrack();

            Assert.IsTrue(track.Settle(wheels));

            MathAssert.Near(new float3(0f, 0.05f, 0.1f), track.Lift0.xyz, 1e-5f);
            Assert.AreEqual(0.08f, track.Lift0.w, 1e-5f, "toward the second wheel");
            MathAssert.Near(new float3(0.06f, 0.04f, 0.02f), track.Lift1.xyz, 1e-5f);
            Assert.AreEqual(0f, track.Lift1.w, 1e-5f, "the back end holds still with the idler");
            Assert.IsFalse(track.Settle(wheels), "nothing moved, nothing to write");

            wheels[0] = new TrackWheel { Station = 2f, Lift = 0.103f };
            Assert.IsFalse(track.Settle(wheels), "under 5 mm is not written");
            wheels.Dispose();
        }

        private static MotionRigPart GroundWheel(float travel) => new()
        {
            Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.5f, Travel = travel, LiftAxis = math.up(),
        };

        // A rise of the given height at the unit's cell.
        private void AddBump(float height)
        {
            var size = new int2(16, 16);
            var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
            heights[5 * size.x + 5] = height;
            var terrain = TerrainHeight.Create(heights, size, float2.zero, 2f, Allocator.Persistent);
            heights.Dispose();
            _em.AddComponentData(_em.CreateEntity(), terrain);
        }
    }
}
