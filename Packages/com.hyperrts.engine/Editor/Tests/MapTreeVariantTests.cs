using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class MapTreeVariantTests
    {
        [TestCase(MapSymmetry.Rotate180)]
        [TestCase(MapSymmetry.MirrorXName)]
        [TestCase("rotate-4")]
        public void EveryCopy_GetsTheSameVariant(string symmetry)
        {
            var species = Species(3);
            var planting = new MapTreePlanting(MapSymmetry.Parse(symmetry), Climate(species), new MapTreeSpacing(null, null, null));

            for (var i = 0; i < 40; i++)
            {
                planting.PlantCopies(0, new Vector2(i * 7.3f, -i * 3.1f), 1f, 0f);
            }

            var copies = MapSymmetry.Parse(symmetry).Copies.Count;
            for (var i = 0; i < planting.Planted.Count; i += copies)
            {
                var variants = planting.Planted.Skip(i).Take(copies).Select(tree => species.Variant(tree.Seed));
                Assert.AreEqual(1, variants.Distinct().Count(), $"copies of tree {i / copies}");
            }
        }

        [Test]
        public void Seeds_AreTheSameEveryRun()
        {
            var point = new Vector2(-123.456f, 78.9f);

            Assert.AreEqual(MapTree.SeedAt(point), MapTree.SeedAt(new Vector2(-123.456f, 78.9f)));
            Assert.AreNotEqual(MapTree.SeedAt(point), MapTree.SeedAt(point + Vector2.right));
        }

        [Test]
        public void Variants_AreSpreadEvenly()
        {
            var species = Species(3);
            var counts = new int[3];
            for (var x = 0; x < 30; x++)
            {
                for (var z = 0; z < 30; z++)
                {
                    counts[species.Variant(MapTree.SeedAt(new Vector2(x * 4.5f, z * 4.5f)))]++;
                }
            }

            Assert.IsTrue(counts.All(count => count > 900 / 3 * 0.8f), $"counts {string.Join(", ", counts)}");
        }

        [Test]
        public void ATreeWithoutPrefabs_CannotPickAVariant()
        {
            var species = Species(0);

            var error = Assert.Throws<InvalidOperationException>(() => species.Variant(1u));

            StringAssert.Contains("'Oak'", error.Message);
        }

        private static MapClimateTree Species(int variants) =>
            new() { key = "Oak", prefabs = new List<GameObject>(new GameObject[variants]) };

        private static MapClimate Climate(MapClimateTree species)
        {
            var climate = new MapClimate();
            climate.trees.Add(species);
            return climate;
        }
    }
}
