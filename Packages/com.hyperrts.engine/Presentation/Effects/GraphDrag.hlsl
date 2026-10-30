// A VFX Graph Update block: takes drag metres a second off the speed every second until the particle stops, as
// Shuriken's Limit Velocity drag does, rather than VFX's drag in proportion to the speed. bySpeed 1 scales the drag
// by the speed squared, as its Multiply by Velocity.
void SlowDown(inout VFXAttributes attributes, in float drag, in float bySpeed, in float deltaTime)
{
    float speed = length(attributes.velocity);
    float slowing = drag * lerp(1.0, speed * speed, bySpeed) * deltaTime;
    attributes.velocity *= max(speed - slowing, 0.0) / max(speed, 0.0001);
}
