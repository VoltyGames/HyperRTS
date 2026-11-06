using HyperRTS.Presentation.Animation.Bones;
using HyperRTS.Presentation.Animation.Figures;
using HyperRTS.Presentation.Animation.VAT;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Infantry;
using System.Collections.Generic;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
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

        /// <summary>Seconds of the fixture's fire clips, which set how long a figure keeps firing.</summary>
        protected const float FireLength = 1f;

        protected PresentationWorldKit _kit;
        protected EntityManager _em;
        protected SystemHandle _placement;
        protected SystemHandle _parents;
        protected SystemHandle _matrices;
        protected SystemHandle _system;
        protected Entity _squad;
        protected Entity _model;
        protected Entity[] _figures;
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

        // The ranges the pipeline gives a figure whose table starts at the row offset; figures at one offset share them.
        protected BlobAssetReference<FigureClips> Clips(int offset)
        {
            if (!_clips.TryGetValue(offset, out var clips))
            {
                var table = Table(offset);
                var set = Set();
                _made.Add(table);
                _made.Add(set);
                clips = set.Build(table);
                _clips[offset] = clips;
            }

            return clips;
        }

        // Every clip the set names, ten rows apart from the offset; the falls play once and the rest loop.
        public static BoneClipTable Table(int offset)
        {
            var table = ScriptableObject.CreateInstance<BoneClipTable>();
            var names = new[]
            {
                "Idle", "Walk", "Run", "Fire", "Die", "DieBlast_Front", "DieBlast_Back", "DieBlast_Left", "DieBlast_Right",
                "Dig", "CrouchIdle", "CrouchWalk", "CrouchFire", "ProneIdle", "ProneCrawl", "ProneFire", "ProneDie", "CrouchDie",
            };
            var rows = new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170 };
            for (var i = 0; i < names.Length; i++)
            {
                table.clips.Add(new VATClipTable.Clip
                {
                    name = names[i], startRow = offset + rows[i], frameCount = 10, fps = 10f, loop = !names[i].Contains("Die"),
                });
            }

            return table;
        }

        // Crouched and prone figures run with their walking and crawling clips, and a crouched fall is its own clip.
        public static FigureClipSet Set()
        {
            var set = ScriptableObject.CreateInstance<FigureClipSet>();
            set.idle = Postures("Idle", "CrouchIdle", "ProneIdle");
            set.walk = Postures("Walk", "CrouchWalk", "ProneCrawl");
            set.run = Postures("Run", "CrouchWalk", "ProneCrawl");
            set.fire = Postures("Fire", "CrouchFire", "ProneFire");
            set.actions.Add(new ActionClip { action = MemberAction.Dig, clip = "Dig" });
            set.shotDeath = Postures("Die", "CrouchDie", "ProneDie");
            set.blastFrontDeath = Postures("DieBlast_Front", "CrouchDie", "ProneDie");
            set.blastBackDeath = Postures("DieBlast_Back", "CrouchDie", "ProneDie");
            set.blastLeftDeath = Postures("DieBlast_Left", "CrouchDie", "ProneDie");
            set.blastRightDeath = Postures("DieBlast_Right", "CrouchDie", "ProneDie");
            return set;
        }

        private static PostureClips Postures(string stand, string crouch, string prone) =>
            new() { stand = stand, crouch = crouch, prone = prone };

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

        protected VATClip Lower(int index) => _em.GetComponentData<LowerClip>(_figures[index]).Clip;

        protected VATClip Upper(int index) => _em.GetComponentData<UpperClip>(_figures[index]).Clip;

        // A second figure for the member's upgraded weapon, baked hidden like the squad's others.
        protected Entity AddUpgradedModel(int index)
        {
            var upgraded = ModelPart();
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
