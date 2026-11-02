// The horizon's ground, drawn by Horizon on its ring mesh: large fields of the map terrain's own layers, each field one
// crop with a margin along its edges, lit by the sun and ambient light. Far off, where a pixel spans much of a field,
// fields and margins fade to their weighted mean so the ground doesn't sparkle. Horizon sets every property:
//   _AlbedoArray, _LayerParams  the map terrain material's, as HyperRTS/Terrain documents them
//   _FieldGrid                  field length along the rows, width across them (m); cos, sin of the grid's turn
//   _FieldMargin                margin width (m); margin layer; crop count; seed (24 bits)
//   _CropLayersLow, High        each crop's layer
//   _CropRollsLow, High         each crop's running share of the weights, 1 from the last crop on
Shader "HyperRTS/HorizonGround"
{
    Properties
    {
        [NoScaleOffset] _AlbedoArray ("Albedo (RGB)", 2DArray) = "" {}
        [NoScaleOffset] _LayerParams ("Layer Parameters", 2D) = "black" {}
        _Variation ("Field Shade Variation", Range(0, 0.5)) = 0.12
        _VariationSize ("Broad Shade Size (m)", Float) = 600
        [HideInInspector] _FieldGrid ("Field Grid", Vector) = (256, 144, 1, 0)
        [HideInInspector] _FieldMargin ("Field Margin", Vector) = (5, 0, 1, 0)
        [HideInInspector] _CropLayersLow ("Crop Layers 0 to 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CropLayersHigh ("Crop Layers 4 to 7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _CropRollsLow ("Crop Rolls 0 to 3", Vector) = (1, 1, 1, 1)
        [HideInInspector] _CropRollsHigh ("Crop Rolls 4 to 7", Vector) = (1, 1, 1, 1)
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
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonFields.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _FieldGrid;
            float4 _FieldMargin;
            float4 _CropLayersLow;
            float4 _CropLayersHigh;
            float4 _CropRollsLow;
            float4 _CropRollsHigh;
            half _Variation;
            float _VariationSize;
        CBUFFER_END

        TEXTURE2D_ARRAY(_AlbedoArray);
        SAMPLER(sampler_AlbedoArray);
        TEXTURE2D(_LayerParams);

        // Where a pixel spans this share of a field's width, fields have fully faded to their mean.
        #define HORIZON_MEAN_FOOTPRINT 0.3
        #define HORIZON_MEAN_MIP 16.0

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

        float CropLayer(uint crop)
        {
            float layers[8] = { _CropLayersLow.x, _CropLayersLow.y, _CropLayersLow.z, _CropLayersLow.w,
                _CropLayersHigh.x, _CropLayersHigh.y, _CropLayersHigh.z, _CropLayersHigh.w };
            return layers[crop];
        }

        // HyperRTS/Terrain's RotateUV: (cos, sin) of a clockwise turn.
        float2 Turn(float2 uv, float2 rotation)
        {
            return float2(uv.x * rotation.x - uv.y * rotation.y, uv.x * rotation.y + uv.y * rotation.x);
        }

        // A layer's albedo and tint as the terrain tiles it, row layers laid along the fields' rows.
        half3 LayerAlbedo(float layer, float2 positionXZ)
        {
            float4 mapping = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 0));
            half3 tint = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1)).rgb;
            float rowLayer = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 3)).w == 0.0;
            float2 rowTurn = float2(_FieldGrid.z, -_FieldGrid.w);
            float2 rotation = rowLayer > 0.0 ? Turn(mapping.xy, rowTurn) : mapping.xy;
            float2 uv = Turn(positionXZ, rotation) * mapping.zw;
            return SAMPLE_TEXTURE2D_ARRAY(_AlbedoArray, sampler_AlbedoArray, uv, layer).rgb * tint;
        }

        // A layer's mean colour, from its smallest mip.
        half3 LayerMean(float layer)
        {
            half3 tint = LOAD_TEXTURE2D(_LayerParams, uint2(layer, 1)).rgb;
            return SAMPLE_TEXTURE2D_ARRAY_LOD(_AlbedoArray, sampler_AlbedoArray, float2(0.5, 0.5), layer,
                HORIZON_MEAN_MIP).rgb * tint;
        }

        // The share of a field its margins take.
        float MarginShare()
        {
            float2 size = _FieldGrid.xy;
            return saturate(2.0 * _FieldMargin.x * (size.x + size.y) / (size.x * size.y));
        }

        // The weighted mean of the crops and margins, what a field-sized pixel sees.
        half3 FarmlandMean()
        {
            half3 crops = 0.0;
            float rolled = 0.0;
            float rolls[8] = { _CropRollsLow.x, _CropRollsLow.y, _CropRollsLow.z, _CropRollsLow.w,
                _CropRollsHigh.x, _CropRollsHigh.y, _CropRollsHigh.z, _CropRollsHigh.w };
            for (uint i = 0; i < (uint)_FieldMargin.z; i++)
            {
                crops += LayerMean(CropLayer(i)) * (rolls[i] - rolled);
                rolled = rolls[i];
            }

            return lerp(crops, LayerMean(_FieldMargin.y), MarginShare());
        }

        half3 HorizonAlbedo(float2 positionXZ)
        {
            float2 size = _FieldGrid.xy;
            uint seed = (uint)_FieldMargin.w;
            HorizonField field = FieldAt(positionXZ, size, _FieldGrid.zw, seed);
            float2 footprint = fwidth(field.grid);
            float blur = max(footprint.x, footprint.y);
            float margin = _FieldMargin.x;
            half marginWeight = 1.0 - smoothstep(margin - blur, margin + blur, FieldEdge(field, size));

            half3 crop = LayerAlbedo(CropLayer(FieldCrop(field, seed, _CropRollsLow, _CropRollsHigh)), positionXZ);
            half3 near = lerp(crop, LayerAlbedo(_FieldMargin.y, positionXZ), marginWeight);
            near *= 1.0 + (FieldShade(field, seed) - 0.5) * 2.0 * _Variation;

            half far = saturate(blur / (min(size.x, size.y) * HORIZON_MEAN_FOOTPRINT));
            [branch] if (far > 0.0)
            {
                near = lerp(near, FarmlandMean(), far);
            }

            half broad = ValueNoise(positionXZ / _VariationSize);
            return near * (1.0 + (broad - 0.5) * _Variation);
        }
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
                return half4(albedo * (direct + ambient), 1.0);
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
