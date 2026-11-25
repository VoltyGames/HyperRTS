// Track bend and scroll: the lower run lifts with the road wheels (lift at 8 stations along the track, set by MotionRigSystem);
// the weight fades out above the axle so the top run stays put.
#ifndef HYPERRTS_LIT_TEAM_TRACK_INCLUDED
#define HYPERRTS_LIT_TEAM_TRACK_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/Rendering/LitTeam/LitTeamInput.hlsl"

float TrackStationLift(int station)
{
    return station < 4 ? _TrackLift0[station] : _TrackLift1[station - 4];
}

float3 BendTrack(float3 positionOS)
{
    if (_TrackSpan.x == _TrackSpan.y)
    {
        return positionOS;
    }

    float station = saturate((positionOS.z - _TrackSpan.x) / (_TrackSpan.y - _TrackSpan.x)) * 7.0;
    int lower = (int)min(station, 6.0);
    float lift = lerp(TrackStationLift(lower), TrackStationLift(lower + 1), station - lower);
    positionOS.y += lift * (1.0 - saturate((positionOS.y - _TrackSpan.z) / _TrackSpan.w));
    return positionOS;
}

// Runs the belt: V is metres along it, increasing toward the back, so the tread moves back as the travel grows. Done
// on the mesh UV before the base map's tiling, so the tiling scales it. Other meshes have no travel and stay put.
#define SCROLL_TRACK_UV(uv) uv.y -= _TrackTravel;

#endif
