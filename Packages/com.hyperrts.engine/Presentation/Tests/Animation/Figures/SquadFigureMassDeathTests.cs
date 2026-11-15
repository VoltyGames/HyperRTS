using System.Diagnostics;
using System.Linq;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
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
    /// Squads of four five-part figures dying together: a blast wiping 300 comes a frame's budget at a time, nearest the
    /// camera first, and side by side each death cue names its own squad.
    /// </summary>
    public class SquadFigureMassDeathTests : SquadFigureRagdollFixture
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
                _em.SetComponentData(_squads[i], new SpawnNumber { Value = i + 1 });
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
            var budget = DetailBudget.CorpsesPerFrameAt(PresentationQuality.Current.Effects);
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

        [Test]
        public void AdjacentSquadsOfOneType_BothLeaveCorpses()
        {
            PlaceFirstTwo(1f);
            Update();
            for (var i = 0; i < 2; i++)
            {
                PushDeath(i, Spot(i));
                _em.DestroyEntity(_squads[i]);
            }

            TimedUpdate();
            Advance(1f);

            using var corpses = Corpses();
            Assert.AreEqual(8, corpses.Length, "each squad's four figures fall");
        }

        [Test]
        public void LiveNeighbour_IsNotTakenForTheSquadThatDied()
        {
            PlaceFirstTwo(1f);
            Update();
            PushDeath(0, Spot(0));
            _em.DestroyEntity(_squads[0]);
            TimedUpdate();

            Assert.IsFalse(_em.GetComponentData<SquadEnd>(_squads[1]).Died, "the live squad nearer the cue");
            Advance(1f);
            using var fallen = Corpses();
            Assert.AreEqual(4, fallen.Length, "the dead squad's figures fall");

            // Walking into fog takes the ghost with no death cue.
            _em.DestroyEntity(_squads[1]);
            Advance(1f);
            using var after = Corpses();
            Assert.LessOrEqual(after.Length, fallen.Length, "the neighbour leaves no corpses");
        }

        private void PlaceFirstTwo(float apart)
        {
            _em.SetComponentData(_squads[0], LocalTransform.FromPosition(new float3(-57f, 0f, 0f)));
            _em.SetComponentData(_squads[1], LocalTransform.FromPosition(new float3(-57f + apart, 0f, 0f)));
        }

        /// <summary>Kills squads first to last at once, as one blast, and returns the cost of the frame after.</summary>
        private double Wipe(int first, int last)
        {
            for (var i = first; i < last; i++)
            {
                PushDeath(i, Spot(i));
                _em.DestroyEntity(_squads[i]);
            }

            return TimedUpdate();
        }

        // Read before the squad is destroyed, as a cue's place is where its source stood.
        private void PushDeath(int squad, float3 spot) => _kit.PushCue(new CueEvent
        {
            TypeId = SquadType, SourceNumber = squad + 1, Slot = CueSlot.Death, Position = spot,
        });

        private float3 Spot(int squad) => _em.GetComponentData<LocalTransform>(_squads[squad]).Position;

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
