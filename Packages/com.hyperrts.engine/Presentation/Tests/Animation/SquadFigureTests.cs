using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation;
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

namespace HyperRTS.Presentation.Tests.Animation
{
    /// <summary>
    /// Squad figures on a hand-made four-figure squad: a dead member drops its figure, each figure stands where its
    /// member does and plays clips from its own motion, posture and weapon (legs and upper body apart), and figures from separate tables
    /// play their own clips.
    /// </summary>
    public class SquadFigureTests
    {
        private const float Step = 0.1f;
        private const float DieHold = 2f;

        private PresentationWorldKit _kit;
        private EntityManager _em;
        private SystemHandle _system;
        private Entity _squad;
        private Entity _cues;
        private Entity _model;
        private Entity[] _figures;
        private double _time;

        [SetUp]
        public void SetUp()
        {
            _kit = new PresentationWorldKit("Squad Figure Tests");
            _em = _kit.EntityManager;
            _system = _kit.World.CreateSystem<SquadFigureSystem>();
            _squad = Squad(4);
            _cues = _em.CreateEntity(typeof(CueQueue));
            _em.AddBuffer<CueEvent>(_cues);
            _time = 0d;
        }

        [TearDown]
        public void TearDown() => _kit.Dispose();

        [Test]
        public void MemberFiringItsUpgrade_ShowsTheUpgradedFigureAndTheCardNameFollows()
        {
            var upgraded = AddUpgradedModel(1, "RPG-7");
            Update();
            Assert.IsTrue(Drawn(1));
            Assert.IsFalse(DrawnModel(upgraded), "the upgraded figure waits hidden");

            Member(1, weapon: 1);
            Update();
            Assert.IsFalse(Drawn(1));
            Assert.IsTrue(DrawnModel(upgraded));
            Assert.AreEqual("RPG-7", Figure(1).Weapon.ToString());
            Assert.IsTrue(_em.GetComponentData<LowerClip>(upgraded).Clip.Plays(Range(1000, true)), "it plays its own table");

            Member(1, weapon: 0);
            Update();
            Assert.IsTrue(Drawn(1));
            Assert.IsFalse(DrawnModel(upgraded));
        }

