using System.Linq;
using HyperRTS.Presentation.Impacts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Replays;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Tests
{
    /// <summary>The impact mark system run on hand-made impacts: where projectors go, the cap, fades, replay seeks.</summary>
    public class ImpactMarkSystemTests
    {
        private const int Mortar = 7;

        private World _world;
        private EntityManager _em;
        private ImpactMarkSystem _system;
        private ImpactMark _mark;
        private Material _decal;
        private NativeHashMap<int, Entity> _prefabs;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Impact Mark Tests");
            _em = _world.EntityManager;
            _system = _world.CreateSystemManaged<ImpactMarkSystem>();
            _em.CreateEntity(typeof(CueQueue), typeof(CueEvent));

            _decal = new Material(Shader.Find("Shader Graphs/Decal"));
            _mark = ScriptableObject.CreateInstance<ImpactMark>();
            _mark.decals = new[] { _decal };
            _mark.size = new Vector2(3f, 3f);
            _mark.depth = 1f;
            _mark.lifetime = 10f;
            _mark.fadeTime = 4f;
            _mark.maxCount = 2;

            var prefab = _em.CreateEntity(typeof(Prefab));
            _em.AddComponentData(prefab, new EntityInfo { TypeId = Mortar });
            _em.AddBuffer<EntityImpactMark>(prefab).Add(new EntityImpactMark { Mark = _mark });
            _prefabs = new NativeHashMap<int, Entity>(1, Allocator.Persistent) { [Mortar] = prefab };
            _em.AddComponentData(_em.CreateEntity(), new PrefabRegistry { ByTypeId = _prefabs });
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
            _prefabs.Dispose();
            Object.DestroyImmediate(_mark);
            Object.DestroyImmediate(_decal);
        }

        private void Land(float3 position, ImpactSurface surface = ImpactSurface.Ground, int typeId = Mortar)
        {
            using var query = _em.CreateEntityQuery(typeof(CueQueue));
            _em.GetBuffer<CueEvent>(query.GetSingletonEntity())
                .Add(new CueEvent { TypeId = typeId, Slot = CueSlot.Impact, Position = position, Faction = 1, Surface = surface });
        }

        private void Run(double time)
        {
            _world.SetTime(new TimeData(time, 1f / 30f));
            _system.Update();
            using var query = _em.CreateEntityQuery(typeof(CueQueue));
            _em.GetBuffer<CueEvent>(query.GetSingletonEntity()).Clear();
        }

        private static DecalProjector[] Shown() => Resources.FindObjectsOfTypeAll<DecalProjector>()
            .Where(p => p.transform.parent != null && p.transform.parent.name == "HyperRTS Impact Marks" && p.enabled)
            .ToArray();

        [Test]
        public void GroundImpact_LaysTheMarkOnTheGround_OtherSurfacesAndTypesNone()
        {
            Land(new float3(5f, 1f, -2f));
            Land(new float3(9f, 0f, 0f), ImpactSurface.Water);
            Land(new float3(9f, 0f, 0f), ImpactSurface.Structure);
            Land(new float3(9f, 8f, 0f), ImpactSurface.Air);
            Land(new float3(9f, 0f, 0f), typeId: 99);
            Run(1.0);

            var shown = Shown();
            Assert.AreEqual(1, shown.Length);
            Assert.That(Vector3.Distance(new Vector3(5f, 0f, -2f), shown[0].transform.position), Is.LessThan(1e-4f),
                "at terrain height, flat ground here");
            Assert.AreEqual(new Vector3(3f, 3f, 1f), shown[0].size);
            Assert.AreSame(_decal, shown[0].material);
        }

        [Test]
        public void PastTheCap_TheOldestMarkMoves()
        {
            Land(new float3(1f, 0f, 0f));
            Run(1.0);
            Land(new float3(2f, 0f, 0f));
            Run(2.0);
            Land(new float3(3f, 0f, 0f));
            Run(3.0);

            var xs = Shown().Select(p => Mathf.Round(p.transform.position.x)).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { 2f, 3f }, xs);
        }

        [Test]
        public void Marks_HoldTheirLifetime_ThenFadeOut()
        {
            Land(float3.zero);
            Run(1.0);
            Run(11.0);
            Assert.AreEqual(1f, Shown()[0].fadeFactor, 1e-3f);

            Run(13.0);
            Assert.AreEqual(0.5f, Shown()[0].fadeFactor, 0.02f);

            Run(15.5);
            Assert.IsEmpty(Shown());
        }

        [Test]
        public void ReplaySeekBack_ClearsTheMarks()
        {
            var clock = _em.CreateEntity();
            _em.AddComponentData(clock, new ReplayPlayback { Time = 20f, Duration = 60f });
            Land(float3.zero);
            Run(1.0);
            Assert.AreEqual(1, Shown().Length, "replay time, not world time, ages the mark");

            _em.SetComponentData(clock, new ReplayPlayback { Time = 5f, Duration = 60f });
            Run(1.1);

            Assert.IsEmpty(Shown());
        }
    }
}
