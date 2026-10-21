// HyperRTS/Terrain micro detail: the TerrainMicroTexture soil grain near the camera, sampled twice at turned scales so
// its tile never lines up. Rough ground takes it fully, and the layers' low texels take its grit shadow most.
#ifndef HYPERRTS_TERRAIN_MICRO_INCLUDED
#define HYPERRTS_TERRAIN_MICRO_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/TerrainLayers.hlsl"

// (cos, sin) of one radian, and the golden ratio: the second sample's turn and scale.
#define MICRO_TURN float2(0.54030231, 0.84147098)
#define MICRO_SECOND_SCALE 1.618034

void ApplyMicro(inout TerrainSurface surface, TerrainPixel pixel)
{
    half fade = saturate((_MicroEnd - pixel.distance) / (_MicroEnd - _MicroStart));
    float2 st = pixel.xz / _MicroTiling;
    float2 dx = pixel.dx / _MicroTiling;
    float2 dy = pixel.dy / _MicroTiling;
    [branch] if (fade > 0.0)
    {
        half4 first = SAMPLE_TEXTURE2D_GRAD(_MicroMap, sampler_LinearRepeat, st, dx, dy);
        half4 second = SAMPLE_TEXTURE2D_GRAD(_MicroMap, sampler_LinearRepeat,
            RotateUV(st, MICRO_TURN) / MICRO_SECOND_SCALE, RotateUV(dx, MICRO_TURN) / MICRO_SECOND_SCALE,
            RotateUV(dy, MICRO_TURN) / MICRO_SECOND_SCALE);
        half2 slope = first.rg * 2.0 - 1.0 + RotateUV(second.rg * 2.0 - 1.0, MICRO_TURN * float2(1.0, -1.0));
        half grain = fade * (1.0 - surface.smoothness);
        surface.normalTS = normalize(half3(surface.normalTS.xy + slope * 0.5 * _MicroNormal * grain,
            surface.normalTS.z));
        half cavity = (first.b + second.b) * 0.5;
        surface.occlusion *= lerp(1.0, cavity, saturate(_MicroOcclusion * grain * (1.5 - surface.height)));
        surface.albedo *= 1.0 + (first.a + second.a - 1.0) * _MicroAlbedo * grain;
    }
}

#endif
