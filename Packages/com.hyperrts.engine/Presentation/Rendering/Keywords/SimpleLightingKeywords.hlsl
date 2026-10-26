// URP lighting keywords for forward passes on the simple main-light shading of plants and flags.
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
#pragma multi_compile_fragment _ _LIGHT_COOKIES
