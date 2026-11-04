using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>Dying figures: the fall, the blast fall from the cue direction, and hiding.</summary>
    public class SquadFigureDeathTests : SquadFigureFixture
    {
        [Test]
        public void DroppedFigure_PlaysDieThenHides()
        {
            Update();
            SetLiving(3);
            Update();

            Assert.AreEqual(FigureState.Die, State(3));
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));
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
            var clips = BlastClips();
            var facing = float4x4.identity;

            Assert.AreEqual(clips.DieBlastFront, clips.Blasted(new float3(0f, 0f, 5f), facing));
            Assert.AreEqual(clips.DieBlastBack, clips.Blasted(new float3(0f, 0f, -5f), facing));
            Assert.AreEqual(clips.DieBlastRight, clips.Blasted(new float3(5f, 0f, 1f), facing));
            Assert.AreEqual(clips.DieBlastLeft, clips.Blasted(new float3(-5f, 0f, 1f), facing));

            var turned = float4x4.RotateY(math.PI);
            Assert.AreEqual(clips.DieBlastBack, clips.Blasted(new float3(0f, 0f, 5f), turned));
        }

        [Test]
        public void FigureWithoutBlastClips_PlaysDie()
        {
            Update();
            Blast(new float3(0f, 0f, 3f));
            SetLiving(3);
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(40, false)));
        }

        [Test]
        public void DroppedFigure_FallsFromABlastNearTheSquad()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 3f));
            SetLiving(3);
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(50, false)));
        }

        [Test]
        public void FigureThatFellJustBeforeTheBlastCue_SwitchesToTheBlastFall()
        {
            SetBlastClips();
            Update();
            SetLiving(3);
            Update();
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));

            Blast(new float3(0f, 0f, -3f));
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(60, false)));
        }

        [Test]
        public void BlastTooOldOrTooFar_PlaysDie()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 30f));
            SetLiving(3);
            Update();
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));

            SetLiving(4);
            Update();
            Blast(new float3(0f, 0f, 3f));
            Advance(SquadFigures.BlastAfter + 0.2f);
            SetLiving(1);
            Update();

            Assert.IsTrue(Lower(2).Plays(Range(40, false)));
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
