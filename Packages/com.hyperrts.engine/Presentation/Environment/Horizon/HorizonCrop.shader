// The horizon's raised crop fields, drawn by Horizon on HorizonRaisedFields' boxes: the crop's canopy tile on the top
// with rows along the grid's x, its wall on the sides, lit as HyperRTS/RowCropCanopy lights its flat top, fading into
// the sky as the ground does. Where a pixel spans much of a field the crop fades to its mean colour and flat light,
// and the walls stop cutting out, so nothing sparkles. Horizon copies the canopy material's textures and values and sets:
//   _CanopyTile                 1 / the tile's metres across and along the rows
//   _GridTurn                   cos, sin of the horizon grid's turn
//   _MeanFootprint              metres of pixel footprint at which the crop has fully faded to its mean
//   _Sky*, _TerrainRect         as HorizonSkyFade.hlsl documents them
Shader "HyperRTS/HorizonCrop"
{
    Properties
    {
        [NoScaleOffset] _CanopyMap ("Canopy (RGB albedo, A height)", 2D) = "white" {}
        [NoScaleOffset] [Normal] _CanopyNormal ("Canopy Normal (X across the rows, Y along)", 2D) = "bump" {}
        [NoScaleOffset] _CanopyMask ("Canopy Mask (R occlusion, G coverage)", 2D) = "white" {}
        [NoScaleOffset] _WallMap ("Wall (RGBA, tiles along the edge)", 2D) = "white" {}
        [NoScaleOffset] [Normal] _WallNormal ("Wall Normal", 2D) = "bump" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Wall Alpha Cutoff", Range(0, 1)) = 0.45
        _Smoothness ("Smoothness", Range(0, 1)) = 0.12
        _Translucency ("Translucency", Range(0, 1)) = 0.45
        _HueVariation ("Hue Variation", Range(0, 0.5)) = 0.1
        _ScreenOcclusion ("Screen Occlusion", Range(0, 1)) = 0.2
        _CanopyShade ("Canopy Shade At The Roots", Range(0, 1)) = 0.6
        _BakedOcclusion ("Baked Occlusion Strength", Range(0, 1)) = 0.3
        _SunDepth ("Sun Fall-Off Into The Crop", Range(0, 8)) = 2
        [HideInInspector] _CanopyTile ("Canopy Tile", Vector) = (0.12, 0.12, 0, 0)
        [HideInInspector] _GridTurn ("Grid Turn", Vector) = (1, 0, 0, 0)
        [HideInInspector] _MeanFootprint ("Mean Footprint", Float) = 20
        [HideInInspector] [NoScaleOffset] _SkyTex ("Sky", Cube) = "grey" {}
        [HideInInspector] _SkyTint ("Sky Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        [HideInInspector] [Gamma] _SkyExposure ("Sky Exposure", Float) = 1
        [HideInInspector] _SkyRotation ("Sky Rotation", Float) = 0
        [HideInInspector] _SkyBlur ("Sky Blur", Float) = 4
        [HideInInspector] _SkyFade ("Sky Fade", Vector) = (600, 1000, 0, 0)
        [HideInInspector] _TerrainRect ("Terrain Rect", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_CanopyMap);
        TEXTURE2D(_CanopyNormal);
        TEXTURE2D(_CanopyMask);
        TEXTURE2D(_WallMap);
        TEXTURE2D(_WallNormal);
        // RowCropCanopy's samplers: the tile repeats both ways (URP's sampler_TrilinearRepeat), the wall only along.
        SAMPLER(sampler_TrilinearRepeatU_ClampV);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Cutoff;
            half _Smoothness;
            half _Translucency;
            half _HueVariation;
            half _ScreenOcclusion;
            half _CanopyShade;
            half _BakedOcclusion;
            half _SunDepth;
            float4 _CanopyTile;
            float4 _GridTurn;
            float _MeanFootprint;
            float4 _SkyTex_HDR;
            half4 _SkyTint;
            half _SkyExposure;
            float _SkyRotation;
            float _SkyBlur;
            float4 _SkyFade;
            float4 _TerrainRect;
        CBUFFER_END

        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonSkyFade.hlsl"
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonCrop.hlsl"
        ENDHLSL

        Pass
        {
            Name "HorizonCrop"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"

            #define PLANT_WRAP 1.0
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantLighting.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                CropSurface surface = CropSurfaceAt(input);
                half3 albedo = surface.albedo * (1.0 - PLANT_RAIN_DARKENING * _HyperRTSWetness);
                // Light under the heads falls off fast, as HyperRTS/RowCrop shades its plants.
                half occlusion = lerp(1.0 - _CanopyShade, 1.0, surface.leafHeight * surface.leafHeight);
                PlantPixel pixel = PlantPixelAt(input.positionWS, input.positionCS, occlusion, 1.0 - surface.coverage);
                pixel.sky = surface.sky;
                half3 toLight = GetMainLight().direction;
                pixel.sunShade = (half)exp2(-_SunDepth * (1.0 - surface.leafHeight) / max(toLight.y, 0.2));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 lit = PlantSurfaceLight(albedo, pixel, surface.normalWS, PlantPixelLights(pixel, surface.normalWS, view));
                return half4(HorizonSkyFade(lit, input.positionWS), 1.0);
            }
            ENDHLSL
        }

        // The walls cast onto the ground beside the field.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/ShadowCasterKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS = ShadowCasterPositionCS(output.positionWS, normalize(output.normalWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (OnWall(input))
                {
                    WallTexel(input, 0.0);
                }

                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half Frag(Varyings input) : SV_Target
            {
                if (OnWall(input))
                {
                    WallTexel(input, CropFar(input.positionWS));
                }

                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/DepthNormalsKeywords.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/CasterPasses.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                return DepthNormalOutput(CropSurfaceAt(input).normalWS);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // The boxes have no UVs; the crop's mean colour stands for all of them.
            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D_LOD(_CanopyMap, sampler_TrilinearRepeat, float2(0.5, 0.5), CROP_MEAN_MIP).rgb * _BaseColor.rgb;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
