// Terrain height from its heightmap, bound by TerrainHeightInput.cs. Needs sampler_LinearClamp declared first.
#ifndef HYPERRTS_TERRAIN_HEIGHT_INCLUDED
#define HYPERRTS_TERRAIN_HEIGHT_INCLUDED

Texture2D<float> _Height;
float4 _HeightArea;  // terrain rect: x min, z min, 1 / size x, 1 / size z
float4 _HeightScale; // terrain base height, metres per unit sample, heightmap texels across

float GroundHeight(float2 xz)
{
    float texels = _HeightScale.z;
    float2 uv = (xz - _HeightArea.xy) * _HeightArea.zw;
    uv = uv * ((texels - 1.0) / texels) + 0.5 / texels;
    return _HeightScale.x + _Height.SampleLevel(sampler_LinearClamp, uv, 0) * _HeightScale.y;
}

#endif
