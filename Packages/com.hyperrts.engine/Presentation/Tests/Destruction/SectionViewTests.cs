using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Vision;
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
    /// Section views follow the building's broken mask: quietly on the first frame, as a clip of pieces for a break
    /// this client saw, holed where it saw impacts, and back to standing on repair.
    /// </summary>
    public class SectionViewTests
    {
        private static readonly float3 Origin = new(10f, 0f, 5f);

        private static readonly SectionClips Clips = new()
        {
            BlownIn = new SectionClip { StartRow = 1, FrameCount = 30, Fps = 30f },
            BlownOut = new SectionClip { StartRow = 31, FrameCount = 30, Fps = 30f },
            Collapse = new SectionClip { StartRow = 61, FrameCount = 60, Fps = 30f },
        };

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SectionViewSystem _views;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Section View Tests");
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

        [Test]
        public void TheMaskDrivesTheViews()
        {
            var house = Building(0b0101ul, false, out var models);

            Update(0f);

            AssertBroken(models, true, false, true, false);
            Assert.AreEqual(0b0101ul, _em.GetComponentData<ShownSections>(house).Broken);
        }

        [Test]
        public void Break_AndRepair_FlipOnlyThatSection()
        {
            var house = Building(0ul, false, out var models);
            Update(0f);

            SetBroken(house, 0b1000ul);
            Update(0f);
            AssertBroken(models, false, false, false, true);

            SetBroken(house, 0ul);
            Update(0f);
            AssertBroken(models, false, false, false, false);
        }

        [Test]
        public void SeenBreak_PlaysItsClipOnceThenShowsTheHeap()
        {
            var house = Building(0ul, true, out var models);
            Update(0f);

            SetBroken(house, 0b0100ul);
            Update(10f);

            var piece = models[2];
            var started = _em.GetComponentData<VATClip>(piece.Pieces).Value;
            Assert.AreEqual(new float4(61f, 60f, -30f, 10f), started, "falls apart alone");
            AssertDrawn(piece, intact: false, pieces: true, stub: true, heap: false);

            Update(10f + Clips.Collapse.Seconds + 0.1f);
            AssertDrawn(piece, intact: false, pieces: false, stub: true, heap: true);
            Assert.AreEqual(10f, _em.GetComponentData<VATClip>(piece.Pieces).Value.w, "the clip is not replayed");
            AssertDrawn(models[0], intact: true, pieces: false, stub: false, heap: false);
        }

        [Test]
        public void UnseenBreak_ShowsTheEndStateWithoutAClip()
        {
            var house = Building(0ul, true, out var models);
            Update(0f);
            var shown = _em.GetComponentData<ShownSections>(house);
            shown.Seen = false;
            _em.SetComponentData(house, shown);

            SetBroken(house, 0b0011ul);
            Update(5f);

            AssertDrawn(models[0], intact: false, pieces: false, stub: true, heap: true);
            AssertDrawn(models[1], intact: false, pieces: false, stub: true, heap: true);
            AssertDrawn(models[2], intact: true, pieces: false, stub: false, heap: false);
            Assert.AreEqual(0f, _em.GetComponentData<VATClip>(models[0].Pieces).Value.w);
            Assert.IsTrue(_em.GetComponentData<ShownSections>(house).Seen);
        }

        [Test]
        public void Stub_HidesWhileNoSupportStands_AndReturnsOnRepair()
        {
            var house = Building(0ul, true, out var models);
            var sections = _em.GetBuffer<Section>(house);
            var resting = sections[2];
            resting.Supports = 0b1000ul;
            sections[2] = resting;
            Update(0f);
            SetBroken(house, 0b0100ul);
            Update(1f);
            Update(1f + Clips.Collapse.Seconds + 0.1f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: true, heap: true);

            SetBroken(house, 0b1100ul);
            Update(10f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: false, heap: true);

            SetBroken(house, 0b0100ul);
            Update(11f);
            AssertDrawn(models[2], intact: false, pieces: false, stub: true, heap: true);
        }

        [Test]
        public void Repair_RevealsTheSectionAgain()
        {
            var house = Building(0ul, true, out var models);
            Update(0f);
            SetBroken(house, 0b0010ul);
            Update(1f);

            SetBroken(house, 0ul);
            Update(2f);

            AssertDrawn(models[1], intact: true, pieces: false, stub: false, heap: false);
        }

        [Test]
        public void SeenImpacts_PickTheNearestHole_AndUnseenOnesAreIgnored()
        {
            Building(0ul, true, out var models);
            AddFog();
            Update(0f);

            // Section 1 is west of the building at x = 6 (seen); section 0 is east at x = 14 (in fog).
            Impact(new float3(5.2f, 1f, 6f));
            Impact(new float3(15.2f, 1f, 6f));
            Update(1f);

            AssertHole(models[1], 1);
            AssertDrawn(models[0], intact: true, pieces: false, stub: false, heap: false);
        }

        [TestCase(5.2f, 1f, TestName = "From outside it blows in")]
        [TestCase(6.8f, 31f, TestName = "From inside it blows out")]
        public void Break_AfterASeenImpact_PicksTheClipByTheSide(float x, float startRow)
        {
            var house = Building(0ul, true, out var models);
            AddFog();
            Update(0f);
            Impact(new float3(x, 1f, 6f));
            Update(1f);

            SetBroken(house, 0b0010ul);
            Update(1.5f);

            Assert.AreEqual(startRow, _em.GetComponentData<VATClip>(models[1].Pieces).Value.x);
        }

        private void Update(float time)
        {
            _kit.World.SetTime(new TimeData(time, 0.1f));
            _views.Update();
            _kit.ClearCues();
        }

        private void Impact(float3 position) => _kit.PushCue(new CueEvent
        {
            Slot = CueSlot.Impact, Position = position, Faction = 2, Direction = math.up(),
        });

        // Cells 0 to 10 m are in team 1's sight, cells 10 to 20 m are not; factions 1 and 2 are enemies.
        private void AddFog()
        {
            var relations = new FactionRelations();
            relations.SetTeam(1, 1);
            relations.SetTeam(2, 2);
            _em.CreateSingleton(relations);
            _em.CreateSingleton(new LocalFogView { Viewer = 1, Team = 1, Active = true });
            var fog = FogOfWar.Create(new CellGrid(float2.zero, 10f, new int2(2, 1)), Allocator.Persistent);
            fog.Set(fog.Visible, int2.zero, 1);
            fog.Set(fog.VisibleTall, int2.zero, 1);
            _em.CreateSingleton(fog);
        }

        private Entity Building(ulong broken, bool withPieces, out SectionModel[] models)
        {
            var house = _em.CreateEntity();
            _em.AddComponentData(house, new LocalToWorld { Value = float4x4.Translate(Origin) });
            _em.AddComponentData(house, new SectionedBuilding { Broken = broken });
            _em.AddComponentData(house, new ShownSections());
            _em.AddBuffer<Child>(house);
            var sides = new[]
            {
                new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f), new float3(0f, 1f, -4f),
            };
            models = new SectionModel[sides.Length];
            for (var i = 0; i < sides.Length; i++)
            {
                models[i] = withPieces
                    ? PiecesModel(house, sides[i])
                    : new SectionModel { Intact = Piece(house), Broken = Piece(house) };
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
            var pieces = Piece(house);
            _em.AddComponentData(pieces, new VATClip());
            return new SectionModel
            {
                Intact = Piece(house),
                Pieces = pieces,
                Stub = Piece(house),
                Heap = Piece(house),
                FirstHole = new SectionHole { Model = Piece(house), Center = side - new float3(0f, 0f, 1f) },
                SecondHole = new SectionHole { Model = Piece(house), Center = side + new float3(0f, 0f, 1f) },
            };
        }

        // A model mesh under the house, as a SubScene building has it.
        private Entity Piece(Entity house)
        {
            var piece = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(piece, new Parent { Value = house });
            _em.GetBuffer<Child>(house).Add(new Child { Value = piece });
            return piece;
        }

        private void SetBroken(Entity house, ulong broken) =>
            _em.SetComponentData(house, new SectionedBuilding { Broken = broken });

        private bool Drawn(Entity model) => _em.IsComponentEnabled<MaterialMeshInfo>(model);

        private void AssertDrawn(SectionModel model, bool intact, bool pieces, bool stub, bool heap)
        {
            Assert.AreEqual(intact, Drawn(model.Intact), "intact drawn");
            Assert.AreEqual(pieces, Drawn(model.Pieces), "pieces drawn");
            Assert.AreEqual(stub, Drawn(model.Stub), "stub drawn");
            Assert.AreEqual(heap, Drawn(model.Heap), "heap drawn");
        }

        private void AssertHole(SectionModel model, int drawn)
        {
            Assert.IsFalse(Drawn(model.Intact), "the standing model gives way to the hole");
            Assert.AreEqual(drawn == 0, Drawn(model.FirstHole.Model), "first hole");
            Assert.AreEqual(drawn == 1, Drawn(model.SecondHole.Model), "second hole");
        }

        private void AssertBroken(SectionModel[] models, params bool[] broken)
        {
            for (var i = 0; i < models.Length; i++)
            {
                Assert.AreEqual(!broken[i], Drawn(models[i].Intact), $"section {i} intact drawn");
                Assert.AreEqual(broken[i], Drawn(models[i].Broken), $"section {i} broken drawn");
            }
        }
    }
}
