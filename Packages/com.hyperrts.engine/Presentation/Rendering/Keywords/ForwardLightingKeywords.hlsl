// URP lighting keywords for forward passes on the full Lit model: LightingKeywords plus ambient probes, reflection
// probes, fog and rendering layers.
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/LightingKeywords.hlsl"
#pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
#pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
