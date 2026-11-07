using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Heights;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;

namespace HyperRTS.Simulation.Vision.Sight
{
    /// <summary>
    /// Draws each viewer's <see cref="SightArea"/> again when its place, eye, reach or team changed, a grid was built
    /// anew or blockers changed round it: sight occluded by terrain and <see cref="SightBlockers"/>, reaching further
    /// from <see cref="HighGround"/>, and the close layer round ground units. A passenger inside a container with a
    /// <see cref="PassengerEye"/> sees from above its roof. Each viewer writes only its own area, so it runs in parallel.
    /// </summary>
    [BurstCompile]
    internal partial struct SightAreaJob : IJobEntity
    {
        private static readonly ProfilerMarker DrawMarker = new("SightAreaJob.Draw");

        [ReadOnly] public FogOfWar Fog;
        public FactionRelations Relations;
        public TerrainHeight Terrain;
        [ReadOnly] public SightBlockers Blockers;
        [ReadOnly] public HighGround HighGround;
        public float WaterFloor;
        public float RevealDistance;
        [ReadOnly] public ComponentLookup<Inside> Inside;
        [ReadOnly] public ComponentLookup<SightBlock> Blocks;
        [ReadOnly] public ComponentLookup<PassengerEye> Eyes;
        [ReadOnly] public ComponentLookup<NavAgent> Agents;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;

        private void Execute(Entity entity, ref SightArea area, DynamicBuffer<SightAreaBits> bits,
            in VisionRange vision, in Faction faction)
        {
            var team = Relations.GetTeam(faction.Value);
            if (vision.Value <= 0f || !FogOfWar.Stamps(team))
            {
                area = new SightArea { Radius = -1 };
                bits.Clear();
                return;
            }

            var position = Transforms[entity].Position;
            var sight = new SightLines { Fog = Fog, Blockers = Blockers, WaterFloor = WaterFloor };
            var eye = Eye(entity, position, vision, ref sight);
            var wanted = Wanted(entity, position, eye, vision.Value * HighGround.SightScale(position), team);
            var dirty = Blockers.IsDirty(wanted.Origin, wanted.Origin + wanted.Width - 1);
            if (wanted.Matches(area) && !dirty)
            {
                return;
            }

            using var marker = DrawMarker.Auto();
            area = wanted;
            bits.ResizeUninitialized(area.Words);
            var words = bits.Reinterpret<ulong>().AsNativeArray();
            for (var i = 0; i < words.Length; i++)
            {
                words[i] = 0ul;
            }

            sight.Area = area;
            sight.Bits = words;
            sight.Draw(position, eye, area.Range);
            DrawClose(position, area, words);
        }

        /// <summary>The viewer's eye height; a viewer's own block, or the one it is inside, hides nothing from it.</summary>
        private readonly float Eye(Entity entity, float3 position, in VisionRange vision, ref SightLines sight)
        {
            var eye = math.max(position.y, Terrain.Height(position.xz)) + vision.EyeHeight;
            var holder = IsInside(entity, out var container) ? container : entity;
            if (!TryGetBlock(holder, out var block, out var transform))
            {
                return eye;
            }

            sight.Own = block.Box(transform);
            sight.HasOwn = true;
            if (holder == entity || !Eyes.TryGetComponent(holder, out var passenger))
            {
                return eye;
            }

            return math.max(eye, block.Top(transform) + passenger.AboveTop);
        }

        private readonly SightArea Wanted(Entity entity, float3 position, float eye, float range, byte team)
        {
            // Aircraft and passengers spot nothing up close.
            var airborne = Agents.TryGetComponent(entity, out var agent) && agent.Layer == NavLayer.Air;
            var close = airborne || IsInside(entity, out _) ? 0f : RevealDistance;
            return new SightArea
            {
                Center = Fog.Grid.Cell(position),
                Radius = (int)math.ceil(math.max(range, close) / Fog.Grid.CellSize),
                Eye = eye,
                Range = range,
                CloseRange = close,
                Team = team,
                FogGeneration = Fog.Generation,
                BlockersGeneration = Blockers.Generation,
            };
        }

        private readonly void DrawClose(float3 position, in SightArea area, NativeArray<ulong> words)
        {
            foreach (var cell in Fog.Grid.CellsInCircle(position.xz, area.CloseRange))
            {
                area.Set(words, SightArea.Close, cell);
            }
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
