using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Rigs
{
    /// <summary>
    /// The motion rig at 1,300 driving tanks, each with a turret, gun, recoil, twelve road wheels and suspension, all
    /// firing now and then.
    /// </summary>
    [Category("Benchmark")]
    public class MotionRigBenchmarkTests
    {
        private const int Rigs = 1300;
        private const float Step = 1f / 60f;

        private World _world;
        private EntityManager _em;
        private SystemHandle _system;
        private Entity[] _units;
        private int _frame;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Motion Rig Benchmark");
            _em = _world.EntityManager;
            _system = _world.CreateSystem<MotionRigSystem>();
            _units = new Entity[Rigs];
            for (var i = 0; i < Rigs; i++)
            {
                _units[i] = Tank(new float3(i % 40 * 12f, 0f, i / 40 * 12f));
            }
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        [Test, Performance]
        public void Rigs_ThirteenHundredTanks()
        {
            var median = BenchmarkKit.Median("MotionRig", Pose, Drive);

            TestContext.WriteLine($"{Rigs} rigs: motion rig {median:F3} ms");
            Assert.Less(median, 4.0, $"median {median:F3} ms");
        }

        private void Pose()
        {
            _system.Update(_world.Unmanaged);
            _em.CompleteAllTrackedJobs();
        }

        // Every tank drives on a slow curve and one in five fires each frame.
        private void Drive()
        {
            _frame++;
            _world.SetTime(new TimeData(_frame * Step, Step));
            for (var i = 0; i < _units.Length; i++)
            {
                var place = _em.GetComponentData<LocalTransform>(_units[i]);
                place.Rotation = math.mul(place.Rotation, quaternion.RotateY(0.01f));
                place.Position += math.forward(place.Rotation) * 8f * Step;
                _em.SetComponentData(_units[i], place);
                if ((i + _frame) % 5 == 0)
                {
                    var poses = _em.GetBuffer<WeaponPose>(_units[i]);
                    poses[0] = new WeaponPose { Yaw = (byte)(_frame + i), Shots = (byte)(poses[0].Shots + 1) };
                }
            }
        }

        private Entity Tank(float3 position)
        {
            var parts = new MotionRigPart[15];
            parts[0] = new MotionRigPart { Kind = MotionPartKind.Turret, Axis = math.up() };
            parts[1] = new MotionRigPart { Kind = MotionPartKind.Elevation, Axis = math.right() };
            parts[2] = new MotionRigPart { Kind = MotionPartKind.Recoil, Axis = math.back(), Size = 0.4f, Time = 0.5f };
            for (var w = 3; w < parts.Length; w++)
            {
                var side = w % 2 == 0 ? 1.5f : -1.5f;
                parts[w] = new MotionRigPart
                {
                    Kind = MotionPartKind.Wheel, Axis = math.right(), Size = 0.4f,
                    RestPosition = new float3(side, 0.4f, (w - 3) / 2 * 1f - 3f),
                };
            }

            for (var p = 0; p < parts.Length; p++)
            {
                parts[p].Part = _em.CreateEntity();
                _em.AddComponentData(parts[p].Part, LocalTransform.FromPosition(parts[p].RestPosition));
                parts[p].RestRotation = quaternion.identity;
            }

            var hull = _em.CreateEntity();
            _em.AddComponentData(hull, LocalTransform.Identity);
            var unit = _em.CreateEntity();
            _em.AddComponentData(unit, LocalTransform.FromPosition(position));
            _em.AddBuffer<WeaponPose>(unit).Add(default);
            var rig = new MotionRig
            {
                TiltRoot = hull, TiltFrame = quaternion.identity, TiltRest = quaternion.identity,
                Footprint = new float2(7f, 3.5f), Suspension = Springs(),
            };
            MotionRigAuthoring.AddRig(new EntityWriter(_em, unit), rig, parts);
            MotionRigLink.AddAll(_em, unit);
            return unit;
        }

        private static Suspension Springs() => new()
        {
            Frequency = 1.5f,
            Damping = 0.45f,
            PerAcceleration = math.radians(0.6f),
            MaxSway = math.radians(3f),
            RecoilRate = math.radians(12f),
        };
    }
}
