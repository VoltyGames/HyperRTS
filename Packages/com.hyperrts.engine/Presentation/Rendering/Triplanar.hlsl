// Triplanar projection weights shared by the shaders that project textures from three sides.
#ifndef HYPERRTS_TRIPLANAR_INCLUDED
#define HYPERRTS_TRIPLANAR_INCLUDED

// Each axis's share for a unit normal, sharpened to the fourth power so the blend bands stay narrow; they sum to 1.
half3 TriplanarWeights(half3 normal)
{
    half3 weights = abs(normal);
    weights *= weights;
    weights *= weights;
    return weights / (weights.x + weights.y + weights.z);
}

#endif
