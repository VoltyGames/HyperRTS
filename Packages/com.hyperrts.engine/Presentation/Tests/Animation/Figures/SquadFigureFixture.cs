using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
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
    public abstract class SquadFigureFixture
    {
        protected const float Step = 0.1f;
        protected const float DieHold = 2f;

        protected PresentationWorldKit _kit;
        protected EntityManager _em;
        protected SystemHandle _placement;
        protected SystemHandle _parents;
        protected SystemHandle _matrices;
        protected SystemHandle _system;
        protected Entity _squad;
        protected Entity _cues;
        protected Entity _model;
        protected Entity[] _figures;
        protected double _time;

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
            _cues = _em.CreateEntity(typeof(CueQueue));
            _em.AddBuffer<CueEvent>(_cues);
            _time = 0d;
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();
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
                _figures[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                _em.AddComponentData(_figures[i], new Parent { Value = _model });
                _em.AddComponentData(_figures[i], LocalTransform.Identity);
                var start = VATClip.Started(Range(0, true), 0f);
                _em.AddComponentData(_figures[i], new LowerClip { Clip = start });
                _em.AddComponentData(_figures[i], new UpperClip { Clip = start });
                _em.AddComponentData(_figures[i], new LowerFade { Fade = VATFade.None });
                _em.AddComponentData(_figures[i], new UpperFade { Fade = VATFade.None });
                _em.AddComponentData(_figures[i], new LocalToWorld { Value = float4x4.identity });
                figures[i] = new SquadFigure { Figure = _figures[i], Clips = Clips(0) };
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

        protected static FigureClips Clips(int offset) => new()
        {
            Idle = Range(offset, true),
            Walk = Range(offset + 10, true),
            Run = Range(offset + 20, true),
            Fire = Range(offset + 30, true),
            Die = Range(offset + 40, false),
            CrouchIdle = Range(offset + 100, true),
            CrouchWalk = Range(offset + 110, true),
            CrouchFire = Range(offset + 120, true),
            ProneIdle = Range(offset + 130, true),
            ProneCrawl = Range(offset + 140, true),
            ProneFire = Range(offset + 150, true),
        };

        protected static FigureClips BlastClips() => new()
        {
            Die = Range(40, false),
            DieBlastFront = Range(50, false),
            DieBlastBack = Range(60, false),
            DieBlastLeft = Range(70, false),
            DieBlastRight = Range(80, false),
        };

        public static VATClipRange Range(int startRow, bool loop) =>
            new() { StartRow = startRow, FrameCount = 10, Fps = 10f, Loop = loop };

        protected void Update()
        {
            _time += Step;
            _kit.World.SetTime(new TimeData(_time, Step));
            _placement.Update(_kit.World.Unmanaged);
            _parents.Update(_kit.World.Unmanaged);
            _matrices.Update(_kit.World.Unmanaged);
            _system.Update(_kit.World.Unmanaged);
            _em.GetBuffer<CueEvent>(_cues).Clear();
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
                figures.ElementAt(i).Clips.Walk.GroundSpeed = speed;
            }
        }

        protected SquadFigure Figure(int index) => _em.GetBuffer<SquadFigure>(_squad)[index];

        protected void SetBlastClips()
        {
            var figures = _em.GetBuffer<SquadFigure>(_squad);
            for (var i = 0; i < figures.Length; i++)
            {
                figures.ElementAt(i).Clips = BlastClips();
            }
        }

        protected void Blast(float3 position) => _em.GetBuffer<CueEvent>(_cues).Add(new CueEvent
        {
            Slot = CueSlot.Impact, Position = position, Radius = 4f,
        });

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

        protected VATClip Lower(int index) => _em.GetComponentData<LowerClip>(_figures[index]).Clip;

        protected VATClip Upper(int index) => _em.GetComponentData<UpperClip>(_figures[index]).Clip;

        // A second figure for the member's upgraded weapon, baked hidden like the squad's others.
        protected Entity AddUpgradedModel(int index, string weapon)
        {
            var upgraded = ModelPart();
            var figure = Figure(index);
            figure.Other = new FigureVariant
            {
                Model = new FigureModel { Figure = upgraded, Clips = Clips(1000), Weapon = weapon },
            };
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(index) = figure;
            return upgraded;
        }

        protected Entity AddEmplacedModel(int index)
        {
            var bare = ModelPart();
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
            var part = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
            _em.AddComponentData(part, new Parent { Value = _model });
            _em.AddComponentData(part, LocalTransform.Identity);
            var start = VATClip.Started(Range(0, true), 0f);
            _em.AddComponentData(part, new LowerClip { Clip = start });
            _em.AddComponentData(part, new UpperClip { Clip = start });
            _em.AddComponentData(part, new LowerFade { Fade = VATFade.None });
            _em.AddComponentData(part, new UpperFade { Fade = VATFade.None });
            _em.AddComponentData(part, new LocalToWorld { Value = float4x4.identity });
            _em.GetBuffer<FigureRenderer>(_squad).Add(new FigureRenderer { Model = part, Part = part });
            _em.GetBuffer<LinkedEntityGroup>(_squad).Add(part);
            return part;
        }

        protected bool DrawnModel(Entity part) => _em.IsComponentEnabled<MaterialMeshInfo>(part);

        protected bool Drawn(int index) => _em.IsComponentEnabled<MaterialMeshInfo>(_figures[index]);
    }
}
