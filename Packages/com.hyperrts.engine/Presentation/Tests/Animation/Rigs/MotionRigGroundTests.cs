using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Rigs;

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

    [Test]
    public void TrackTravel_GrowsDrivingForward_ShrinksInReverse_AndStaysWhenParked()
    {
        RigWithTracks();
        Update(1);

        MoveTo(_position + new float3(0f, 0f, 1f));
        Assert.AreEqual(1f, Travel(0), 1e-3f, "forward runs the belt");
        Assert.AreEqual(1f, Travel(1), 1e-3f, "both sides alike");

        Update(3);
        Assert.AreEqual(1f, Travel(0), 1e-3f, "parked keeps its travel");
        Assert.IsFalse(Track(0).Scrolled, "nothing to write to the mesh");

        MoveTo(_position + new float3(0f, 0f, 0.6f));
        Assert.AreEqual(0.6f, Travel(0), 1e-3f, "reverse runs it back");
    }

    [Test]
    public void TrackTravel_PivotingRunsTheTwoSidesOppositeWays()
    {
        RigWithTracks();
        Update(1);

        _em.SetComponentData(_unit, LocalTransform.FromPositionRotation(_position, quaternion.RotateY(-0.2f)));
        Update(1);

        Assert.AreEqual(0.2f, Travel(1), 1e-3f, "a left turn runs the right track forward");
        Assert.AreEqual(-0.2f, Travel(0), 1e-3f, "and the left track back");
    }

    // Track 0 on the left and track 1 on the right, 1 m from the centre.
    private void RigWithTracks()
    {
        Rig(GroundWheel(0f));
        var tracks = new[] { new MotionRigTrack { Side = -1f }, new MotionRigTrack { Side = 1f } };
        MotionRigAuthoring.AddTracks(new EntityWriter(_em, _unit), tracks, new TrackWheel[0]);
    }

    private MotionRigTrack Track(int index) => _em.GetBuffer<MotionRigTrack>(_unit)[index];

    private float Travel(int index) => Track(index).Travel;

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
        _kit.SetTerrain(heights, size);
        heights.Dispose();
    }
}
