// URP lighting keywords for forward passes on the simple main-light shading of plants and flags: MainLightKeywords plus
// screen space occlusion and irradiance.
#include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/MainLightKeywords.hlsl"
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
