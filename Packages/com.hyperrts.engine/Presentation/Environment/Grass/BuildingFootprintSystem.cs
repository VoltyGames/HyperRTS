using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Environment.Grass
{
    /// <summary>
    /// Keeps the <see cref="BuildingFootprintMask"/> in step with the buildings' obstacle boxes. It redraws only when
    /// buildings appear or go and their footprints differ, and holds nothing while no map is loaded.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BuildingFootprintSystem : SystemBase
    {
        private int _order = -1;
        private ulong _hash;

        public BuildingFootprintMask Mask { get; private set; }

        protected override void OnCreate() => RequireForUpdate<MapSettings>();

        protected override void OnUpdate()
        {
            var map = SystemAPI.GetSingleton<MapSettings>();
            if (Mask == null || !Mask.Fits(map))
            {
                Release();
                Mask = new BuildingFootprintMask(map);
            }

            var order = EntityManager.GetComponentOrderVersion<BuildingTag>();
            if (order == _order)
            {
                return;
            }

            _order = order;
            using var boxes = Boxes();
            var hash = Hash(boxes.AsArray());
            if (hash != _hash)
            {
                _hash = hash;
                Mask.Draw(boxes.AsArray());
            }
        }

        protected override void OnStopRunning() => Release();

        protected override void OnDestroy() => Release();

        private NativeList<NavBox> Boxes()
        {
            var boxes = new NativeList<NavBox>(64, Allocator.Temp);
            foreach (var (obstacle, transform) in SystemAPI.Query<RefRO<NavObstacle>, RefRO<LocalTransform>>()
                         .WithAll<BuildingTag>())
            {
                boxes.Add(new NavBox(transform.ValueRO.Position, transform.ValueRO.Rotation, obstacle.ValueRO.Size));
            }

            return boxes;
        }

        // Never 0, so a fresh mask always draws once, buildings or not.
        private static ulong Hash(NativeArray<NavBox> boxes)
        {
            var hash = 1469598103934665603ul;
            foreach (var box in boxes)
            {
                hash = (hash ^ math.hash(new float4(box.Center, box.Right))) * 1099511628211ul;
                hash = (hash ^ math.hash(box.Half)) * 1099511628211ul;
            }

            return hash == 0 ? 1 : hash;
        }

        private void Release()
        {
            Mask?.Dispose();
            Mask = null;
            _order = -1;
            _hash = 0;
        }
    }
}
