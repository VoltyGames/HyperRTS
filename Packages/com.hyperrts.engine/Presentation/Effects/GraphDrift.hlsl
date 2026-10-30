// A VFX Graph Update block: moves particles at a constant world velocity on top of their own, as Shuriken's
// Velocity over Lifetime in World space does, so drag and bounces never take it away.
void DriftAtVelocity(inout VFXAttributes attributes, in float3 drift, in float deltaTime)
{
    attributes.position += drift * deltaTime;
}
