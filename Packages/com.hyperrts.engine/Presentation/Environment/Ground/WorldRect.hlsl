// World-rect and heightmap lookups shared by the terrain, crater and trample shaders.
#ifndef HYPERRTS_WORLD_RECT_INCLUDED
#define HYPERRTS_WORLD_RECT_INCLUDED

// UV in a packed world rect: xy min x and z, zw 1 / width and 1 / depth.
float2 RectUV(float4 rect, float2 positionXZ)
{
    return (positionXZ - rect.xy) * rect.zw;
}

// area is a world rect; scale is base height, metres per unit sample, heightmap texels across.
float SampleHeightmap(Texture2D<float> heights, SamplerState heightSampler, float4 area, float4 scale, float2 positionXZ)
{
    float texels = scale.z;
    float2 uv = RectUV(area, positionXZ);
    uv = uv * ((texels - 1.0) / texels) + 0.5 / texels;
    return scale.x + heights.SampleLevel(heightSampler, uv, 0) * scale.y;
}

#endif
