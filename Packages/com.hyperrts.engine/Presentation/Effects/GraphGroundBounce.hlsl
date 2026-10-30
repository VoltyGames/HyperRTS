// A VFX Graph Update block: bounces particles off the ground plane through the burst origin (targetPosition).
// bounce keeps that share of the fall speed; friction takes that share of the speed along the ground.
void BounceOnGround(inout VFXAttributes attributes, in float bounce, in float friction)
{
    float3 v = attributes.velocity;
    if (attributes.position.y < attributes.targetPosition.y && v.y < 0.0)
    {
        float keep = 1.0 - friction;
        attributes.position.y = attributes.targetPosition.y;
        attributes.velocity = float3(v.x * keep, -v.y * bounce, v.z * keep);
    }
}
