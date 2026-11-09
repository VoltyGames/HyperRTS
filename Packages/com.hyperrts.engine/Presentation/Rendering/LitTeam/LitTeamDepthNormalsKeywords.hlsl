// HyperRTS/LitTeam DepthNormals keywords: normal and smoothness output, rendering layers and instancing.
#pragma shader_feature_local _NORMALMAP
#pragma shader_feature_local _ALPHATEST_ON
#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
#pragma multi_compile _ LOD_FADE_CROSSFADE
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#pragma multi_compile _ _WRITE_SMOOTHNESS
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
