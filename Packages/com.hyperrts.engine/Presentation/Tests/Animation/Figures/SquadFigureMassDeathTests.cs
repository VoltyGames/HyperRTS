using System.Diagnostics;
using System.Linq;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Cues;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>
    /// A blast that wipes 300 squads of four five-part figures at once: the corpses come a frame's budget at a time,
    /// nearest the camera first, and no frame after it costs 4 ms.
    /// </summary>
    public class SquadFigureMassDeathTests : SquadFigureFixture
    {
        private const int Squads = 300;

        private Entity[] _squads;

        [SetUp]
        public void AddSquads()
        {
            AddParts(4);
            UseRagdolls();
            // In the group, so each copy's parts hang under its own model.
            _em.GetBuffer<LinkedEntityGroup>(_squad).Add(_model);
            var prefab = _em.Instantiate(_squad);
            using var parts = _em.GetBuffer<LinkedEntityGroup>(prefab).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            _em.AddComponent<Prefab>(parts);
            _squads = new Entity[Squads + 1];
            for (var i = 0; i < _squads.Length; i++)
            {
                _squads[i] = _em.Instantiate(_squad);
                var spot = new float3(i % 20 * 6f - 57f, 0f, i / 20 * 6f);
                _em.SetComponentData(_squads[i], LocalTransform.FromPosition(spot));
            }

            _em.DestroyEntity(_squad);
            _kit.AddRegistry();
            _kit.AddCues();
        }

        [Test]
        public void ThreeHundredSquadsWiped_CorpsesComeABudgetAFrameNearestFirst()
        {
            Update();
            // One squad first, so the frames timed below run warm code.
            Wipe(0, 1);
            Advance(1f);
            var nearest = NearestDistances();

            var slowest = Wipe(1, Squads);
            var budget = EffectDetail.CorpsesPerFrameAt(PresentationQuality.Current.Effects);
            var made = NewCorpses();
            Assert.AreEqual(budget, made.Length, "one frame's budget of corpses");
            foreach (var corpse in made)
            {
                var distance = Distance(_em.GetComponentData<Corpse>(corpse).World.c3.xyz);
                Assert.LessOrEqual(distance, nearest[budget - 1] + 0.5f, "nearest the camera first");
            }

            for (var frame = 0; frame < 15; frame++)
            {
                slowest = math.max(slowest, TimedUpdate());
            }

            UnityEngine.Debug.Log($"[MassDeath] {Squads} squads wiped: slowest frame after {slowest:F3} ms");
        }

        /// <summary>Kills squads first to last at once, as one blast, and returns the cost of the frame after.</summary>
        private double Wipe(int first, int last)
        {
            for (var i = first; i < last; i++)
            {
                var spot = _em.GetComponentData<LocalTransform>(_squads[i]).Position;
                _em.DestroyEntity(_squads[i]);
                _kit.PushCue(new CueEvent { TypeId = SquadType, Slot = CueSlot.Death, Position = spot });
            }

            return TimedUpdate();
        }

        private double TimedUpdate()
        {
            var watch = Stopwatch.StartNew();
            Update();
            watch.Stop();
            _kit.ClearCues();
            return watch.Elapsed.TotalMilliseconds;
        }

        // The squads not yet wiped, nearest the camera first; a squad's figures stand on its spot.
        private float[] NearestDistances() => _squads.Skip(1)
            .Select(squad => Distance(_em.GetComponentData<LocalTransform>(squad).Position))
            .SelectMany(distance => Enumerable.Repeat(distance, 4)).OrderBy(distance => distance).ToArray();

        // Corpses still on their fall clip; the warm-up squad's have handed over by now.
        private Entity[] NewCorpses()
        {
            using var all = Corpses();
            return all.ToArray().Where(corpse => !_em.GetComponentData<Corpse>(corpse).HandedOver).ToArray();
        }

        private static float Distance(float3 position) => math.distance((float3)Camera.main.transform.position, position);
    }
}
