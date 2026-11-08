using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common.Cells;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Vision.Fog;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>
    /// A four-sided building in a presentation world running <see cref="SectionViewSystem"/>, with helpers to break,
    /// hit and read which section models draw.
    /// </summary>
    public abstract class SectionViewFixture
    {
        private static readonly float3 Origin = new(10f, 0f, 5f);

        protected static readonly SectionClips Clips = new()
        {
            BlownIn = new SectionClip { StartRow = 1, FrameCount = 30, Fps = 30f },
            BlownOut = new SectionClip { StartRow = 31, FrameCount = 30, Fps = 30f },
            Collapse = new SectionClip { StartRow = 61, FrameCount = 60, Fps = 30f },
        };

        protected PresentationWorldKit _kit;
        protected EntityManager _em;
        private SectionViewSystem _views;
        protected Entity _house;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit(GetType().Name);
            _em = _kit.EntityManager;
            _kit.AddCues();
            _views = _kit.World.CreateSystemManaged<SectionViewSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            using (var grids = _em.CreateEntityQuery(typeof(FogOfWar)))
            {
                if (grids.TryGetSingleton(out FogOfWar fog))
                {
                    fog.Dispose();
                }
            }

            _kit.Dispose();
        }

        protected void Update(float time)
        {
            _kit.World.SetTime(new TimeData(time, 0.1f));
            _views.Update();
            _kit.ClearCues();
        }

        protected void Impact(float3 position) => _kit.PushCue(new CueEvent
        {
            Slot = CueSlot.Impact, Position = position, Owner = 2, Direction = math.up(),
        });

        // Cells 0 to 10 m are in team 1's sight, cells 10 to 20 m are not; owners 1 and 2 are enemies.
        protected void AddFog()
        {
            var relations = new OwnerRelations();
            relations.SetTeam(1, 1);
            relations.SetTeam(2, 2);
            _em.CreateSingleton(relations);
            _em.CreateSingleton(new LocalFogView { Viewer = 1, Team = 1, Active = true });
            var fog = FogOfWar.Create(new CellGrid(float2.zero, 10f, new int2(2, 1)), Allocator.Persistent);
            fog.Set(fog.Visible, int2.zero, 1);
            fog.Set(fog.VisibleTall, int2.zero, 1);
            _em.CreateSingleton(fog);
        }

        protected Entity Building(ulong broken, bool withPieces, out SectionModel[] models)
        {
            var house = _em.CreateEntity();
            _house = house;
            _em.AddComponentData(house, new LocalToWorld { Value = float4x4.Translate(Origin) });
            _em.AddComponentData(house, new SectionedBuilding { Broken = broken });
            _em.AddComponentData(house, new ShownSections());
            _em.AddComponentData(house, new HiddenBy());
            _em.AddBuffer<Child>(house);
            _em.AddBuffer<LinkedEntityGroup>(house).Add(house);
            _em.AddBuffer<ModelCopy>(house);
            var sides = new[]
            {
                new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f), new float3(0f, 1f, -4f),
            };
            models = new SectionModel[sides.Length];
            for (var i = 0; i < sides.Length; i++)
            {
                models[i] = withPieces
                    ? PiecesModel(house, sides[i])
                    : new SectionModel { Intact = Piece(house), Broken = Prefab(house) };
            }

            // Buffers last: every structural change above would invalidate them.
            _em.AddBuffer<Section>(house);
            _em.AddBuffer<SectionLook>(house);
            _em.AddBuffer<SectionModel>(house);
            var sections = _em.GetBuffer<Section>(house);
            var looks = _em.GetBuffer<SectionLook>(house);
            var list = _em.GetBuffer<SectionModel>(house);
            for (var i = 0; i < sides.Length; i++)
            {
                var outward = math.normalize(new float3(sides[i].x, 0f, sides[i].z));
                sections.Add(new Section { Center = sides[i], HalfSize = new float3(0.5f, 1f, 2f), Outward = outward });
                looks.Add(SectionLook.Untouched);
                list.Add(models[i]);
            }

            if (withPieces)
            {
                _em.AddComponentData(house, Clips);
            }

            return house;
        }

        private SectionModel PiecesModel(Entity house, float3 side)
        {
            var pieces = Prefab(house);
            _em.AddComponentData(pieces, new PlayingClip());
            return new SectionModel
            {
                Intact = Piece(house),
                Pieces = pieces,
                Stub = Prefab(house),
                Heap = Prefab(house),
                FirstHole = new SectionHole { Model = Prefab(house), Center = side - new float3(0f, 0f, 1f) },
                SecondHole = new SectionHole { Model = Prefab(house), Center = side + new float3(0f, 0f, 1f) },
            };
        }

        // The intact model's mesh under the house, as a SubScene building has it.
        private Entity Piece(Entity house)
        {
            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(piece, new Parent { Value = house });
            _em.GetBuffer<Child>(house).Add(new Child { Value = piece });
            _em.GetBuffer<LinkedEntityGroup>(house).Add(piece);
            return piece;
        }

        // Any other model, which bakes as a prefab of its own.
        private Entity Prefab(Entity house)
        {
            var model = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy), typeof(Prefab));
            _em.AddComponentData(model, new Parent { Value = house });
            _em.AddBuffer<LinkedEntityGroup>(model).Add(model);
            return model;
        }

        // The copy of a prefab model showing on the house, or none.
        protected Entity Copy(Entity model) => ModelCopies.Find(_em, _house, model);

        protected void SetBroken(Entity house, ulong broken) =>
            _em.SetComponentData(house, new SectionedBuilding { Broken = broken });

        protected bool Drawn(Entity mesh) => _em.IsComponentEnabled<MaterialMeshInfo>(mesh);

        // A prefab model draws through its copy, and only one copy at a time.
        private bool Shown(Entity model)
        {
            var copy = Copy(model);
            return copy != Entity.Null && Drawn(copy);
        }

        protected void AssertDrawn(SectionModel model, bool intact, bool pieces, bool stub, bool heap)
        {
            Assert.AreEqual(intact, Drawn(model.Intact), "intact drawn");
            Assert.AreEqual(pieces, Shown(model.Pieces), "pieces drawn");
            Assert.AreEqual(stub, Shown(model.Stub), "stub drawn");
            Assert.AreEqual(heap, Shown(model.Heap), "heap drawn");
        }

        protected void AssertHole(SectionModel model, int drawn)
        {
            Assert.IsFalse(Drawn(model.Intact), "the standing model gives way to the hole");
            Assert.AreEqual(drawn == 0, Shown(model.FirstHole.Model), "first hole");
            Assert.AreEqual(drawn == 1, Shown(model.SecondHole.Model), "second hole");
        }

        protected void AssertBroken(SectionModel[] models, params bool[] broken)
        {
            for (var i = 0; i < models.Length; i++)
            {
                Assert.AreEqual(!broken[i], Drawn(models[i].Intact), $"section {i} intact drawn");
                Assert.AreEqual(broken[i], Shown(models[i].Broken), $"section {i} broken drawn");
            }
        }
    }
}
