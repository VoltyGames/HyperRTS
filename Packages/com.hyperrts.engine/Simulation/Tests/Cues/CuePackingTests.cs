using HyperRTS.Simulation.Cues.Announcements;
using HyperRTS.Simulation.Cues.Surfaces;
using System.IO;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Cues
{
    /// <summary>Cue events survive the wire and replay files.</summary>
    public class CuePackingTests
    {
        [Test]
        public void PackUnpack_KeepsTheEvent_WithinAPositionStep_AndItsDirectionWithinAStep()
        {
            var cue = new CueEvent
            {
                TypeId = 1234, Slot = CueSlot.Fire, Faction = 3, Position = new float3(1500.3f, 12.1f, -1800.7f),
                Direction = math.normalize(new float3(1f, 0.5f, -1f)), Stealthed = true,
            };

            var read = PackedCue.Pack(cue).Unpack();

            Assert.AreEqual(cue.TypeId, read.TypeId);
            Assert.AreEqual(cue.Slot, read.Slot);
            Assert.AreEqual(cue.Faction, read.Faction);
            Assert.That(math.distance(cue.Position, read.Position), Is.LessThan(QuantizedPosition.Step));
            Assert.That(math.dot(cue.Direction, read.Direction), Is.GreaterThan(0.999f));
            Assert.IsTrue(read.Stealthed);
            Assert.IsFalse(PackedCue.Pack(default).Unpack().Stealthed);
        }

        [Test]
        public void PackUnpack_KeepsAnImpactsRadiusSurfaceAndMaterial()
        {
            var impact = new CueEvent
            {
                TypeId = 1234, Slot = CueSlot.Impact, Position = new float3(3f, 0f, 4f), Radius = 2.5f, Faction = 3,
                Surface = ImpactSurface.Structure, Material = SurfaceMaterial.Concrete,
            };

            var read = PackedCue.Pack(impact).Unpack();

            Assert.AreEqual(impact.Radius, read.Radius, QuantizedPosition.Step);
            Assert.AreEqual(impact.Surface, read.Surface);
            Assert.AreEqual(impact.Material, read.Material);
            Assert.AreEqual(0, read.Variant, "impacts carry their material, not a variant");
        }

        [TestCase(CueSlot.Fire, 3)]
        [TestCase(CueSlot.Announce, AnnouncementKind.GameFirst + 2)]
        public void PackUnpack_KeepsTheVariant(CueSlot slot, int variant)
        {
            var read = PackedCue.Pack(new CueEvent { Slot = slot, Variant = (byte)variant }).Unpack();

            Assert.AreEqual(variant, read.Variant);
            Assert.AreEqual(SurfaceMaterial.None, read.Material);
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        public void PackUnpack_KeepsAxisDirections(float x, float y, float z)
        {
            var direction = new float3(x, y, z);

            var read = PackedCue.Pack(new CueEvent { Direction = direction }).Unpack();

            Assert.That(math.dot(direction, read.Direction), Is.GreaterThan(0.999f));
        }

        [Test]
        public void WriteRead_RoundTripsIn18Bytes()
        {
            var packed = PackedCue.Pack(new CueEvent
            {
                TypeId = 77, Slot = CueSlot.Death, Faction = 2, Position = new float3(-3f, 1f, 9f), Radius = 4f,
                Direction = new float3(0f, 0f, 1f), Surface = ImpactSurface.Water, Material = SurfaceMaterial.Water,
            });
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            packed.Write(writer);
            stream.Position = 0;

            Assert.AreEqual(18, stream.Length);
            Assert.AreEqual(packed, PackedCue.Read(new BinaryReader(stream)));
        }
    }
}
