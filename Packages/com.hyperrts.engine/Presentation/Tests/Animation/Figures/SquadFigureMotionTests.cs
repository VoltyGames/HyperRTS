using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
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
    /// <summary>Where figures stand and face, and how their walk and run clips are timed.</summary>
    public class SquadFigureMotionTests : SquadFigureFixture
    {
        [Test]
        public void FigureStands_WhereItsMemberDoesInTheModelsFrame()
        {
            var turned = float4x4.RotateY(math.PI * 0.5f);
            _em.SetComponentData(_model, LocalTransform.FromRotation(quaternion.RotateY(math.PI * 0.5f)));
            Member(1, offset: new float2(2f, -1f));

            Update();

            var local = _em.GetComponentData<LocalTransform>(_figures[1]).Position;
            var world = math.transform(turned, local);
            Assert.AreEqual(2f, world.x, 1e-4f);
            Assert.AreEqual(-1f, world.z, 1e-4f);
        }

        [Test]
        public void SquadMovingAndTurning_TakesItsFiguresAlongInTheSameUpdate()
        {
            Member(1, offset: new float2(2f, -1f));
            Update();
            Advance(1f);

            MoveSquad(new float3(5f, 0f, 3f), math.PI * 0.5f);
            var before = Figure(1).Offset;
            Update();

            var squad = _em.GetComponentData<LocalToWorld>(_squad);
            var offset = Figure(1).Offset;
            var place = _em.GetComponentData<LocalToWorld>(_figures[1]);
            Assert.AreEqual(before.x, offset.x, 1e-3f, "settled on its member's offset");
            Assert.AreEqual(squad.Position.x + offset.x, place.Position.x, 1e-4f);
            Assert.AreEqual(squad.Position.z + offset.y, place.Position.z, 1e-4f);
        }

        [Test]
        public void SquadTurningAtOnce_TurnsItsFiguresOverSeveralUpdates()
        {
            Update();
            Advance(1f);
            var from = _em.GetComponentData<LocalToWorld>(_figures[1]).Forward;

            MoveSquad(float3.zero, math.PI);
            Update();
            var first = _em.GetComponentData<LocalToWorld>(_figures[1]).Forward;
            Assert.Less(math.degrees(math.acos(math.dot(from, first))), 150f, "not turned at once");
            Assert.Greater(math.dot(from, first), -0.99f);

            Advance(2f);
            var last = _em.GetComponentData<LocalToWorld>(_figures[1]).Forward;
            Assert.AreEqual(-1f, math.dot(from, last), 1e-2f, "faces the new way in the end");
        }

        [Test]
        public void MemberCatchingUp_RunsWhileItsSquadmatesStandIdle()
        {
            Update();
            Advance(SquadFigures.MoveHold);
            Member(2, offset: new float2(3f, 0f));
            Update();

            Assert.IsTrue(Plays(Lower(2), Range(20, true)));
            Assert.IsTrue(Plays(Lower(1), Range(0, true)));
        }

        [Test]
        public void SquadMovingInSteps_StaysInWalkWithoutSwitchingClips()
        {
            Update();
            for (var frame = 0; frame < 10; frame++)
            {
                Move(frame % 2 == 0 ? 3f : 0f);
            }

            var started = Figure(0).LowerStart;
            Assert.AreEqual(FigureState.Walk, State(0));
            for (var frame = 0; frame < 40; frame++)
            {
                Move(frame % 2 == 0 ? 3f : 0f);
                Assert.AreEqual(FigureState.Walk, State(0));
            }

            Assert.AreEqual(started, Figure(0).LowerStart, "the stride never restarted");

            var changes = 0;
            for (var frame = 0; frame < 30; frame++)
            {
                var before = State(0);
                Move(0f);
                changes += State(0) != before ? 1 : 0;
            }

            Assert.AreEqual(1, changes);
            Assert.AreEqual(FigureState.Idle, State(0));
        }

        [Test]
        public void FigureMovingAtHalfTheClipsGroundSpeed_PlaysItAtHalfRate()
        {
            GiveWalkGroundSpeed(2f);
            Update();
            MoveFor(1f, 3f);

            Assert.AreEqual(FigureState.Walk, State(0));
            Assert.AreEqual(5f, Lower(0).Value.z, 0.1f, "10 fps clip at half rate");
            Assert.AreEqual(5f, Upper(0).Value.z, 0.1f, "the arms in step with the legs");
        }

        [Test]
        public void StrideRateChange_KeepsTheFrameShowing()
        {
            GiveWalkGroundSpeed(2f);
            Update();
            MoveFor(1f, 3f);

            var changes = 0;
            for (var i = 0; i < 20; i++)
            {
                var before = Lower(0).Value;
                Move(1.5f);
                var after = Lower(0).Value;
                if (after.z != before.z)
                {
                    changes++;
                    Assert.AreEqual((float)_time - before.w, ((float)_time - after.w) * after.z / before.z, 1e-3f);
                }
            }

            Assert.Greater(changes, 0);
        }

        [Test]
        public void FiringFigure_TurnsToItsWeaponsAim_AndBackWhenItStops()
        {
            var figure = new SquadFigure { Baked = quaternion.identity, Facing = quaternion.identity, Firing = true };

            figure.Turn(quaternion.identity, new SquadMember(), math.PI * 0.5f, 0f);
            Assert.AreEqual(1f, math.mul(figure.Facing, math.forward()).x, 1e-4f, "a first update faces the aim at once");

            figure.Primed = true;
            figure.Firing = false;
            for (var i = 0; i < 60; i++)
            {
                figure.Turn(quaternion.identity, new SquadMember(), math.PI * 0.5f, 0.1f);
            }

            Assert.AreEqual(1f, math.mul(figure.Facing, math.forward()).z, 1e-3f, "done firing, it eases back to the squad's facing");
        }

        [Test]
        public void MemberInCover_TurnsToItsHeadingAndBackWhenItLeaves()
        {
            Update();
            ref var member = ref _em.GetBuffer<SquadMember>(_squad).ElementAt(0);
            member.Cover = CoverKind.Heavy;
            member.CoverHeading = 0;

            Advance(2f);
            var facing = math.mul(_em.GetComponentData<LocalTransform>(_figures[0]).Rotation, math.forward());
            Assert.AreEqual(1f, facing.x, 0.05f, "turned from the squad's Z to the heading along X");

            _em.GetBuffer<SquadMember>(_squad).ElementAt(0).Cover = CoverKind.None;
            Advance(2f);
            facing = math.mul(_em.GetComponentData<LocalTransform>(_figures[0]).Rotation, math.forward());
            Assert.AreEqual(1f, facing.z, 0.05f);
            Assert.AreEqual(0f, facing.x, 0.05f);
        }
    }
}
