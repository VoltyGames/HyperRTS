// A VFX Graph Update block: pushes particles downwind with the scene's Wind, as External Forces push Shuriken
// particles with a WindZone. share scales the push; Drag sets how fast they reach the wind's speed.
float3 _HyperRTSWindDirection;
float _HyperRTSWindStrength;

void DriftWithWind(inout VFXAttributes attributes, in float share, in float deltaTime)
{
    attributes.velocity += _HyperRTSWindDirection * (_HyperRTSWindStrength * share * deltaTime);
}
