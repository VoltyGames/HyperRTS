// The screen-space dither every engine fade and stochastic trace clips against. Include after URP's Core.hlsl.
#ifndef HYPERRTS_DITHER_INCLUDED
#define HYPERRTS_DITHER_INCLUDED

// DitherSeedSystem steps it once per rendered frame.
float _HyperRTSDitherSeed;

// A [0, 1) threshold per pixel that moves every frame, so the temporal upscaler averages a dithered fade into a smooth
// one instead of keeping a fixed dot lattice. Shadow casters must not use it: a moving pattern shimmers in shadows.
float TemporalDither(float2 pixel)
{
    return InterleavedGradientNoise(pixel, (int)_HyperRTSDitherSeed);
}

#endif
