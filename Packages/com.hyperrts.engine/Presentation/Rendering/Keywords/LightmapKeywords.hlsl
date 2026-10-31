// URP keywords for a forward pass that reads baked lightmaps and shadow masks.
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
#pragma multi_compile _ DYNAMICLIGHTMAP_ON
#pragma multi_compile _ USE_LEGACY_LIGHTMAPS
