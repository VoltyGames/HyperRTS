// HyperRTS/LitTeam ForwardLit keywords: material features, lighting, lightmaps, LOD cross-fade and instancing.
#pragma shader_feature_local _NORMALMAP
#pragma shader_feature_local_fragment _ALPHATEST_ON
#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
#pragma shader_feature_local_fragment _EMISSION
#pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
#pragma shader_feature_local_fragment _OCCLUSIONMAP
#pragma shader_feature_local_fragment _DETAIL
#pragma shader_feature_local _INTERIOR
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ForwardLightingKeywords.hlsl"
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ObjectLightingKeywords.hlsl"
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/LightmapKeywords.hlsl"
#pragma multi_compile _ LOD_FADE_CROSSFADE
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/InstancingKeywords.hlsl"
#pragma instancing_options renderinglayer
