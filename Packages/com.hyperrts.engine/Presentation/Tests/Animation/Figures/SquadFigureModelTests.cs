using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
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
    /// <summary>Figures that swap to an upgraded or emplaced model, and the deployed weapon model with its crew places.</summary>
    public class SquadFigureModelTests : SquadFigureFixture
    {
        [Test]
        public void MemberFiringItsUpgrade_ShowsTheUpgradedFigureAndTheCardNameFollows()
        {
            var upgraded = AddUpgradedModel(1, "RPG-7");
            Update();
            Assert.IsTrue(Drawn(1));
            Assert.IsFalse(DrawnModel(upgraded), "the upgraded figure waits hidden");

            Member(1, weapon: 1);
            Update();
            Assert.IsFalse(Drawn(1));
            Assert.IsTrue(DrawnModel(upgraded));
            Assert.AreEqual("RPG-7", Figure(1).Weapon.ToString());
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
            var buffer = _em.GetBuffer<SquadFigure>(_squad);
            buffer.ElementAt(0).Crew = new CrewPlace { Takes = true, Offset = new float2(0f, -0.6f), Posture = Posture.Prone };
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
        public void DeployedWeapon_ShowsItsModelAndSendsTheCrewToTheirPlacesOnlyWhileSetUp()
        {
            var gun = AddDeployedModel();
            var buffer = _em.GetBuffer<SquadFigure>(_squad);
            buffer.ElementAt(0).Crew = new CrewPlace { Takes = true, Offset = new float2(0f, -0.6f), Posture = Posture.Prone };
            _em.AddComponentData(_squad, new Deployable { State = DeployState.Packed });
            Member(0, offset: new float2(2f, 0f));
            Update();
            Advance(1f);
            Assert.IsFalse(DrawnModel(gun), "packed, the gun is stowed");
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(_figures[0]).Position.x, 0.05f);

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Deploying });
            Advance(2f);
            Assert.IsTrue(DrawnModel(gun));
            var place = _em.GetComponentData<LocalTransform>(_figures[0]).Position;
            Assert.AreEqual(0f, place.x, 0.05f, "not dragged back to its slot");
            Assert.AreEqual(-0.6f, place.z, 0.05f);
            Assert.IsTrue(Lower(0).Plays(Range(130, true)), "prone at the gun");

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Packing });
            Advance(2f);
            Assert.IsFalse(DrawnModel(gun));
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(_figures[0]).Position.x, 0.05f);
        }
    }
}
