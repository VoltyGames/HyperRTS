using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Animation.Rigs
{
    /// <summary>HyperRTS/LitTeam's lift in metres of a track mesh at its stations 0 to 3, station 0 at the front.</summary>
    [MaterialProperty("_TrackLift0")]
    public struct TrackLift0 : IComponentData
    {
        public float4 Value;
    }

    /// <summary>The lift of a track mesh at its stations 4 to 7, station 7 at the back.</summary>
    [MaterialProperty("_TrackLift1")]
    public struct TrackLift1 : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Metres the tread of a track mesh has run along the belt, wrapped; the shader moves the belt's V by it. Positive
    /// when the track's ground run moves back, which is when the hull drives forward.
    /// </summary>
    [MaterialProperty("_TrackTravel")]
    public struct TrackTravel : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Where a track mesh bends, in its object space: x the front z and y the back z of its stations, z the road
    /// wheels' axle height and w their radius. Without it the shader leaves the mesh alone.
    /// </summary>
    [MaterialProperty("_TrackSpan")]
    public struct TrackSpan : IComponentData
    {
        public float4 Value;
    }

    /// <summary>The rig and track whose lift a track mesh shows, set when the mesh bakes.</summary>
    public struct TrackMeshSource : IComponentData
    {
        public Entity Rig;

        /// <summary>Index in the rig's <see cref="MotionRigTrack"/>s.</summary>
        public int Track;
    }

    /// <summary>A road wheel a track bends over, in order from the front.</summary>
    [InternalBufferCapacity(0)]
    public struct TrackWheel : IBufferElementData
    {
        /// <summary>Index of the wheel in the rig's <see cref="MotionRigPart"/>s.</summary>
        public int Part;

        /// <summary>Where the wheel sits along the track, 0 at the front station to 7 at the back one.</summary>
        public float Station;

        /// <summary>The wheel's lift this frame.</summary>
        public float Lift;
    }

    /// <summary>One track mesh of a <see cref="MotionRig"/> and the lift its mesh shows.</summary>
    [InternalBufferCapacity(0)]
    public struct MotionRigTrack : IBufferElementData
    {
        public const int Stations = 8;

        /// <summary>The smallest change in metres worth writing to the mesh again.</summary>
        public const float MinChange = 0.005f;

        /// <summary>Wrap of <see cref="Travel"/>, which keeps the float precise in the shader.</summary>
        public const float TravelPeriod = 1000f;

        /// <summary>The track's wheels in the rig's <see cref="TrackWheel"/>s.</summary>
        public int FirstWheel;

        public int WheelCount;

        public float4 Lift0;
        public float4 Lift1;

        /// <summary>True when the last <see cref="Settle"/> changed the lift, so the mesh needs it again.</summary>
        public bool Moved;

        /// <summary>Metres the track's side of the hull sits right of the rig's centre, negative on the left.</summary>
        public float Side;

        /// <summary>Metres the belt has run, wrapped to <see cref="TravelPeriod"/>.</summary>
        public float Travel;

        /// <summary>The <see cref="Travel"/> the mesh shows.</summary>
        public float Shown;

        /// <summary>True when the last <see cref="Scroll"/> changed <see cref="Shown"/>, so the mesh needs it again.</summary>
        public bool Scrolled;

        /// <summary>
        /// Runs the belt with the hull's <paramref name="rolled"/> metres and <paramref name="turn"/> radians: a turn
        /// runs the two sides opposite ways, the side in the turn's direction faster.
        /// </summary>
        public void Scroll(float rolled, float turn)
        {
            Travel = math.fmod(Travel + rolled + turn * Side, TravelPeriod);
            Scrolled = math.abs(Travel - Shown) > MinChange;
            if (Scrolled)
            {
                Shown = Travel;
            }
        }

        /// <summary>
        /// Takes the lift at each station from <paramref name="wheels"/>. True when one moved by more than
        /// <see cref="MinChange"/> and the mesh needs <see cref="Lift0"/> and <see cref="Lift1"/>.
        /// </summary>
        public bool Settle(NativeArray<TrackWheel> wheels)
        {
            var front = new float4(LiftAt(0, wheels), LiftAt(1, wheels), LiftAt(2, wheels), LiftAt(3, wheels));
            var back = new float4(LiftAt(4, wheels), LiftAt(5, wheels), LiftAt(6, wheels), LiftAt(7, wheels));
            var moved = math.cmax(math.max(math.abs(front - Lift0), math.abs(back - Lift1))) > MinChange;
            Moved = moved;
            if (moved)
            {
                Lift0 = front;
                Lift1 = back;
            }

            return moved;
        }

        // The ends hold still with the sprocket and idler, so lift fades to 0 beyond the outer wheels.
        private static float LiftAt(float station, NativeArray<TrackWheel> wheels)
        {
            var behind = float2.zero;
            foreach (var wheel in wheels)
            {
                var ahead = new float2(wheel.Station, wheel.Lift);
                if (station <= wheel.Station)
                {
                    return Between(behind, ahead, station);
                }

                behind = ahead;
            }

            return Between(behind, new float2(Stations - 1, 0f), station);
        }

        private static float Between(float2 from, float2 to, float station) =>
            to.x > from.x ? math.lerp(from.y, to.y, (station - from.x) / (to.x - from.x)) : to.y;
    }
}
