// HyperRTS/LitTeam track bend. A track mesh carries _TrackSpan and the lift of 8 stations along its length, which
// MotionRigSystem sets from the road wheels' lift. The lower run is lifted by the stations around its z; the weight
// fades to 0 between the axle height and the top of the wheels, so the return run on top stays put.
#ifndef HYPERRTS_LIT_TEAM_TRACK_INCLUDED
#define HYPERRTS_LIT_TEAM_TRACK_INCLUDED

#include "Packages/com.hyperrts.engine/Presentation/TeamColors/LitTeamInput.hlsl"

float TrackStationLift(int station)
{
    return station < 4 ? _TrackLift0[station] : _TrackLift1[station - 4];
}

float3 BendTrack(float3 positionOS)
{
    // A zero span is not a track.
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

#endif
