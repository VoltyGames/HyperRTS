// HyperRTS/Terrain triplanar sides for the slope layer: world x and z projections on steep ground, so ravine walls
// keep their texel size instead of stretching downhill. Normals use Golus's whiteout blend with flipped sides.
#ifndef HYPERRTS_TERRAIN_TRIPLANAR_INCLUDED
#define HYPERRTS_TERRAIN_TRIPLANAR_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayerParams.hlsl"
#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainPixel.hlsl"

struct TerrainSides
{
    float2 uv[2];      // x projection on (z, y), z projection on (x, y)
    float2 dx[2];
    float2 dy[2];
    half2 weights;     // each projection's share of the layer
    half3 axisSign;
    float slice;
};

TerrainSides GetSides(TerrainPixel pixel, TerrainLayerParams params, float slice)
{
    TerrainSides sides;
    sides.axisSign = (half3)(pixel.frame[2] >= 0.0) * 2.0 - 1.0;
    float2 flip = float2(sides.axisSign.x, -sides.axisSign.z);
    sides.uv[0] = float2(pixel.positionWS.z * flip.x, pixel.positionWS.y) * params.tiling;
    sides.dx[0] = float2(pixel.dPdx.z * flip.x, pixel.dPdx.y) * params.tiling;
    sides.dy[0] = float2(pixel.dPdy.z * flip.x, pixel.dPdy.y) * params.tiling;
    sides.uv[1] = float2(pixel.positionWS.x * flip.y, pixel.positionWS.y) * params.tiling;
    sides.dx[1] = float2(pixel.dPdx.x * flip.y, pixel.dPdx.y) * params.tiling;
    sides.dy[1] = float2(pixel.dPdy.x * flip.y, pixel.dPdy.y) * params.tiling;
    #ifdef TERRAIN_TRIPLANAR
        sides.weights = pixel.side * params.triplanar * pixel.sideWeights;
    #else
        sides.weights = 0.0;
    #endif
    sides.slice = slice;
    return sides;
}

// The sides' weighted sum.
half4 SampleSides(TEXTURE2D_ARRAY_PARAM(layers, samplerLayers), TerrainSides sides)
{
    half4 value = 0.0;
    [unroll] for (uint i = 0; i < 2; i++)
    {
        [branch] if (sides.weights[i] > HEX_MIN_WEIGHT)
        {
            value += sides.weights[i] * SAMPLE_TEXTURE2D_ARRAY_GRAD(layers, samplerLayers, sides.uv[i], sides.slice,
                sides.dx[i], sides.dy[i]);
        }
    }

    return value;
}

// The sides' weighted normals in terrain tangent space.
half3 SampleSideNormals(TerrainSides sides, TerrainPixel pixel, half strength)
{
    half3 normalWS = pixel.frame[2];
    half3 world = 0.0;
    [branch] if (sides.weights.x > HEX_MIN_WEIGHT)
    {
        half3 t = UnpackNormalScale(SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, sides.uv[0],
            sides.slice, sides.dx[0], sides.dy[0]), strength);
        t.x *= sides.axisSign.x;
        t = half3(t.xy + normalWS.zy, abs(t.z) * normalWS.x);
        world += sides.weights.x * normalize(t.zyx);
    }

    [branch] if (sides.weights.y > HEX_MIN_WEIGHT)
    {
        half3 t = UnpackNormalScale(SAMPLE_TEXTURE2D_ARRAY_GRAD(_NormalArray, sampler_AlbedoArray, sides.uv[1],
            sides.slice, sides.dx[1], sides.dy[1]), strength);
        t.x *= -sides.axisSign.z;
        t = half3(t.xy + normalWS.xy, abs(t.z) * normalWS.z);
        world += sides.weights.y * normalize(t);
    }

    return mul(pixel.frame, world);
}

// The top projection's sample with the sides mixed in by their share.
half4 WithSides(half4 top, TEXTURE2D_ARRAY_PARAM(layers, samplerLayers), TerrainSides sides)
{
    half share = sides.weights.x + sides.weights.y;
    [branch] if (share > 0.0)
    {
        top = top * (1.0 - share) + SampleSides(TEXTURE2D_ARRAY_ARGS(layers, samplerLayers), sides);
    }

    return top;
}

half3 NormalWithSides(half3 top, TerrainSides sides, TerrainPixel pixel, half strength)
{
    half share = sides.weights.x + sides.weights.y;
    [branch] if (share > 0.0)
    {
        top = normalize(top * (1.0 - share) + SampleSideNormals(sides, pixel, strength));
    }

    return top;
}

#endif
