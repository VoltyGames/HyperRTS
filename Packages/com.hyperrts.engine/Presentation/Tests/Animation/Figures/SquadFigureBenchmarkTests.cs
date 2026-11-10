using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Tests;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Animation.Figures
{
    /// <summary>
    /// The squad figure system at 2,700 five-figure squads, about 13,000 figures, all walking and one in three firing.
    /// </summary>
    [Category("Benchmark")]
    public class SquadFigureBenchmarkTests : SquadFigureFixture
    {
        private const int Squads = 2700;

        private Entity[] _squads;

        [Test, Performance]
        public void Figures_TwoThousandSevenHundredSquads()
        {
            _squads = new Entity[Squads];
            for (var i = 0; i < Squads; i++)
            {
                _squads[i] = Squad(5);
                _em.SetComponentData(_squads[i], LocalTransform.FromPosition(new float3(i % 60 * 8f, 0f, i / 60 * 8f)));
            }

            var median = BenchmarkKit.Median("SquadFigures", Pose, Walk);

            TestContext.WriteLine($"{Squads} squads: squad figures {median:F3} ms");
            Assert.Less(median, 4.0, $"median {median:F3} ms");
        }

        private void Pose()
        {
            _system.Update(_kit.World.Unmanaged);
            _em.CompleteAllTrackedJobs();
        }

        private void Walk()
        {
            _time += Step;
            _kit.World.SetTime(new TimeData(_time, Step));
            for (var i = 0; i < _squads.Length; i++)
            {
                var place = _em.GetComponentData<LocalTransform>(_squads[i]);
                place.Position.x += 1.5f * Step;
                _em.SetComponentData(_squads[i], place);
                if ((i + (int)(_time / Step)) % 3 == 0)
                {
                    var poses = _em.GetBuffer<WeaponPose>(_squads[i]);
                    poses[0] = new WeaponPose { Shots = (byte)(poses[0].Shots + 1) };
                }
            }

            _placement.Update(_kit.World.Unmanaged);
            _parents.Update(_kit.World.Unmanaged);
            _matrices.Update(_kit.World.Unmanaged);
            _em.CompleteAllTrackedJobs();
        }
    }
}
