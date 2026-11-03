// The horizon's tree lines, drawn by Horizon on HorizonBelts' upright quads: a side view of a belt, cut out, lit like the
// plants and shadowed by the sun, fading into the sky as the ground does. It casts no shadow. The strip texture covers
// _StripSize (metres along, metres tall) and tiles along the belt; the mesh's u runs in metres along it.
// Horizon sets the _Sky* properties and _TerrainRect, as HorizonSkyFade.hlsl documents them.
Shader "HyperRTS/HorizonBelt"
{
    Properties
    {
        [MainTexture] _BaseMap ("Strip (RGB, A Cutout)", 2D) = "white" {}
        [NoScaleOffset] [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _StripSize ("Strip Size (m along, m tall)", Vector) = (32, 12, 0, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Translucency ("Translucency", Range(0, 1)) = 0.3
        _ScreenOcclusion ("Screen Occlusion Share", Range(0, 1)) = 0.5
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

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);

        // PlantShading.hlsl reads the ground blend, hue and mip bias terms too; belts leave them at zero.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float4 _StripSize;
            half _Smoothness;
            half _Translucency;
            half _ScreenOcclusion;
            half _HueVariation;
            half _GroundBlend;
            half _GroundBlendHeight;
            float _MipBias;
            float4 _SkyTex_HDR;
            half4 _SkyTint;
            half _SkyExposure;
            float _SkyRotation;
            float _SkyBlur;
            float4 _SkyFade;
            float4 _TerrainRect;
        CBUFFER_END

        #include "Packages/com.hyperrts.engine/Presentation/Environment/Horizon/HorizonSkyFade.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half4 tangentWS : TEXCOORD3;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = float2(input.uv.x / _StripSize.x, input.uv.y);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
            return output;
        }

        half4 StripTexel(float2 uv)
        {
            half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            clip(texel.a - _Cutoff);
            return texel;
        }

        // The side in view faces the camera; the tangent and bitangent keep their world directions on both sides.
        half3 StripNormal(Varyings input, bool front)
        {
            half3 mapped = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
            half3 normal = normalize(input.normalWS);
            half3 tangent = normalize(input.tangentWS.xyz);
            half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
            half side = front ? 1.0 : -1.0;
            return normalize(tangent * mapped.x + bitangent * mapped.y + normal * (mapped.z * side));
        }
        ENDHLSL

        Pass
        {
            Name "HorizonBelt"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"

            // A belt is crowns seen from the side: wrapped, lit through and bent skyward like a two-sided leaf.
            #define PLANT_WRAP 1.0
            #define PLANT_TWO_SIDED 1.0
            #include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/PlantShading.hlsl"

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 texel = StripTexel(input.uv);
                half3 albedo = texel.rgb * (1.0 - PLANT_RAIN_DARKENING * _HyperRTSWetness);
                half3 normal = PlantNormal(StripNormal(input, front), true, 0.0);
                ForwardVaryings plant = (ForwardVaryings)0;
                plant.positionCS = input.positionCS;
                plant.positionWS = input.positionWS;
                plant.normalWS = normal;
                plant.tint = half4(1.0, 1.0, 1.0, 1.0);
                half3 lit = PlantLight(albedo, plant, normal, 0.0);
                return half4(HorizonSkyFade(lit, input.positionWS), 1.0);
            }
            ENDHLSL
        }

        // Horizon draws the belts without shadows; the pass keeps the prepasses complete.
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
                StripTexel(input.uv);
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
                StripTexel(input.uv);
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

            half4 Frag(Varyings input, bool front : SV_IsFrontFace) : SV_Target
            {
                StripTexel(input.uv);
                return DepthNormalOutput(StripNormal(input, front));
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

            half3 MetaAlbedo(float2 uv0)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv0).rgb * _BaseColor.rgb;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
