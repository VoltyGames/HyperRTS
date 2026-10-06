using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Bakes a Terrain's heights so units follow the ground and hills block sight. Put it on the Terrain, or, when
    /// the Terrain stays outside the SubScene, on an object at the Terrain's position. One per match.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Navigation + "Terrain Height")]
    [Icon(HyperRTSIcons.Navigation)]
    [HelpURL(HyperRTSDocs.Modules)]
    [DisallowMultipleComponent]
    public class TerrainHeightAuthoring : AuthoringBehaviour
    {
        /// <summary>Finer samples than this cost memory without helping unit-scale movement.</summary>
        private const float MinSpacing = 1f;

        [Tooltip("Heightmap to bake; empty uses the Terrain on this object. This transform is the Terrain's corner.")]
        public TerrainData terrainData;

        public class Baker : Baker<TerrainHeightAuthoring>
        {
            public override void Bake(TerrainHeightAuthoring authoring)
            {
                var data = authoring.terrainData;
                if (data == null)
                {
                    var terrain = GetComponent<Terrain>();
                    data = terrain != null ? terrain.terrainData : null;
                }

                if (data == null)
                {
                    return;
                }

                DependsOn(data);
                var height = Sample(data, GetComponent<Transform>().position);
                AddBlobAsset(ref height.Blob, out _);
                var sink = new BakerSink(this, GetEntity(TransformUsageFlags.None));
                NavSetup.AddTerrain(ref sink, height);
            }

            private static TerrainHeight Sample(TerrainData data, Vector3 origin)
            {
                var extent = new float2(data.size.x, data.size.z);
                var spacing = math.max(MinSpacing, math.min(data.heightmapScale.x, data.heightmapScale.z));
                var size = math.max((int2)math.ceil(extent / spacing) + 1, 2);
                var heights = new NativeArray<float>(size.x * size.y, Allocator.Temp);
                for (var y = 0; y < size.y; y++)
                {
                    for (var x = 0; x < size.x; x++)
                    {
                        var uv = math.saturate(new float2(x, y) * spacing / extent);
                        heights[y * size.x + x] = data.GetInterpolatedHeight(uv.x, uv.y) + origin.y;
                    }
                }

                var height = TerrainHeight.Create(heights, size, new float2(origin.x, origin.z), spacing,
                    Allocator.Persistent);
                heights.Dispose();
                return height;
            }
        }
    }
}
