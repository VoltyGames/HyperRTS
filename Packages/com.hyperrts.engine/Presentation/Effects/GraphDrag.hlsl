// A VFX Graph Update block: takes drag metres a second off the speed every second until the particle stops, as
// Shuriken's Limit Velocity drag does, rather than VFX's drag in proportion to the speed.
void SlowDown(inout VFXAttributes attributes, in float drag, in float deltaTime)
{
    float speed = length(attributes.velocity);
    attributes.velocity *= max(speed - drag * deltaTime, 0.0) / max(speed, 0.0001);
}
