using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
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
    /// <summary>Which clips the legs and upper body play for speed, posture, weapon shots and separate tables.</summary>
    public class SquadFigureClipTests : SquadFigureFixture
    {
        [Test]
        public void Figure_FiresOnlyOnItsOwnWeaponsShot()
        {
            _em.GetBuffer<WeaponPose>(_squad).Add(default);
            Member(1, weapon: 1);
            Update();

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(1).CountShot();
            Update();

            Assert.IsTrue(Plays(Upper(1), Range(30, false)));
            Assert.IsTrue(Plays(Upper(0), Range(0, true)));
        }

        [Test]
        public void Clip_FollowsSpeedThenFiresOnAShot()
        {
            Update();
            Assert.IsTrue(Plays(Lower(0), Range(0, true)));

            MoveFor(1f, 0.5f);
            Assert.IsTrue(Plays(Lower(0), Range(10, true)));

            MoveFor(5f, 0.5f);
            Assert.IsTrue(Plays(Lower(0), Range(20, true)));

            MoveFor(0f, 2f);
            Assert.IsTrue(Plays(Lower(0), Range(0, true)));

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();
            Assert.IsTrue(Plays(Lower(0), Range(0, true)), "the legs keep idling");
            Assert.IsTrue(Plays(Upper(0), Range(30, false)));

            Advance(FireLength);
            Assert.IsTrue(Plays(Upper(0), Range(0, true)));
        }

        [Test]
        public void FiringWhileWalking_FiresWithTheUpperBodyOverWalkingLegs()
        {
            Update();
            MoveFor(1f, 0.5f);
            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Move(1f);

            Assert.IsTrue(Plays(Lower(0), Range(10, true)));
            Assert.IsTrue(Plays(Upper(0), Range(30, false)));

            MoveFor(1f, FireLength + 0.1f);

            Assert.IsTrue(Plays(Upper(0), Range(10, true)));
            Assert.AreEqual(Lower(0).Value.w, Upper(0).Value.w, "the arms rejoin the stride in step");
        }

        [Test]
        public void Posture_PicksCrouchAndProneClipsForLegsAndFire()
        {
            Member(0, posture: Posture.Crouch);
            Member(1, posture: Posture.Prone);
            Update();

            Assert.IsTrue(Plays(Lower(0), Range(100, true)));
            Assert.IsTrue(Plays(Lower(1), Range(130, true)));

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();

            Assert.IsTrue(Plays(Upper(0), Range(120, false)));
            Assert.IsTrue(Plays(Lower(0), Range(100, true)));
        }

        [Test]
        public void FireClip_PlaysOncePerShotAndTheFigureKeepsFiringForItsLength()
        {
            Update();
            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();
            var first = Upper(0).Value.w;

            Advance(FireLength / 2f);
            Assert.IsTrue(Plays(Upper(0), Range(30, false)), "still firing before the clip ends");
            Assert.AreEqual(first, Upper(0).Value.w, "a clip that is not shot again plays on");

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();
            Assert.Greater(Upper(0).Value.w, first, "another shot restarts the clip");

            Advance(FireLength);
            Assert.IsTrue(Plays(Upper(0), Range(0, true)));
        }

        [Test]
        public void ProneFigure_CrawlsAndFiresWithTheProneClips()
        {
            Member(0, posture: Posture.Prone);
            Update();
            MoveFor(5f, 0.5f);

            Assert.IsTrue(Plays(Lower(0), Range(140, true)), "running prone crawls");

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Move(5f);
            Assert.IsTrue(Plays(Upper(0), Range(150, false)));
        }

        [Test]
        public void OnlyChangedFigures_GetANewClip()
        {
            Update();
            var kept = Lower(0).Value;
            SetLiving(3);
            Update();

            Assert.AreEqual(kept, Lower(0).Value);
            Assert.AreNotEqual(kept.w, Lower(3).Value.w);
        }

        [Test]
        public void LoopingFigures_StartOutOfStep()
        {
            Update();

            Assert.AreNotEqual(Lower(1).Value.w, Lower(2).Value.w);
        }

        [Test]
        public void MixedTableSquad_PlaysEachFiguresOwnClips()
        {
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(1).Clips = Clips(100);

            Update();
            Assert.IsTrue(Plays(Lower(0), Range(0, true)));
            Assert.IsTrue(Plays(Lower(1), Range(100, true)));

            MoveFor(5f, 0.5f);
            Assert.IsTrue(Plays(Lower(0), Range(20, true)));
            Assert.IsTrue(Plays(Lower(1), Range(120, true)));
        }
    }
}
