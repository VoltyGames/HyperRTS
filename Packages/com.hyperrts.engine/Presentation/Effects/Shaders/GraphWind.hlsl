// A VFX Graph Update block: pushes particles downwind with the scene's Wind. share scales the push.
float4 _HyperRTSWindDirection;
float _HyperRTSWindStrength;

void DriftWithWind(inout VFXAttributes attributes, in float share, in float deltaTime)
{
    attributes.velocity += _HyperRTSWindDirection.xyz * (_HyperRTSWindStrength * share * deltaTime);
}
