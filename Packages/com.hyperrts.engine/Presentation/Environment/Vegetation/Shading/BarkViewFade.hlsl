// Bark's URP colour and depth passes call LODFadeCrossFade unconditionally, so the view fade rides on it. Include
// after BarkInput.hlsl and before the URP pass file, and not in the shadow caster.
#ifndef HYPERRTS_BARK_VIEW_FADE_INCLUDED
#define HYPERRTS_BARK_VIEW_FADE_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Environment/Vegetation/Shading/ViewFade.hlsl"

void BarkCrossFade(float4 positionCS)
{
    LODFadeCrossFade(positionCS);
    TreeViewFade(positionCS);
}
#define LODFadeCrossFade(positionCS) BarkCrossFade(positionCS)

#endif
