using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Animation.Clips;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>
    /// A hand-made four-figure squad in a presentation world, with helpers to move it, fire it and read what each
    /// figure plays.
    /// </summary>
    public abstract partial class SquadFigureFixture
    {
        protected const float Step = 0.1f;
        protected const float DieHold = 2f;

        /// <summary>Seconds of the fixture's fire clips, which set how long a figure keeps firing.</summary>
        protected const float FireLength = 1f;

        protected static readonly int SquadType = EntityInfo.TypeIdFromName("Test Squad");

        protected PresentationWorldKit _kit;
        protected EntityManager _em;
        protected SystemHandle _placement;
        protected SystemHandle _parents;
        protected SystemHandle _matrices;
        protected SystemHandle _system;
        protected Entity _squad;
        protected Entity _model;
        protected Entity[] _figures;

        /// <summary>Systems a test adds, updated after the figure system.</summary>
        protected readonly List<SystemHandle> _later = new();
        protected double _time;
        private readonly Dictionary<int, BlobAssetReference<FigureClips>> _clips = new();
        private readonly List<Object> _made = new();

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Squad Figure Tests");
            _em = _kit.EntityManager;
            _placement = _kit.World.CreateSystem<SquadFigurePlacementSystem>();
            _parents = _kit.World.CreateSystem<ParentSystem>();
            _matrices = _kit.World.CreateSystem<LocalToWorldSystem>();
            _system = _kit.World.CreateSystem<SquadFigureSystem>();
            _squad = Squad(4);
            _time = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            _kit.Dispose();
            foreach (var clips in _clips.Values)
            {
                clips.Dispose();
            }

            _clips.Clear();
            foreach (var made in _made)
            {
                Object.DestroyImmediate(made);
            }

            _made.Clear();
            _later.Clear();
        }

        protected Entity Squad(int count)
        {
            _figures = new Entity[count];
            var figures = new SquadFigure[count];
            var squad = _em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            _em.SetComponentData(squad, LocalTransform.Identity);
            _model = _em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld));
            _em.SetComponentData(_model, LocalTransform.Identity);
            _em.AddComponentData(_model, new Parent { Value = squad });
            for (var i = 0; i < count; i++)
            {
                _figures[i] = CreatePart();
                Drive(_figures[i], squad, _figures[i], i);
                figures[i] = new SquadFigure { Figure = _figures[i], Clips = Clips(0), Baked = quaternion.identity };
            }

            _em.AddBuffer<FigureRenderer>(squad);
            foreach (var figure in _figures)
            {
                _em.GetBuffer<FigureRenderer>(squad).Add(new FigureRenderer { Model = figure, Part = figure });
            }

            var group = _em.AddBuffer<LinkedEntityGroup>(squad);
            group.Add(squad);
            foreach (var figure in _figures)
            {
                group.Add(figure);
            }

            _em.AddComponentData(squad, new Health { Current = 100f, Max = 100f });
            _em.AddComponentData(squad, new EntityInfo { TypeId = SquadType });
            _em.AddComponentData(squad, new Owner { Value = 1 });
            _em.AddBuffer<WeaponPose>(squad).Add(default);
            var members = _em.AddBuffer<SquadMember>(squad);
            for (var i = 0; i < count; i++)
            {
                members.Add(new SquadMember { Health = 25f, MaxHealth = 25f });
            }

            var writer = new EntityWriter(_em, squad);
            var setup = new SquadFigures { RunSpeed = 3f, DieHold = DieHold };
            SquadFiguresAuthoring.AddSquad(writer, setup, figures);
            return squad;
        }

        protected void Update()
        {
            _time += Step;
            _kit.World.SetTime(new TimeData(_time, Step));
            _placement.Update(_kit.World.Unmanaged);
            _parents.Update(_kit.World.Unmanaged);
            _matrices.Update(_kit.World.Unmanaged);
            _system.Update(_kit.World.Unmanaged);
            foreach (var system in _later)
            {
                system.Update(_kit.World.Unmanaged);
            }
        }

        // One update past the seconds, so rounding can't leave the clock just short.
        protected void Advance(float seconds)
        {
            var steps = (int)math.ceil(seconds / Step) + 1;
            for (var i = 0; i < steps; i++)
            {
                Update();
            }
        }

        protected void Move(float speed)
        {
            MoveSquad(new float3(speed * Step, 0f, 0f), 0f);
            Update();
        }

        // Between updates, as movement does before the transform systems build matrices.
        protected void MoveSquad(float3 by, float turn)
        {
            var spot = _em.GetComponentData<LocalTransform>(_squad);
            spot.Position += by;
            spot.Rotation = math.mul(quaternion.RotateY(turn), spot.Rotation);
            _em.SetComponentData(_squad, spot);
        }

        protected void MoveFor(float speed, float seconds)
        {
            for (var i = 0; i < (int)math.ceil(seconds / Step); i++)
            {
                Move(speed);
            }
        }

        protected void GiveWalkGroundSpeed(float speed)
        {
            var figures = _em.GetBuffer<SquadFigure>(_squad);
            for (var i = 0; i < figures.Length; i++)
            {
                figures.ElementAt(i).Clips.Value.Legs[1].GroundSpeed = speed;
            }
        }

        protected SquadFigure Figure(int index) => _em.GetBuffer<SquadFigure>(_squad)[index];

        // How a dead member fell; a blast lay toward the direction on the ground plane.
        protected void SetDeath(int index, DeathCause cause, float2 toward = default)
        {
            ref var member = ref _em.GetBuffer<SquadMember>(_squad).ElementAt(index);
            member.Death = cause;
            member.BlastHeading = SquadMember.PackHeading(toward);
        }

        // The first members stay alive, the rest are dead.
        protected void SetLiving(int count)
        {
            var members = _em.GetBuffer<SquadMember>(_squad);
            for (var i = 0; i < members.Length; i++)
            {
                members.ElementAt(i).Health = i < count ? 25f : 0f;
            }
        }

        protected void Member(int index, float2 offset = default, byte weapon = 0, Posture posture = Posture.Stand)
        {
            ref var member = ref _em.GetBuffer<SquadMember>(_squad).ElementAt(index);
            member.Offset = offset;
            member.Weapon = weapon;
            member.Posture = posture;
        }

        protected FigureState State(int index) => _em.GetBuffer<SquadFigure>(_squad)[index].State;

        /// <summary>True when <paramref name="clip"/> is the one playing, whenever it started.</summary>
        protected static bool Plays(in PlayingClip clip, in ClipRange range)
        {
            var packed = range.Packed(0f);
            return clip.Value.x == packed.x && clip.Value.y == packed.y && (clip.Value.z > 0f) == (packed.z > 0f);
        }

        protected PlayingClip Lower(int index) => _em.GetComponentData<LowerClip>(_figures[index]).Clip;

        protected PlayingClip Upper(int index) => _em.GetComponentData<UpperClip>(_figures[index]).Clip;

        // A second figure for the member's upgraded weapon, baked hidden like the squad's others.
        protected Entity AddUpgradedModel(int index)
        {
            var upgraded = ModelPart();
            Drive(upgraded, _squad, upgraded, index);
            var figure = Figure(index);
            figure.Other = new FigureVariant
            {
                Model = new FigureModel { Figure = upgraded, Clips = Clips(1000) },
            };
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(index) = figure;
            return upgraded;
        }

        protected Entity AddEmplacedModel(int index)
        {
            var bare = ModelPart();
            Drive(bare, _squad, bare, index);
            var figure = Figure(index);
            figure.Other = new FigureVariant
            {
                Reason = VariantReason.Emplaced, Model = new FigureModel { Figure = bare, Clips = Clips(1000) },
            };
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(index) = figure;
            return bare;
        }

        protected Entity AddDeployedModel()
        {
            var gun = ModelPart();
            var squad = _em.GetComponentData<SquadFigures>(_squad);
            squad.DeployedModel = gun;
            _em.SetComponentData(_squad, squad);
            return gun;
        }

        protected Entity ModelPart()
        {
            var part = CreatePart();
            _em.GetBuffer<FigureRenderer>(_squad).Add(new FigureRenderer { Model = part, Part = part });
            _em.GetBuffer<LinkedEntityGroup>(_squad).Add(part);
            return part;
        }

        // The part follows its figure as the baking system sets it up.
        private void Drive(Entity part, Entity root, Entity model, int index) =>
            _em.AddComponentData(part, new FigurePart { Root = root, Index = index, Model = model });

        // A hidden figure part under the squad's model, on the first clip.
        private Entity CreatePart()
        {
            var part = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(part, new Parent { Value = _model });
            _em.AddComponentData(part, LocalTransform.Identity);
            var start = PlayingClip.Started(Range(0, true), 0f);
            _em.AddComponentData(part, new LowerClip { Clip = start });
            _em.AddComponentData(part, new UpperClip { Clip = start });
            _em.AddComponentData(part, new LowerFade { Fade = ClipFade.None });
            _em.AddComponentData(part, new UpperFade { Fade = ClipFade.None });
            _em.AddComponentData(part, new PoseSlot { Value = PoseSlot.None });
            _em.AddComponentData(part, new RenderBounds { Value = new AABB { Extents = new float3(0.5f, 1f, 0.5f) } });
            _em.AddComponentData(part, new LocalToWorld { Value = float4x4.identity });
            return part;
        }

        protected bool DrawnModel(Entity part) => _em.IsComponentEnabled<MaterialMeshInfo>(part);

        protected bool Drawn(int index) => _em.IsComponentEnabled<MaterialMeshInfo>(_figures[index]);
    }
}
