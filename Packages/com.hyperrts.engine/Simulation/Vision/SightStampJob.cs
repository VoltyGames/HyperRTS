using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Heights;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Stamps every viewer's sight into <see cref="FogOfWar.Visible"/> for its team, occluded by terrain and
    /// <see cref="SightBlockers"/>. A passenger inside a container with a <see cref="PassengerEye"/> sees from above
    /// its roof. Single-threaded because overlapping sight circles OR into the same cells.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(LocalTransform))]
    internal partial struct SightStampJob : IJobEntity
    {
        public FogOfWar Fog;
        public FactionRelations Relations;
        public TerrainHeight Terrain;
        [ReadOnly] public SightBlockers Blockers;
        public float WaterFloor;
        [ReadOnly] public ComponentLookup<Inside> Inside;
        [ReadOnly] public ComponentLookup<SightBlock> Blocks;
        [ReadOnly] public ComponentLookup<PassengerEye> Eyes;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

        private void Execute(Entity entity, in VisionRange vision, in Faction faction)
        {
            if (vision.Value <= 0f || !FogOfWar.TryGetTeamBit(Relations.GetTeam(faction.Value), out var bit))
            {
                return;
            }

            var position = Transforms[entity].Position;
            var sight = new SightLines { Fog = Fog, Terrain = Terrain, Blockers = Blockers, WaterFloor = WaterFloor };
            var eye = math.max(position.y, Terrain.Height(position.xz)) + vision.EyeHeight;
            // A viewer's own block, or the one it is inside, hides nothing from it.
            var holder = IsInside(entity, out var container) ? container : entity;
            if (TryGetBlock(holder, out var block, out var transform))
            {
                sight.Own = block.Box(transform);
                sight.HasOwn = true;
                if (holder != entity && Eyes.TryGetComponent(holder, out var raised))
                {
                    eye = math.max(eye, block.Top(transform) + raised.AboveTop);
                }
            }

            sight.Stamp(position, eye, vision.Value, bit);
        }

        private readonly bool IsInside(Entity entity, out Entity container)
        {
            var inside = Inside.HasComponent(entity) && Inside.IsComponentEnabled(entity);
            container = inside ? Inside[entity].Container : Entity.Null;
            return inside;
        }

        private readonly bool TryGetBlock(Entity entity, out SightBlock block, out LocalTransform transform)
        {
            transform = default;
            return Blocks.TryGetComponent(entity, out block) && Blocks.IsComponentEnabled(entity) &&
                   Transforms.TryGetComponent(entity, out transform);
        }
    }
}
