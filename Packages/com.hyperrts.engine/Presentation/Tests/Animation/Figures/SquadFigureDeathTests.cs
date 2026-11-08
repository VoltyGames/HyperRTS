using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>Dying figures: the fall, the blast fall from the member's death cause, and hiding.</summary>
    public class SquadFigureDeathTests : SquadFigureFixture
    {
        [Test]
        public void DroppedFigure_PlaysDieThenHides()
        {
            Update();
            SetLiving(3);
            Update();

            Assert.AreEqual(FigureState.Die, State(3));
            Assert.IsTrue(Plays(Lower(3), Range(40, false)));
            Assert.IsTrue(Drawn(3));

            Advance(DieHold);

            Assert.AreEqual(FigureState.Hidden, State(3));
            Assert.IsFalse(Drawn(3));
            Assert.AreEqual(FigureState.Idle, State(2));
            Assert.IsTrue(Drawn(2));
        }

        [Test]
        public void BlastDeath_PicksTheClipForWhereTheBlastCameFrom()
        {
            var clips = Clips(0);
            var facing = float4x4.identity;
            var blast = DeathCause.Blast;

            Assert.AreEqual(Range(50, false), clips.Value.Fall(Posture.Stand, blast, new float3(0f, 0f, 5f), facing));
            Assert.AreEqual(Range(60, false), clips.Value.Fall(Posture.Stand, blast, new float3(0f, 0f, -5f), facing));
            Assert.AreEqual(Range(80, false), clips.Value.Fall(Posture.Stand, blast, new float3(5f, 0f, 1f), facing));
            Assert.AreEqual(Range(70, false), clips.Value.Fall(Posture.Stand, blast, new float3(-5f, 0f, 1f), facing));

            var turned = float4x4.RotateY(math.PI);
            Assert.AreEqual(Range(60, false), clips.Value.Fall(Posture.Stand, blast, new float3(0f, 0f, 5f), turned));
        }

        [Test]
        public void CrouchedAndProneMembers_FallInTheirOwnPostureWhateverDroppedThem()
        {
            Member(1, posture: Posture.Crouch);
            Member(2, posture: Posture.Prone);
            Member(3, posture: Posture.Prone);
            Update();
            SetDeath(2, DeathCause.Shot);
            SetDeath(3, DeathCause.Blast, new float2(0f, 1f));
            SetLiving(1);
            Update();

            Assert.IsTrue(Plays(Lower(1), Range(170, false)));
            Assert.IsTrue(Plays(Lower(2), Range(160, false)));
            Assert.IsTrue(Plays(Lower(3), Range(160, false)));
        }

        [Test]
        public void SpawnWithDeadMembers_HidesTheirFiguresWithoutFalling()
        {
            SetLiving(2);
            Update();

            Assert.AreEqual(FigureState.Idle, State(1));
            Assert.AreEqual(FigureState.Hidden, State(2));
            Assert.IsFalse(Drawn(3));
        }
    }
}
