// Triplanar projection weights and side projections shared by the shaders that project textures from three sides.
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

// 1 or -1 per axis: which way a normal faces along it.
half3 AxisSigns(half3 normal)
{
    return (half3)(normal >= 0.0) * 2.0 - 1.0;
}

// A world position or derivative on a side projection: side 0 looks along x onto (z, y), side 1 along z onto (x, y),
// each flipped by which way the surface faces so the texture never mirrors.
float2 SideProjection(float3 v, uint side, half3 axisSigns)
{
    return side == 0 ? float2(v.z * axisSigns.x, v.y) : float2(v.x * -axisSigns.z, v.y);
}

// A side's tangent-space normal in world space, by Golus's whiteout blend onto the surface normal.
half3 SideNormal(half3 t, uint side, half3 axisSigns, half3 normalWS)
{
    if (side == 0)
    {
        t.x *= axisSigns.x;
        return normalize(half3(t.xy + normalWS.zy, abs(t.z) * normalWS.x).zyx);
    }

    t.x *= -axisSigns.z;
    return normalize(half3(t.xy + normalWS.xy, abs(t.z) * normalWS.z));
}

#endif
