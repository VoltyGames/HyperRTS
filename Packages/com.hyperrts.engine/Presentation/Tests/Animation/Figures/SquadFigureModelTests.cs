using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>Figures that swap to an upgraded or emplaced model, and the deployed weapon model with its crew places.</summary>
    public class SquadFigureModelTests : SquadFigureFixture
    {
        [Test]
        public void MemberFiringItsUpgrade_ShowsTheUpgradedFigure()
        {
            var upgraded = AddUpgradedModel(1);
            Update();
            Assert.IsTrue(Drawn(1));
            Assert.IsFalse(DrawnModel(upgraded), "the upgraded figure waits hidden");

            Member(1, weapon: 1);
            Update();
            Assert.IsFalse(Drawn(1));
            Assert.IsTrue(DrawnModel(upgraded));
            Assert.IsTrue(_em.GetComponentData<LowerClip>(upgraded).Clip.Plays(Range(1000, true)), "it plays its own table");

            Member(1, weapon: 0);
            Update();
            Assert.IsTrue(Drawn(1));
            Assert.IsFalse(DrawnModel(upgraded));
        }

        [Test]
        public void EmplacedFigure_ShowsOnlyWhileTheSquadIsSetUpAndSwapsBackOnPackUp()
        {
            var bare = AddEmplacedModel(0);
            _em.GetBuffer<SquadMember>(_squad).ElementAt(0).TakesCrewPlace = true;
            _em.AddComponentData(_squad, new Deployable { State = DeployState.Packed });
            Update();
            Assert.IsTrue(Drawn(0));
            Assert.IsFalse(DrawnModel(bare), "packed, the carried figure shows");

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Deploying });
            Advance(1f);
            Assert.IsFalse(Drawn(0));
            Assert.IsTrue(DrawnModel(bare));

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Packing });
            Advance(1f);
            Assert.IsTrue(Drawn(0));
            Assert.IsFalse(DrawnModel(bare));
        }

        [Test]
        public void DeployedWeapon_ShowsItsModelOnlyWhileSetUp()
        {
            var gun = AddDeployedModel();
            _em.AddComponentData(_squad, new Deployable { State = DeployState.Packed });
            Update();
            Assert.IsFalse(DrawnModel(gun), "packed, the gun is stowed");

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Deploying });
            Advance(1f);
            Assert.IsTrue(DrawnModel(gun));

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Packing });
            Advance(1f);
            Assert.IsFalse(DrawnModel(gun));
        }
    }
}
