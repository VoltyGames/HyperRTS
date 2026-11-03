// The horizon's ground, drawn by Horizon on its ring mesh: fields of the map terrain's own layers, each one crop with
// margins or tracks along its edges, lit by the sun and ambient light, fading into the sky over the ring's outer share.
// Far off, where a pixel spans much of a field, fields and margins fade to their weighted mean so the ground doesn't
// sparkle. Horizon sets every property:
//   _AlbedoArray, _LayerParams  the map terrain material's, as HyperRTS/Terrain documents them
//   _FieldGrid                  cell length along the rows, width across them (m); cos, sin of the grid's turn
//   _FieldMargin                margin and track layer; their share of the farmland's area
//   _FieldCells                 the column and grid row of _Fields' first cell; the crop count
//   _Fields                     HorizonFields' texture, laid out as HorizonFields.Apply documents
//   _Sky*, _TerrainRect         as HorizonSkyFade.hlsl documents them
Shader "HyperRTS/HorizonGround"
{
    Properties
    {
        [NoScaleOffset] _AlbedoArray ("Albedo (RGB)", 2DArray) = "" {}
        [NoScaleOffset] _LayerParams ("Layer Parameters", 2D) = "black" {}
        _Variation ("Field Shade Variation", Range(0, 0.5)) = 0.12
        _BlockVariation ("Block Shade Variation", Range(0, 0.5)) = 0.08
        [HideInInspector] _FieldGrid ("Field Grid", Vector) = (256, 144, 1, 0)
        [HideInInspector] _FieldMargin ("Field Margin", Vector) = (0, 0, 0, 0)
        [HideInInspector] _FieldCells ("Field Cells", Vector) = (0, 0, 0, 0)
        [HideInInspector] [NoScaleOffset] _Fields ("Fields", 2D) = "black" {}
        [HideInInspector] [NoScaleOffset] _SkyTex ("Sky", Cube) = "grey" {}
        [HideInInspector] _SkyTint ("Sky Tint", Color) = (0.5, 0.5, 0.5, 0.5)
        [HideInInspector] [Gamma] _SkyExposure ("Sky Exposure", Float) = 1
        [HideInInspector] _SkyTurn ("Sky Turn", Vector) = (1, 0, 0, 0)
        [HideInInspector] _SkyBlur ("Sky Blur", Float) = 4
        [HideInInspector] _SkyFade ("Sky Fade", Vector) = (600, 1000, 0, 0)
        [HideInInspector] _TerrainRect ("Terrain Rect", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _FieldGrid;
            float4 _FieldMargin;
            float4 _FieldCells;
            half _Variation;
            half _BlockVariation;
            float4 _SkyTex_HDR;
            half4 _SkyTint;
            half _SkyExposure;
            float4 _SkyTurn;
            float _SkyBlur;
            float4 _SkyFade;
            float4 _TerrainRect;
        CBUFFER_END

        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonSkyFade.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            return output;
        }

        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonFields.hlsl"
        ENDHLSL

        Pass
        {
            Name "HorizonGround"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = HorizonAlbedo(input.positionWS.xz);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                half3 direct = sun.color * (sun.shadowAttenuation * saturate(dot(normal, sun.direction)));
                half3 ambient = SurfaceCacheAmbient(input.positionWS, normal, view, input.positionCS.xy);
                return half4(HorizonSkyFade(albedo * (direct + ambient), input.positionWS), 1.0);
            }
            ENDHLSL
        }

        // Horizon draws the ground without shadows; the pass keeps the prepasses complete.
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
                return DepthNormalOutput(normalize(input.normalWS));
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/MetaPass.hlsl"

            // The ring has no UVs; its mean farmland colour stands for all of it.
            half3 MetaAlbedo(float2 uv0)
            {
                return FarmlandMean();
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
