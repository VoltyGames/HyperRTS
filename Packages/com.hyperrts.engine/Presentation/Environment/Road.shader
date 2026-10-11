// Road surface on a ribbon mesh laid over the terrain: U runs across the road (0..1), V along it in metres, and
// vertex alpha fades the ribbon out at its ends and junctions. The sides fade over Edge Fade of the half width,
// broken up by world-space noise so the road crumbles into the ground. URP PBR lighting with shadows, SSAO, DBuffer
// decals and light cookies. Alpha-blended without writing depth, in the opaque queue after the terrain: decals, SSAO
// and the fog of war (projected on the depth buffer) all see the terrain underneath, so they cover the road too.
// Needs mesh normals and tangents.
Shader "HyperRTS/Road"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Albedo (A = coverage)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        [Normal] [NoScaleOffset] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        [NoScaleOffset] _MaskMap ("Mask (G = occlusion, A = smoothness)", 2D) = "white" {}
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 1
        _TileLength ("Tile Length (m along the road)", Float) = 8
        _EdgeWidth ("Edge Fade (of the half width)", Range(0.01, 1)) = 0.3
        _EdgeNoise ("Edge Breakup", Range(0, 1)) = 0.6
        [NoScaleOffset] _NoiseMap ("Edge Noise", 2D) = "gray" {}
        _NoiseSize ("Edge Noise Size (m)", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Geometry+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Road"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            ZWrite Off
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _BumpScale;
                half _Smoothness;
                float _TileLength;
                half _EdgeWidth;
                half _EdgeNoise;
                float _NoiseSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half2 opacityAndFog : TEXCOORD4;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    half3 vertexLighting : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.normalWS = normals.normalWS;
                output.tangentWS = half4(normals.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.opacityAndFog = half2(input.color.a, ComputeFogFactor(position.positionCS.z));
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLighting = VertexLighting(position.positionWS, normals.normalWS);
                #endif
                return output;
            }

            // How much road covers the pixel. Toward the sides the noise decides, so gravel thins out in patches
            // instead of along a clean line; the base map's alpha and the vertex alpha fade it further.
            half Coverage(Varyings input, half albedoAlpha)
            {
                half inside = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                half fade = saturate(inside / _EdgeWidth);
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz / _NoiseSize).r;
                half crumble = saturate(fade * (1.0 + _EdgeNoise) - noise * _EdgeNoise);
                return smoothstep(0.0, 1.0, crumble) * albedoAlpha * input.opacityAndFog.x;
            }

            InputData Inputs(Varyings input, half3 normalTS)
            {
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                data.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent, input.normalWS);
                data.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, data.tangentToWorld));
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.opacityAndFog.y);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.bakedGI = SampleSH(data.normalWS);
                data.shadowMask = half4(1.0, 1.0, 1.0, 1.0);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    data.vertexLighting = input.vertexLighting;
                #endif
                return data;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = float2(input.uv.x, input.uv.y / _TileLength);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _Tint;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.alpha = Coverage(input, albedo.a);
                surface.normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                surface.occlusion = mask.g;
                surface.smoothness = mask.a * _Smoothness;

                InputData inputData = Inputs(input, surface.normalTS);
                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surface, inputData);
                #endif
                half4 color = UniversalFragmentPBR(inputData, surface);
                return half4(MixFog(color.rgb, inputData.fogCoord), surface.alpha);
            }
            ENDHLSL
        }
    }
}
