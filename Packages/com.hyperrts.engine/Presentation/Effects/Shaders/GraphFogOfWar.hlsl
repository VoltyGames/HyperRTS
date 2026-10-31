// A VFX Graph output block: the fog of war over each particle's centre, as the particle shaders lay it per pixel.
#include "Packages/com.hyperrts.engine/Presentation/Fog/FogOfWar.hlsl"

/// glow: 1 for additive layers, which dim out in the fog instead of taking its colour.
void ShadeInFogOfWar(inout VFXAttributes attributes, in float glow)
{
    half4 war = FogOfWarAt(attributes.position);
    float3 fogged = lerp(attributes.color, war.rgb, war.a);
    attributes.color = lerp(fogged, attributes.color * (1.0 - war.a), glow);
}
