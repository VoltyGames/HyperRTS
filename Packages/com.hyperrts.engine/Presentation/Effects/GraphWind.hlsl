// A VFX Graph Update block: pushes particles downwind with the scene's Wind, as External Forces push Shuriken
// particles with a WindZone. share scales the push.
float3 _HyperRTSWindDirection;
float _HyperRTSWindStrength;

void DriftWithWind(inout VFXAttributes attributes, in float share, in float deltaTime)
{
    attributes.velocity += _HyperRTSWindDirection * (_HyperRTSWindStrength * share * deltaTime);
}
