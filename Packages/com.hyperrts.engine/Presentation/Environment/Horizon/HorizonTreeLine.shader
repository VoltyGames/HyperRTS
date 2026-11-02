// The horizon's far belts, drawn by Horizon on HorizonTreeLine's ridges: the crowns' colour broken into crown-sized
// clumps of light and shade, darker toward the foot, with the sun wrapped round the crowns. Horizon sets _BaseColor to
// the tree's leaves as seen from afar: its leaf atlas weighted by coverage, times its tint.
Shader "HyperRTS/HorizonTreeLine"
{
    Properties
    {
        _BaseColor ("Crown Colour", Color) = (0.2, 0.25, 0.12, 1)
        _ClumpShade ("Crown Clump Shade", Range(0, 1)) = 0.35
        _ClumpSize ("Crown Clump Size (m)", Float) = 4
        _FootShade ("Shade At The Foot", Range(0, 1)) = 0.4
        _Wrap ("Sun Wrap", Range(0, 1)) = 0.5
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
        #include "Packages/com.hyperrts.engine/Presentation/Rendering/Hash.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _ClumpShade;
            float _ClumpSize;
            half _FootShade;
            half _Wrap;
        CBUFFER_END


        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float up : TEXCOORD2; // 0 at the foot, 1 at the crown's top
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.up = input.uv.y;
            return output;
        }

        half3 TreeLineAlbedo(Varyings input)
        {
            half clump = ValueNoise(input.positionWS / _ClumpSize);
            half shade = (1.0 + (clump - 0.5) * 2.0 * _ClumpShade) * lerp(1.0 - _FootShade, 1.0, input.up);
            return _BaseColor.rgb * shade;
        }
        ENDHLSL

        Pass
        {
            Name "HorizonTreeLine"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.hyperrts.engine/Presentation/Rendering/Keywords/SimpleLightingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Rendering/SceneLighting.hlsl"

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS), input.positionWS, half4(1.0, 1.0, 1.0, 1.0));
                half wrap = saturate((dot(normal, sun.direction) + _Wrap) / (1.0 + _Wrap));
                half3 direct = sun.color * (sun.shadowAttenuation * wrap);
                half3 ambient = SurfaceCacheAmbient(input.positionWS, normal, view, input.positionCS.xy);
                return half4(TreeLineAlbedo(input) * (direct + ambient), 1.0);
            }
            ENDHLSL
        }

        // Horizon draws the tree line without shadows; the pass keeps the prepasses complete.
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

            half3 MetaAlbedo(float2 uv0)
            {
                return _BaseColor.rgb;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
