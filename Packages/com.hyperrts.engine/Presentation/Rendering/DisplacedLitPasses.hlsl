// One vertex entry, DisplacedLitVertex, for a URP pass whose object-space vertices a shader moves. Include after the
// URP pass file. Define first DISPLACED_LIT_INPUTS, extra vertex inputs with a leading comma on semantics that pass
// leaves free, and DisplaceLit(input, positionOS, normalOS), statements that move both in place and may read them.
#ifndef HYPERRTS_DISPLACED_LIT_PASSES_INCLUDED
#define HYPERRTS_DISPLACED_LIT_PASSES_INCLUDED

// URP's Attributes name position and normal differently from pass to pass.
#if defined(UNIVERSAL_FORWARD_LIT_PASS_INCLUDED)
    #define DISPLACED_LIT_PASS_VERTEX LitPassVertex
    #define DISPLACED_LIT_POSITION(input) input.positionOS.xyz
    #define DISPLACED_LIT_NORMAL(input) input.normalOS
#elif defined(UNIVERSAL_SHADOW_CASTER_PASS_INCLUDED)
    #define DISPLACED_LIT_PASS_VERTEX ShadowPassVertex
    #define DISPLACED_LIT_POSITION(input) input.positionOS.xyz
    #define DISPLACED_LIT_NORMAL(input) input.normalOS
#elif defined(UNIVERSAL_DEPTH_ONLY_PASS_INCLUDED)
    #define DISPLACED_LIT_PASS_VERTEX DepthOnlyVertex
    #define DISPLACED_LIT_POSITION(input) input.position.xyz
    #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
        #define DISPLACED_LIT_NORMAL(input) input.normalOS
    #endif
#elif defined(UNIVERSAL_FORWARD_LIT_DEPTH_NORMALS_PASS_INCLUDED)
    #define DISPLACED_LIT_PASS_VERTEX DepthNormalsVertex
    #define DISPLACED_LIT_POSITION(input) input.positionOS.xyz
    #define DISPLACED_LIT_NORMAL(input) input.normal
#elif defined(UNIVERSAL_OBJECT_MOTION_VECTORS_INCLUDED)
    #define DISPLACED_LIT_PASS_VERTEX vert
    #define DISPLACED_LIT_POSITION(input) input.position.xyz
#else
    #error "DisplacedLitPasses.hlsl: include a URP pass file first."
#endif

Varyings DisplacedLitVertex(Attributes input DISPLACED_LIT_INPUTS)
{
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(DISPLACED_LIT_NORMAL)
    DisplaceLit(input, DISPLACED_LIT_POSITION(input), DISPLACED_LIT_NORMAL(input));
#else
    // The pass reads no normal, so the compiler drops its deformation.
    float3 normalOS = float3(0.0, 1.0, 0.0);
    DisplaceLit(input, DISPLACED_LIT_POSITION(input), normalOS);
#endif
    return DISPLACED_LIT_PASS_VERTEX(input);
}

#endif
