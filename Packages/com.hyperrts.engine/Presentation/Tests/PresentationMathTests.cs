using HyperRTS.Presentation.Fog;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Presentation.Tests
{
    public class PresentationMathTests
    {
        [Test]
        public void FogTexels_ClearWhenVisible_DimWhenExplored_DarkOtherwise()
        {
            const byte team = 2;
            var mask = (ushort)(1 << team);
            using var visible = new NativeArray<ushort>(new ushort[] { mask, 0, 0, 1 << 1 }, Allocator.TempJob);
            using var explored = new NativeArray<ushort>(new ushort[] { mask, mask, 0, 1 << 1 }, Allocator.TempJob);
            using var output = new NativeArray<byte>(4, Allocator.TempJob);

            FogTexels.Fill(visible, explored, team, 128, 217, output);

            CollectionAssert.AreEqual(new byte[] { 0, 128, 217, 217 }, output.ToArray());
        }

        [Test]
        public void Minimap_RoundTripsAndPutsNorthAtTop()
        {
            var min = new float2(-100f, -50f);
            var size = new float2(200f, 100f);

            var topLeft = MinimapMath.WorldToMinimap(new float2(-100f, 50f), min, size);
            var back = MinimapMath.MinimapToWorld(new Vector2(0.25f, 0.75f), min, size);

            Assert.AreEqual(Vector2.zero, topLeft);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), MinimapMath.WorldToMinimap(back, min, size));
        }

        [Test]
        public void ViewGround_Reach_HitsGroundOrCapsAboveHorizon()
        {
            var eye = new float3(0f, 10f, 0f);
            var down = ViewGround.Reach(default, eye, math.normalize(new float3(0f, -1f, 1f)), 1000f);
            var up = ViewGround.Reach(default, eye, new float3(0f, 0f, 1f), 50f);

            Assert.That(math.distance(new float3(0f, 0f, 10f), down), Is.LessThan(1e-3f));
            Assert.That(math.distance(new float3(0f, 0f, 50f), up), Is.LessThan(1e-3f));
        }

        [Test]
        public void ViewGround_Focus_FallsBackBelowTheCamera()
        {
            var eye = new float3(4f, 10f, -2f);

            var focus = ViewGround.Focus(default, eye, new float3(0f, 0f, 1f), 1000f);

            Assert.That(math.distance(new float3(4f, 0f, -2f), focus), Is.LessThan(1e-3f));
        }

        [Test]
        public void RelationOf_ClassifiesOwnAllyEnemyNeutral()
        {
            var relations = new FactionRelations();
            relations.Teams.Add(0);
            relations.Teams.Add(1);
            relations.Teams.Add(1);
            relations.Teams.Add(2);

            Assert.AreEqual(Relation.Own, relations.RelationOf(1, 1));
            Assert.AreEqual(Relation.Ally, relations.RelationOf(1, 2));
            Assert.AreEqual(Relation.Enemy, relations.RelationOf(1, 3));
            Assert.AreEqual(Relation.Neutral, relations.RelationOf(1, Faction.Neutral));
        }
    }
}