        [Test]
        public void DeployedWeapon_ShowsItsModelAndSendsTheCrewToTheirPlacesOnlyWhileSetUp()
        {
            var gun = AddDeployedModel();
            var buffer = _em.GetBuffer<SquadFigure>(_squad);
            buffer.ElementAt(0).Crew = new CrewPlace { Takes = true, Offset = new float2(0f, -0.6f), Posture = Posture.Prone };
            _em.AddComponentData(_squad, new Deployable { State = DeployState.Packed });
            Member(0, offset: new float2(2f, 0f));
            Update();
            Advance(1f);
            Assert.IsFalse(DrawnModel(gun), "packed, the gun is stowed");
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(_figures[0]).Position.x, 0.05f);

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Deploying });
            Advance(2f);
            Assert.IsTrue(DrawnModel(gun));
            var place = _em.GetComponentData<LocalTransform>(_figures[0]).Position;
            Assert.AreEqual(0f, place.x, 0.05f, "not dragged back to its slot");
            Assert.AreEqual(-0.6f, place.z, 0.05f);
            Assert.IsTrue(Lower(0).Plays(Range(130, true)), "prone at the gun");

            _em.SetComponentData(_squad, new Deployable { State = DeployState.Packing });
            Advance(2f);
            Assert.IsFalse(DrawnModel(gun));
            Assert.AreEqual(2f, _em.GetComponentData<LocalTransform>(_figures[0]).Position.x, 0.05f);
        }

        [Test]
        public void FigureStands_WhereItsMemberDoesInTheModelsFrame()
        {
            var turned = float4x4.RotateY(math.PI * 0.5f);
            _em.SetComponentData(_model, new LocalToWorld { Value = turned });
            Member(1, offset: new float2(2f, -1f));

            Update();

            var local = _em.GetComponentData<LocalTransform>(_figures[1]).Position;
            var world = math.transform(turned, local);
            Assert.AreEqual(2f, world.x, 1e-4f);
            Assert.AreEqual(-1f, world.z, 1e-4f);
        }

        [Test]
        public void MemberCatchingUp_RunsWhileItsSquadmatesStandIdle()
        {
            Update();
            Advance(SquadFigures.MoveHold);
            Member(2, offset: new float2(3f, 0f));
            Update();

            Assert.IsTrue(Lower(2).Plays(Range(20, true)));
            Assert.IsTrue(Lower(1).Plays(Range(0, true)));
        }

        [Test]
        public void SquadMovingInSteps_StaysInWalkWithoutSwitchingClips()
        {
            Update();
            for (var frame = 0; frame < 10; frame++)
            {
                Move(frame % 2 == 0 ? 3f : 0f);
            }

            var started = Figure(0).LowerStart;
            Assert.AreEqual(FigureState.Walk, State(0));
            for (var frame = 0; frame < 40; frame++)
            {
                Move(frame % 2 == 0 ? 3f : 0f);
                Assert.AreEqual(FigureState.Walk, State(0));
            }

            Assert.AreEqual(started, Figure(0).LowerStart, "the stride never restarted");

            var changes = 0;
            for (var frame = 0; frame < 30; frame++)
            {
                var before = State(0);
                Move(0f);
                changes += State(0) != before ? 1 : 0;
            }

            Assert.AreEqual(1, changes);
            Assert.AreEqual(FigureState.Idle, State(0));
        }

        [Test]
        public void MemberInCover_TurnsToItsHeadingAndBackWhenItLeaves()
        {
            Update();
            ref var member = ref _em.GetBuffer<SquadMember>(_squad).ElementAt(0);
            member.Cover = CoverKind.Heavy;
            member.CoverHeading = 0;

            Advance(2f);
            var facing = math.mul(_em.GetComponentData<LocalTransform>(_figures[0]).Rotation, math.forward());
            Assert.AreEqual(1f, facing.x, 0.05f, "turned from the squad's Z to the heading along X");

            _em.GetBuffer<SquadMember>(_squad).ElementAt(0).Cover = CoverKind.None;
            Advance(2f);
            facing = math.mul(_em.GetComponentData<LocalTransform>(_figures[0]).Rotation, math.forward());
            Assert.AreEqual(1f, facing.z, 0.05f);
            Assert.AreEqual(0f, facing.x, 0.05f);
        }

        [Test]
        public void Figure_FiresOnlyOnItsOwnWeaponsShot()
        {
            _em.GetBuffer<WeaponPose>(_squad).Add(default);
            Member(1, weapon: 1);
            Update();

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(1).CountShot();
            Update();

            Assert.IsTrue(Upper(1).Plays(Range(30, true)));
            Assert.IsTrue(Upper(0).Plays(Range(0, true)));
        }

        [Test]
        public void DroppedFigure_PlaysDieThenHides()
        {
            Update();
            SetLiving(3);
            Update();

            Assert.AreEqual(FigureState.Die, State(3));
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));
            Assert.IsTrue(Drawn(3));

            Advance(DieHold);

            Assert.AreEqual(FigureState.Hidden, State(3));
            Assert.IsFalse(Drawn(3));
            Assert.AreEqual(FigureState.Idle, State(2));
            Assert.IsTrue(Drawn(2));
        }

        [Test]
        public void BlastDeath_PicksTheClipForWhereTheBlastCameFrom()
        {
            var clips = BlastClips();
            var facing = float4x4.identity;

            Assert.AreEqual(clips.DieBlastFront, clips.Blasted(new float3(0f, 0f, 5f), facing));
            Assert.AreEqual(clips.DieBlastBack, clips.Blasted(new float3(0f, 0f, -5f), facing));
            Assert.AreEqual(clips.DieBlastRight, clips.Blasted(new float3(5f, 0f, 1f), facing));
            Assert.AreEqual(clips.DieBlastLeft, clips.Blasted(new float3(-5f, 0f, 1f), facing));

            var turned = float4x4.RotateY(math.PI);
            Assert.AreEqual(clips.DieBlastBack, clips.Blasted(new float3(0f, 0f, 5f), turned));
        }

        [Test]
        public void FigureWithoutBlastClips_PlaysDie()
        {
            Update();
            Blast(new float3(0f, 0f, 3f));
            SetLiving(3);
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(40, false)));
        }

        [Test]
        public void DroppedFigure_FallsFromABlastNearTheSquad()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 3f));
            SetLiving(3);
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(50, false)));
        }

        [Test]
        public void FigureThatFellJustBeforeTheBlastCue_SwitchesToTheBlastFall()
        {
            SetBlastClips();
            Update();
            SetLiving(3);
            Update();
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));

            Blast(new float3(0f, 0f, -3f));
            Update();

            Assert.IsTrue(Lower(3).Plays(Range(60, false)));
        }

        [Test]
        public void BlastTooOldOrTooFar_PlaysDie()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 30f));
            SetLiving(3);
            Update();
            Assert.IsTrue(Lower(3).Plays(Range(40, false)));

            SetLiving(4);
            Update();
            Blast(new float3(0f, 0f, 3f));
            Advance(SquadFigures.BlastAfter + 0.2f);
            SetLiving(1);
            Update();

            Assert.IsTrue(Lower(2).Plays(Range(40, false)));
        }

        [Test]
        public void SpawnWithDeadMembers_HidesTheirFiguresWithoutFalling()
        {
            SetLiving(2);
            Update();

            Assert.AreEqual(FigureState.Idle, State(1));
            Assert.AreEqual(FigureState.Hidden, State(2));
            Assert.IsFalse(Drawn(3));
        }

        [Test]
        public void Clip_FollowsSpeedThenFiresOnAShot()
        {
            Update();
            Assert.IsTrue(Lower(0).Plays(Range(0, true)));

            MoveFor(1f, 0.5f);
            Assert.IsTrue(Lower(0).Plays(Range(10, true)));

            MoveFor(5f, 0.5f);
            Assert.IsTrue(Lower(0).Plays(Range(20, true)));

            MoveFor(0f, 2f);
            Assert.IsTrue(Lower(0).Plays(Range(0, true)));

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();
            Assert.IsTrue(Lower(0).Plays(Range(0, true)), "the legs keep idling");
            Assert.IsTrue(Upper(0).Plays(Range(30, true)));

            Advance(SquadFigures.FireHold);
            Assert.IsTrue(Upper(0).Plays(Range(0, true)));
        }

        [Test]
        public void FiringWhileWalking_FiresWithTheUpperBodyOverWalkingLegs()
        {
            Update();
            MoveFor(1f, 0.5f);
            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Move(1f);

            Assert.IsTrue(Lower(0).Plays(Range(10, true)));
            Assert.IsTrue(Upper(0).Plays(Range(30, true)));

            MoveFor(1f, SquadFigures.FireHold + 0.1f);

            Assert.IsTrue(Upper(0).Plays(Range(10, true)));
            Assert.AreEqual(Lower(0).Value.w, Upper(0).Value.w, "the arms rejoin the stride in step");
        }

        [Test]
        public void Posture_PicksCrouchAndProneClipsForLegsAndFire()
        {
            Member(0, posture: Posture.Crouch);
            Member(1, posture: Posture.Prone);
            Update();

            Assert.IsTrue(Lower(0).Plays(Range(100, true)));
            Assert.IsTrue(Lower(1).Plays(Range(130, true)));

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();

            Assert.IsTrue(Upper(0).Plays(Range(120, true)));
            Assert.IsTrue(Lower(0).Plays(Range(100, true)));
        }

        [Test]
        public void OnlyChangedFigures_GetANewClip()
        {
            Update();
            var kept = Lower(0).Value;
            SetLiving(3);
            Update();

            Assert.AreEqual(kept, Lower(0).Value);
            Assert.AreNotEqual(kept.w, Lower(3).Value.w);
        }

        [Test]
        public void LoopingFigures_StartOutOfStep()
        {
            Update();

            Assert.AreNotEqual(Lower(1).Value.w, Lower(2).Value.w);
        }

        [Test]
        public void MixedTableSquad_PlaysEachFiguresOwnClips()
        {
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(1).Clips = Clips(100);

            Update();
            Assert.IsTrue(Lower(0).Plays(Range(0, true)));
            Assert.IsTrue(Lower(1).Plays(Range(100, true)));

            MoveFor(5f, 0.5f);
            Assert.IsTrue(Lower(0).Plays(Range(20, true)));
            Assert.IsTrue(Lower(1).Plays(Range(120, true)));
        }

        [Test]
        public void Authoring_ResolvesClipsFromEachFiguresOwnTable()
        {
            var root = new GameObject("Squad");
            try
            {
                var squad = root.AddComponent<SquadFiguresAuthoring>();
                squad.figures.Add(Figure(root, 0));
                squad.figures.Add(Figure(root, 100));
                squad.digClip = "";

                var clips = squad.Clips();

                Assert.AreEqual(Range(20, true), clips[0].Run);
                Assert.AreEqual(Range(120, true), clips[1].Run);
                Assert.AreEqual(Range(140, false), clips[1].Die);

                squad.figures[1].root.GetComponentInChildren<BoneAnimatedAuthoring>().clips.clips.RemoveAt(4);
                Assert.Throws<KeyNotFoundException>(() => squad.Clips());
            }
            finally
            {
                foreach (var figure in root.GetComponentsInChildren<BoneAnimatedAuthoring>())
                {
                    Object.DestroyImmediate(figure.clips);
                }

                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Authoring_FillsOnlyEmptyPostureClipsWithTheStandingOnes()
        {
            var root = new GameObject("Squad");
            try
            {
                var squad = root.AddComponent<SquadFiguresAuthoring>();
                squad.figures.Add(Figure(root, 0));
                squad.crouchWalkClip = "Run";
                squad.digClip = "";

                var clips = squad.Clips()[0];

                Assert.AreEqual(clips.Idle, clips.CrouchIdle);
                Assert.AreEqual(clips.Run, clips.CrouchWalk);
                Assert.AreEqual(clips.Fire, clips.ProneFire);
                Assert.AreEqual(clips.Idle, clips.Dig);

                squad.proneCrawlClip = "Missing";
                Assert.Throws<KeyNotFoundException>(() => squad.Clips());
            }
            finally
            {
                foreach (var figure in root.GetComponentsInChildren<BoneAnimatedAuthoring>())
                {
                    Object.DestroyImmediate(figure.clips);
                }

                Object.DestroyImmediate(root);
            }
        }

        // A figure baked on its own, with a body and a weapon sharing its table; its clip names match the squad's.
        private static SquadFiguresAuthoring.Figure Figure(GameObject root, int offset, string prefix = "")
        {
            var table = ScriptableObject.CreateInstance<BoneClipTable>();
            string[] names = { "Idle", "Walk", "Run", "Fire", "Die" };
            for (var i = 0; i < names.Length; i++)
            {
                table.clips.Add(new VATClipTable.Clip
                {
                    name = prefix + names[i], startRow = offset + i * 10, frameCount = 10, fps = 10f, loop = i < 4,
                });
            }

            var figure = new GameObject($"Figure {offset}");
            figure.transform.SetParent(root.transform);
            foreach (var part in new[] { "Body", "Weapon" })
            {
                var child = new GameObject(part);
                child.transform.SetParent(figure.transform);
                child.AddComponent<BoneAnimatedAuthoring>().clips = table;
            }

            return new SquadFiguresAuthoring.Figure { root = figure.transform, clipPrefix = prefix };
        }

        private Entity Squad(int count)
        {
            _figures = new Entity[count];
            var figures = new SquadFigure[count];
            _model = _em.CreateEntity(typeof(LocalToWorld));
            _em.SetComponentData(_model, new LocalToWorld { Value = float4x4.identity });
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

            var squad = _em.CreateEntity();
            var group = _em.AddBuffer<LinkedEntityGroup>(squad);
            group.Add(squad);
            foreach (var figure in _figures)
            {
                group.Add(figure);
            }

            _em.AddComponentData(squad, new Health { Current = 100f, Max = 100f });
            _em.AddComponentData(squad, new LocalToWorld { Value = float4x4.identity });
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

        private static FigureClips Clips(int offset) => new()
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

        private static FigureClips BlastClips() => new()
        {
            Die = Range(40, false),
            DieBlastFront = Range(50, false),
            DieBlastBack = Range(60, false),
            DieBlastLeft = Range(70, false),
            DieBlastRight = Range(80, false),
        };

        private static VATClipRange Range(int startRow, bool loop) =>
            new() { StartRow = startRow, FrameCount = 10, Fps = 10f, Loop = loop };

        private void Update()
        {
            _time += Step;
            _kit.World.SetTime(new TimeData(_time, Step));
            _system.Update(_kit.World.Unmanaged);
            _em.GetBuffer<CueEvent>(_cues).Clear();
        }

        // One update past the seconds, so rounding can't leave the clock just short.
        private void Advance(float seconds)
        {
            var steps = (int)math.ceil(seconds / Step) + 1;
            for (var i = 0; i < steps; i++)
            {
                Update();
            }
        }

        private void Move(float speed)
        {
            var place = _em.GetComponentData<LocalToWorld>(_squad);
            var position = place.Position + new float3(speed * Step, 0f, 0f);
            _em.SetComponentData(_squad, new LocalToWorld { Value = float4x4.Translate(position) });
            Update();
        }

        private void MoveFor(float speed, float seconds)
        {
            for (var i = 0; i < (int)math.ceil(seconds / Step); i++)
            {
                Move(speed);
            }
        }

        private SquadFigure Figure(int index) => _em.GetBuffer<SquadFigure>(_squad)[index];

        private void SetBlastClips()
        {
            var figures = _em.GetBuffer<SquadFigure>(_squad);
            for (var i = 0; i < figures.Length; i++)
            {
                figures.ElementAt(i).Clips = BlastClips();
            }
        }

        private void Blast(float3 position) => _em.GetBuffer<CueEvent>(_cues).Add(new CueEvent
        {
            Slot = CueSlot.Impact, Position = position, Radius = 4f,
        });

        // The first members stay alive, the rest are dead.
        private void SetLiving(int count)
        {
            var members = _em.GetBuffer<SquadMember>(_squad);
            for (var i = 0; i < members.Length; i++)
            {
                members.ElementAt(i).Health = i < count ? 25f : 0f;
            }
        }

        private void Member(int index, float2 offset = default, byte weapon = 0, Posture posture = Posture.Stand)
        {
            ref var member = ref _em.GetBuffer<SquadMember>(_squad).ElementAt(index);
            member.Offset = offset;
            member.Weapon = weapon;
            member.Posture = posture;
        }

        private FigureState State(int index) => _em.GetBuffer<SquadFigure>(_squad)[index].State;

        private VATClip Lower(int index) => _em.GetComponentData<LowerClip>(_figures[index]).Clip;

        private VATClip Upper(int index) => _em.GetComponentData<UpperClip>(_figures[index]).Clip;

        // A second figure for the member's upgraded weapon, baked hidden like the squad's others.
        private Entity AddUpgradedModel(int index, string weapon)
        {
            var upgraded = ModelPart();
            var figure = Figure(index);
            figure.Other = new FigureVariant { Figure = upgraded, Clips = Clips(1000), Weapon = weapon };
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(index) = figure;
            return upgraded;
        }

        private Entity AddDeployedModel()
        {
            var gun = ModelPart();
            var squad = _em.GetComponentData<SquadFigures>(_squad);
            squad.DeployedModel = gun;
            _em.SetComponentData(_squad, squad);
            return gun;
        }

        private Entity ModelPart()
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
            _em.GetBuffer<LinkedEntityGroup>(_squad).Add(part);
            return part;
        }

        private bool DrawnModel(Entity part) => _em.IsComponentEnabled<MaterialMeshInfo>(part);

        private bool Drawn(int index) => _em.IsComponentEnabled<MaterialMeshInfo>(_figures[index]);
    }
}
