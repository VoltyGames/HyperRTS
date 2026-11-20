// URP keywords a lit model's forward pass takes beside ForwardLightingKeywords: light falloff, box projected
// reflections, exposure, foveated rendering, debug views and probe volumes.
#pragma multi_compile _ _LIGHT_FALLOFF_QUADRATIC
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile _ _EXPOSURE
#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
#pragma multi_compile_fragment _ DEBUG_DISPLAY
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
