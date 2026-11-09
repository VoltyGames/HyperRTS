// Flat-colour, alpha-blended overlay (selection rings, dials, command markers) drawn from _Instances by
// InstanceBuffer. _ZTest lets a material clone draw through scenery, so the local player's selection shows behind
// buildings.
Shader "HyperRTS/Overlay"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Overlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma use_dxc
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.hyperrts.engine/Presentation/Overlays/OverlayInstance.cs.hlsl"

            StructuredBuffer<OverlayInstance> _Instances;

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                nointerpolation half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                OverlayInstance instance = _Instances[input.instanceID];
                Varyings output;
                float3 positionWS = mul(instance.ObjectToWorld, float4(input.positionOS.xyz, 1.0)).xyz;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = instance.Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
