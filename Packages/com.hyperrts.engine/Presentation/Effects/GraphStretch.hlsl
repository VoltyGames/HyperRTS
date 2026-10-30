// A VFX Graph output block for quads oriented along their velocity: stretches each to sizeLength times its size plus
// speedLength metres per metre a second of speed.
void StretchAlongVelocity(inout VFXAttributes attributes, in float sizeLength, in float speedLength)
{
    float speed = sqrt(dot(attributes.velocity, attributes.velocity));
    attributes.scaleY = sizeLength + speed * speedLength / max(attributes.size, 0.0001);
}
