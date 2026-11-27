using System.Collections.Generic;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Ground.Holes
{
    /// <summary>
    /// Cuts the active terrain open under each finished building with a <see cref="TerrainHole"/> and fills it again
    /// when the building goes or is hidden, or the map changes. Visual only: the simulation's ground never changes.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TerrainHoleSystem : SystemBase
    {
        private readonly Dictionary<Entity, HoleFootprint> _wanted = [];
        private TerrainHoles _holes;

        protected override void OnDestroy() => Release();

        protected override void OnUpdate()
        {
            _wanted.Clear();
            foreach (var (hole, place, entity) in SystemAPI.Query<RefRO<TerrainHole>, RefRO<LocalToWorld>>()
                         .WithNone<ConstructionProgress, FogHidden>().WithEntityAccess())
            {
                var local = new float3(hole.ValueRO.Center.x, 0f, hole.ValueRO.Center.y);
                var center = math.transform(place.ValueRO.Value, local).xz;
                _wanted[entity] = new HoleFootprint(TurnedRect.Create(place.ValueRO.Value, center, hole.ValueRO.Size));
            }

            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Release();
                return;
            }

            if (_holes != null && _holes.Terrain != terrain)
            {
                Release();
            }

            if (_holes == null && _wanted.Count == 0)
            {
                return;
            }

            _holes ??= new TerrainHoles(terrain);
            _holes.Apply(_wanted);
        }

        private void Release()
        {
            _holes?.Dispose();
            _holes = null;
        }
    }
}
