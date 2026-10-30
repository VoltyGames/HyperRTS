// A VFX Graph Update block: plays a flipbook from a random start frame, span sheets over each life, wrapping round
// the sheet of frames, as Shuriken's random start frame on a looping sheet.
void LoopFrames(inout VFXAttributes attributes, in float frames, in float span, in float deltaTime)
{
    attributes.texIndex = fmod(attributes.texIndex + span * frames * deltaTime / attributes.lifetime, frames);
}
