using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Where squad members away from their cover spots, crew places and a site's work line may stand: on cells open to
    /// their squad and off vehicle hulls. Run it every tick with <see cref="TestWorld.CheckEveryTick"/>.
    /// </summary>
    public sealed class SquadFootingCheck
    {
        private readonly TestWorld _world;

        // Made once and read without completing every job, which would invalidate the buffers a test holds across ticks.
        private readonly EntityQuery _squads;
        private readonly EntityQuery _units;
        private readonly EntityQuery _grid;

        public SquadFootingCheck(TestWorld world)
        {
            _world = world;
            _squads = world.Query(typeof(SquadMember), typeof(NavAgent), typeof(LocalTransform));
            _units = world.Query(typeof(NavAgent), typeof(LocalTransform));
            _grid = world.Query(typeof(NavGrid));
        }

        /// <summary>Fails when a member stands in a cell closed to its squad.</summary>
        public void AssertOnOpenGround()
        {
            var grid = _grid.GetSingleton<NavGrid>();
            using var squads = _squads.ToEntityArray(Allocator.Temp);
            foreach (var squad in squads)
            {
                if (!IsPlaced(squad) || !_world.Get<NavAgent>(squad).TryAccess(grid, out var access))
                {
                    continue;
                }

                var centre = _world.GetPosition(squad);
                foreach (var member in _world.Buffer<SquadMember>(squad))
                {
                    var cell = grid.Grid.Cell(member.Position(centre));
                    var open = !IsFree(member) || access.IsOpen(cell);
                    Assert.IsTrue(open, $"a member of squad {squad} stands in closed cell {cell}");
                }
            }
        }

        /// <summary>Fails when a member stands under a vehicle's hull.</summary>
        public void AssertOffHulls()
        {
            using var units = _units.ToEntityArray(Allocator.Temp);
            using var squads = _squads.ToEntityArray(Allocator.Temp);
            foreach (var unit in units)
            {
                var agent = _world.Get<NavAgent>(unit);
                if (agent.IsVehicle)
                {
                    AssertOffHull(agent, _world.Get<LocalTransform>(unit), squads);
                }
            }
        }

        private void AssertOffHull(in NavAgent agent, in LocalTransform hull, NativeArray<Entity> squads)
        {
            var axis = agent.SegmentAxis(hull.Rotation);
            var centre = hull.Position.xz;
            foreach (var squad in squads)
            {
                if (!IsPlaced(squad))
                {
                    continue;
                }

                var place = _world.GetPosition(squad);
                foreach (var member in _world.Buffer<SquadMember>(squad))
                {
                    var point = member.Position(place).xz;
                    var depth = agent.Radius - Planar.PointSegmentDistance(point, centre - axis, centre + axis);
                    var clear = !IsFree(member) || depth <= 0f;
                    Assert.IsTrue(clear, $"a member of squad {squad} stands {depth:F2} m under a hull");
                }
            }
        }

        // Squads inside a container don't walk, and diggers walk to their site's work line.
        private bool IsPlaced(Entity squad) => !IsEnabled<Inside>(squad) && !IsEnabled<SquadWork>(squad);

        private bool IsEnabled<T>(Entity entity) where T : unmanaged, IComponentData, IEnableableComponent =>
            _world.EntityManager.HasComponent<T>(entity) && _world.IsEnabled<T>(entity);

        private static bool IsFree(in SquadMember member) => member.Alive && !member.HasSpot && !member.TakesCrewPlace;
    }
}
