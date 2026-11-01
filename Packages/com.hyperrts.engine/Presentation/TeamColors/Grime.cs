using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>HyperRTS/LitTeam's grime on a render entity: x dust, y mud, z wet, w soot, each 0 to 1.</summary>
    [MaterialProperty("_Grime")]
    public struct GrimeLevels : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// HyperRTS/LitTeam's grime band on a render entity: x the unit's ground height in this mesh's object space,
    /// y the mud top and z the dust top above it, in object-space units.
    /// </summary>
    [MaterialProperty("_GrimeBand")]
    public struct GrimeBand : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// A ground vehicle's dirt: dust and mud from the ground it drives on, wet from water, soot from damage. Client-only,
    /// written to <see cref="GrimeLevels"/> on every mesh under it.
    /// </summary>
    public struct Grime : IComponentData
    {
        /// <summary>How far a level must move from what the meshes show before they are rewritten.</summary>
        public const float PaintStep = 0.02f;

        /// <summary>Ground wetness from which Soil and Grass turn to mud.</summary>
        public const float WetGround = 0.5f;

        public float Dust;
        public float Mud;
        public float Wet;
        public float Soot;

        public float DustPer100m;
        public float MudPer100m;
        public float DryingSeconds;
        public float DustWashPerSecond;
        public float MudWashPerSecond;

        /// <summary>Metres above the ground where mud and dust end.</summary>
        public float MudHeight;
        public float DustHeight;

        public float3 LastPosition;

        /// <summary>False until the first <see cref="Advance"/>, which only notes where the unit stands.</summary>
        public bool Placed;

        /// <summary>The levels last written to the meshes.</summary>
        public float4 Painted;

        /// <summary>A ground vehicle's usual rates and heights, the authoring defaults.</summary>
        public static Grime Standard => new()
        {
            DustPer100m = 0.25f,
            MudPer100m = 0.4f,
            DryingSeconds = 60f,
            DustWashPerSecond = 0.5f,
            MudWashPerSecond = 0.1f,
            MudHeight = 0.7f,
            DustHeight = 1.6f,
        };

        public readonly float4 Levels => new(Dust, Mud, Wet, Soot);

        /// <summary>Dust comes off dry Soil, Gravel and Sand.</summary>
        public static bool RaisesDust(SurfaceMaterial ground, float wetness)
        {
            var dry = wetness < WetGround;
            return ground is SurfaceMaterial.Gravel or SurfaceMaterial.Sand || (ground == SurfaceMaterial.Soil && dry);
        }

        /// <summary>Mud comes off Mud, and off Soil and Grass once the ground is wet.</summary>
        public static bool RaisesMud(SurfaceMaterial ground, float wetness)
        {
            var wet = wetness >= WetGround;
            var soaks = ground is SurfaceMaterial.Soil or SurfaceMaterial.Grass;
            return ground == SurfaceMaterial.Mud || (soaks && wet);
        }

        /// <summary>
        /// Moves the levels by one frame: the ground covered, the ground under the unit, how wet the weather has made
        /// it, and the health left. True when the meshes are out of date.
        /// </summary>
        public bool Advance(float3 position, SurfaceMaterial ground, float wetness, float healthFraction,
            float deltaTime)
        {
            var driven = Placed ? math.distance(position.xz, LastPosition.xz) : 0f;
            LastPosition = position;
            Placed = true;

            var per100m = driven / 100f;
            if (RaisesDust(ground, wetness))
            {
                Dust += per100m * DustPer100m;
            }

            if (RaisesMud(ground, wetness))
            {
                Mud += per100m * MudPer100m;
            }

            if (ground == SurfaceMaterial.Water)
            {
                Wet = 1f;
                Dust -= DustWashPerSecond * deltaTime;
                Mud -= MudWashPerSecond * deltaTime;
            }
            else
            {
                Wet -= deltaTime / DryingSeconds;
            }

            Soot = math.max(Soot, 1f - healthFraction);
            Dust = math.saturate(Dust);
            Mud = math.saturate(Mud);
            Wet = math.saturate(Wet);
            Soot = math.saturate(Soot);
            return math.any(math.abs(Levels - Painted) > PaintStep);
        }

        /// <summary>The levels to write to the meshes, noted as shown.</summary>
        public float4 Paint()
        {
            Painted = Levels;
            return Painted;
        }
    }
}
