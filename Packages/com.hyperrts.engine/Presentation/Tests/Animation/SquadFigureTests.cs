using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Presentation.Animation;
using HyperRTS.Presentation.Rendering.Visibility;
using HyperRTS.Simulation.Combat;
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
    /// Squad figures on a hand-made four-figure squad: health drops figures, motion and shots pick clips, and figures
    /// from separate tables play their own clips.
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
        public void VisibleCount_IsOneFigurePerShareOfHealthAndOneWhileAlive()
        {
            Assert.AreEqual(4, SquadFigures.VisibleCount(new Health { Current = 100f, Max = 100f }, 4));
            Assert.AreEqual(3, SquadFigures.VisibleCount(new Health { Current = 60f, Max = 100f }, 4));
            Assert.AreEqual(1, SquadFigures.VisibleCount(new Health { Current = 1f, Max = 100f }, 4));
            Assert.AreEqual(0, SquadFigures.VisibleCount(new Health { Current = 0f, Max = 100f }, 4));
        }

        [Test]
        public void DroppedFigure_PlaysDieThenHides()
        {
            Update();
            SetHealth(0.7f);
            Update();

            Assert.AreEqual(FigureState.Die, State(3));
            Assert.IsTrue(Clip(3).Plays(Range(40, false)));
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
            SetHealth(0.7f);
            Update();

            Assert.IsTrue(Clip(3).Plays(Range(40, false)));
        }

        [Test]
        public void DroppedFigure_FallsFromABlastNearTheSquad()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 3f));
            SetHealth(0.7f);
            Update();

            Assert.IsTrue(Clip(3).Plays(Range(50, false)));
        }

        [Test]
        public void FigureThatFellJustBeforeTheBlastCue_SwitchesToTheBlastFall()
        {
            SetBlastClips();
            Update();
            SetHealth(0.7f);
            Update();
            Assert.IsTrue(Clip(3).Plays(Range(40, false)));

            Blast(new float3(0f, 0f, -3f));
            Update();

            Assert.IsTrue(Clip(3).Plays(Range(60, false)));
        }

        [Test]
        public void BlastTooOldOrTooFar_PlaysDie()
        {
            SetBlastClips();
            Update();
            Blast(new float3(0f, 0f, 30f));
            SetHealth(0.7f);
            Update();
            Assert.IsTrue(Clip(3).Plays(Range(40, false)));

            SetHealth(1f);
            Update();
            Blast(new float3(0f, 0f, 3f));
            Advance(SquadFigures.BlastAfter + 0.2f);
            SetHealth(0.2f);
            Update();

            Assert.IsTrue(Clip(2).Plays(Range(40, false)));
        }

        [Test]
        public void DamagedSpawn_HidesItsLostFiguresWithoutFalling()
        {
            SetHealth(0.3f);
            Update();

            Assert.AreEqual(FigureState.Idle, State(1));
            Assert.AreEqual(FigureState.Hidden, State(2));
            Assert.IsFalse(Drawn(3));
        }

        [Test]
        public void Clip_FollowsSpeedThenFiresOnAShot()
        {
            Update();
            Assert.IsTrue(Clip(0).Plays(Range(0, true)));

            Move(1f);
            Assert.IsTrue(Clip(0).Plays(Range(10, true)));

            Move(5f);
            Assert.IsTrue(Clip(0).Plays(Range(20, true)));

            Move(0f);
            Assert.IsTrue(Clip(0).Plays(Range(0, true)));

            _em.GetBuffer<WeaponPose>(_squad).ElementAt(0).CountShot();
            Update();
            Assert.IsTrue(Clip(0).Plays(Range(30, true)));

            Advance(SquadFigures.FireHold);
            Assert.IsTrue(Clip(0).Plays(Range(0, true)));
        }

        [Test]
        public void OnlyChangedFigures_GetANewClip()
        {
            Update();
            var kept = Clip(0).Value;
            SetHealth(0.7f);
            Update();

            Assert.AreEqual(kept, Clip(0).Value);
            Assert.AreNotEqual(kept.w, Clip(3).Value.w);
        }

        [Test]
        public void LoopingFigures_StartOutOfStep()
        {
            Update();

            Assert.AreNotEqual(Clip(1).Value.w, Clip(2).Value.w);
        }

        [Test]
        public void MixedTableSquad_PlaysEachFiguresOwnClips()
        {
            _em.GetBuffer<SquadFigure>(_squad).ElementAt(1).Clips = Clips(100);

            Update();
            Assert.IsTrue(Clip(0).Plays(Range(0, true)));
            Assert.IsTrue(Clip(1).Plays(Range(100, true)));

            Move(5f);
            Assert.IsTrue(Clip(0).Plays(Range(20, true)));
            Assert.IsTrue(Clip(1).Plays(Range(120, true)));
        }

        [Test]
        public void Authoring_ResolvesClipsFromEachFiguresOwnTable()
        {
            var root = new GameObject("Squad");
            try
            {
                var squad = root.AddComponent<SquadFiguresAuthoring>();
                squad.figures.Add(Figure(root, 0, 64));
                squad.figures.Add(Figure(root, 100, 128));

                var clips = squad.Clips();

                Assert.AreEqual(Range(20, true), clips[0].Run);
                Assert.AreEqual(Range(120, true), clips[1].Run);
                Assert.AreEqual(Range(140, false), clips[1].Die);

                squad.figures[1].clips.clips.RemoveAt(4);
                Assert.Throws<KeyNotFoundException>(() => squad.Clips());
            }
            finally
            {
                foreach (var figure in root.GetComponentsInChildren<VATAnimatedAuthoring>())
                {
                    Object.DestroyImmediate(figure.clips);
                }

                Object.DestroyImmediate(root);
            }
        }

        // A figure baked on its own: its table's width and rows differ but its clip names match the squad's.
        private static VATAnimatedAuthoring Figure(GameObject root, int offset, int width)
        {
            var table = ScriptableObject.CreateInstance<VATClipTable>();
            table.width = width;
            string[] names = { "Idle", "Walk", "Run", "Fire", "Die" };
            for (var i = 0; i < names.Length; i++)
            {
                table.clips.Add(new VATClipTable.Clip
                {
                    name = names[i], startRow = offset + i * 10, frameCount = 10, fps = 10f, loop = i < 4,
                });
            }

            var figure = new GameObject($"Figure {offset}");
            figure.transform.SetParent(root.transform);
            var authoring = figure.AddComponent<VATAnimatedAuthoring>();
            authoring.clips = table;
            return authoring;
        }

        private Entity Squad(int count)
        {
            _figures = new Entity[count];
            var figures = new SquadFigure[count];
            for (var i = 0; i < count; i++)
            {
                _figures[i] = _em.CreateEntity(typeof(MaterialMeshInfo), typeof(HiddenBy));
                _em.AddComponentData(_figures[i], VATClip.Started(Range(0, true), 0f));
                _em.AddComponentData(_figures[i], VATFade.None);
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
            var writer = new EntityManagerWriter(_em, squad);
            var setup = new SquadFigures { RunSpeed = 3f, DieHold = DieHold };
            SquadFiguresAuthoring.AddSquad(ref writer, setup, figures);
            return squad;
        }

        private static FigureClips Clips(int offset) => new()
        {
            Idle = Range(offset, true),
            Walk = Range(offset + 10, true),
            Run = Range(offset + 20, true),
            Fire = Range(offset + 30, true),
            Die = Range(offset + 40, false),
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

        private void SetHealth(float fraction) =>
            _em.SetComponentData(_squad, new Health { Current = 100f * fraction, Max = 100f });

        private FigureState State(int index) => _em.GetBuffer<SquadFigure>(_squad)[index].State;

        private VATClip Clip(int index) => _em.GetComponentData<VATClip>(_figures[index]);

        private bool Drawn(int index) => _em.IsComponentEnabled<MaterialMeshInfo>(_figures[index]);
    }
}
