using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Breakable parts: the part list, how many break for the health, and which one breaks next.</summary>
    public class BreakablePartsTests
    {
        private GameObject _house;

        [TearDown]
        public void TearDown()
        {
            if (_house != null)
            {
                Object.DestroyImmediate(_house);
            }
        }

        [Test]
        public void FindPartsByName_ListsEachIntactWithItsBrokenSibling()
        {
            var house = Building();
            var intact = Part(house.transform, "WallEast_Intact", new Vector3(4f, 1f, 0f));
            var broken = Part(house.transform, "WallEast_Broken", new Vector3(4f, 0.5f, 0f));
            Part(house.transform, "WallWest_Intact", new Vector3(-4f, 1f, 0f));

            house.FindPartsByName();

            Assert.AreEqual(2, house.parts.Length);
            Assert.AreSame(intact.gameObject, house.parts[0].intact, "hierarchy order");
            Assert.AreSame(broken.gameObject, house.parts[0].broken);
            Assert.IsNull(house.parts[1].broken, "a missing sibling stays empty for validation to report");
            StringAssert.Contains("needs both", house.Problems()[0]);
        }

        [Test]
        public void Placement_IsTheCentreAndOutwardInRootSpace()
        {
            var house = Building();
            Part(house.transform, "WallEast_Intact", new Vector3(4f, 1f, 0f));
            Part(house.transform, "WallEast_Broken", new Vector3(4f, 0.5f, 0f));
            Part(house.transform, "WallWest_Intact", new Vector3(-4f, 1f, 0f));
            Part(house.transform, "WallWest_Broken", new Vector3(-4f, 0.5f, 0f));
            Part(house.transform, "Roof_Intact", new Vector3(0f, 3f, 0f));
            Part(house.transform, "Roof_Broken", new Vector3(0f, 2.5f, 0f));
            house.FindPartsByName();

            var (centre, outward) = house.Placement(house.parts[0]);

            Assert.That(Vector3.Distance(new Vector3(4f, 1f, 0f), centre), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(Vector3.right, outward), Is.LessThan(1e-4f), "horizontal");
            Assert.AreEqual(Vector3.up, house.Placement(house.parts[2]).Outward, "a part over the middle points up");
        }

        [Test]
        public void Problems_NoParts_IsReported()
        {
            StringAssert.Contains("no parts", Building().Problems()[0]);
        }

        [Test]
        public void Problems_BrokenInsideItsIntact_IsReported()
        {
            var house = Building();
            var intact = Part(house.transform, "Door_Intact", Vector3.right);
            var broken = Part(intact, "Door_Broken", Vector3.zero);
            house.parts = new[] { new BreakablePartsAuthoring.Part { intact = intact.gameObject, broken = broken.gameObject } };

            StringAssert.Contains("would hide with it", house.Problems()[0]);
        }

        [TestCase(1f, 0)]
        [TestCase(0.5f, 4)]
        [TestCase(0f, 8)]
        [TestCase(0.95f, 0)]
        public void TargetBroken_IsRoundedShareOfMissingHealth(float health, int expected)
        {
            var building = new BreakableParts { BreakShare = 0.8f };

            Assert.AreEqual(expected, building.TargetBroken(10, health));
        }

        [Test]
        public void NextToBreak_IsTheIntactPartNearestTheLastHit()
        {
            var parts = Parts(new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f));
            var shown = new ShownBreakage { HasHit = true, LastHit = new float3(-5f, 0f, 1f) };

            Assert.AreEqual(1, shown.NextToBreak(parts));

            var west = parts[1];
            west.Order = 1;
            parts[1] = west;
            Assert.AreEqual(2, shown.NextToBreak(parts), "the broken one is skipped");
        }

        [Test]
        public void NextToBreak_WithoutAHit_FollowsBakeOrder()
        {
            var parts = Parts(new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f));
            var first = parts[0];
            first.Order = 1;
            parts[0] = first;

            Assert.AreEqual(1, new ShownBreakage { Broken = 1 }.NextToBreak(parts));
        }

        [Test]
        public void LatestBroken_IsTheLastInBreakOrder()
        {
            var parts = Parts(float3.zero, float3.zero, float3.zero);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                part.Order = i == 1 ? 0 : i == 0 ? 2 : 1;
                parts[i] = part;
            }

            Assert.AreEqual(0, new ShownBreakage { Broken = 2 }.LatestBroken(parts));
            Assert.AreEqual(-1, new ShownBreakage().LatestBroken(parts));
        }

        private static NativeArray<BreakablePart> Parts(params float3[] centres)
        {
            var parts = new NativeArray<BreakablePart>(centres.Length, Allocator.Temp);
            for (var i = 0; i < centres.Length; i++)
            {
                parts[i] = new BreakablePart { Centre = centres[i] };
            }

            return parts;
        }

        private BreakablePartsAuthoring Building()
        {
            _house = new GameObject("House");
            Part(_house.transform, "Frame", Vector3.zero);
            return _house.AddComponent<BreakablePartsAuthoring>();
        }

        private static Transform Part(Transform parent, string name, Vector3 at)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            part.name = name;
            part.SetParent(parent, false);
            part.localPosition = at;
            return part;
        }
    }
}
