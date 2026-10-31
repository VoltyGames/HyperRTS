using HyperRTS.Presentation.Destruction;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Combat;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction
{
    /// <summary>Section views follow the building's broken mask, quietly on the first frame, and come back on repair.</summary>
    public class SectionViewTests
    {
        private static readonly float3 Origin = new(10f, 0f, 10f);

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SectionViewSystem _views;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Section View Tests");
            _em = _kit.EntityManager;
            _views = _kit.World.CreateSystemManaged<SectionViewSystem>();
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void TheMaskDrivesTheViews()
        {
            var house = Building(0b0101ul, out var models);

            _views.Update();

            AssertBroken(models, true, false, true, false);
            Assert.AreEqual(0b0101ul, _em.GetComponentData<ShownSections>(house).Broken);
        }

        [Test]
        public void Break_AndRepair_FlipOnlyThatSection()
        {
            var house = Building(0ul, out var models);
            _views.Update();

            SetBroken(house, 0b1000ul);
            _views.Update();
            AssertBroken(models, false, false, false, true);

            SetBroken(house, 0ul);
            _views.Update();
            AssertBroken(models, false, false, false, false);
        }

        [Test]
        public void UnseenChange_ShowsTheEndStateWithoutReplaying()
        {
            var house = Building(0ul, out var models);
            _views.Update();
            var shown = _em.GetComponentData<ShownSections>(house);
            shown.Seen = false;
            _em.SetComponentData(house, shown);

            SetBroken(house, 0b0011ul);
            _views.Update();

            AssertBroken(models, true, true, false, false);
            Assert.IsTrue(_em.GetComponentData<ShownSections>(house).Seen);
        }

        private Entity Building(ulong broken, out SectionModel[] models)
        {
            var house = _em.CreateEntity();
            _em.AddComponentData(house, new LocalToWorld { Value = float4x4.Translate(Origin) });
            _em.AddComponentData(house, new Sections { Broken = broken });
            _em.AddComponentData(house, new ShownSections());
            _em.AddBuffer<Child>(house);
            var sides = new[]
            {
                new float3(4f, 1f, 0f), new float3(-4f, 1f, 0f), new float3(0f, 1f, 4f), new float3(0f, 1f, -4f),
            };
            models = new SectionModel[sides.Length];
            for (var i = 0; i < sides.Length; i++)
            {
                models[i] = new SectionModel { Intact = Piece(house), Broken = Piece(house) };
            }

            var sections = _em.AddBuffer<Section>(house);
            foreach (var side in sides)
            {
                sections.Add(new Section { Center = side, Outward = math.normalize(new float3(side.x, 0f, side.z)) });
            }

            var list = _em.AddBuffer<SectionModel>(house);
            foreach (var model in models)
            {
                list.Add(model);
            }

            return house;
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
            _em.SetComponentData(house, new Sections { Broken = broken });

        private void AssertBroken(SectionModel[] models, params bool[] broken)
        {
            for (var i = 0; i < models.Length; i++)
            {
                Assert.AreEqual(!broken[i], _em.IsComponentEnabled<MaterialMeshInfo>(models[i].Intact), $"section {i} intact drawn");
                Assert.AreEqual(broken[i], _em.IsComponentEnabled<MaterialMeshInfo>(models[i].Broken), $"section {i} broken drawn");
            }
        }
    }
}
