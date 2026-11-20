// Only TrampleBrush.cs draws with this, never a material.
Shader "Hidden/HyperRTS/TrampleBrush"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #pragma use_dxc
        #include "Packages/com.hyperrts.engine/Presentation/Environment/Ground/Stamps/TrampleBrushStamps.hlsl"
        ENDHLSL

        Pass
        {
            Name "TrampleDirection"
            ColorMask RG
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TrampleDirectionFragment
            ENDHLSL
        }

        // Flatten (B) and crop damage (A) only grow while stamped.
        Pass
        {
            Name "TrampleAmount"
            ColorMask BA
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TrampleAmountFragment
            ENDHLSL
        }

        Pass
        {
            Name "Tracks"
            ColorMask RG
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment TracksFragment
            ENDHLSL
        }

        // Scars only deepen: they last the match.
        Pass
        {
            Name "Scars"
            ColorMask GBA
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment ScarsFragment
            ENDHLSL
        }

        Pass
        {
            Name "ScarRims"
            ColorMask A
            BlendOp Min
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment ScarRimsFragment
            ENDHLSL
        }

        Pass
        {
            Name "FillCap"
            ColorMask A
            BlendOp Min
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment FillCapFragment
            ENDHLSL
        }

        Pass
        {
            Name "FillFloor"
            ColorMask A
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment FillFloorFragment
            ENDHLSL
        }

        Pass
        {
            Name "Wake"
            ColorMask R
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment WakeFragment
            ENDHLSL
        }

        // Stains only build up: they last the match.
        Pass
        {
            Name "Stains"
            BlendOp Max
            Blend One One
            HLSLPROGRAM
            #pragma vertex StampVertex
            #pragma fragment StainFragment
            ENDHLSL
        }

        Pass
        {
            Name "Fade"
            BlendOp RevSub
            Blend One One
            HLSLPROGRAM
            #pragma vertex FadeVertex
            #pragma fragment FadeFragment
            ENDHLSL
        }
    }
}
