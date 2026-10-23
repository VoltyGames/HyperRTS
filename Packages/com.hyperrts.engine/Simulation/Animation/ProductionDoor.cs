using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>
    /// A door on a producer that opens for each unit it releases, holds while the unit drives out, then closes. Driven
    /// on every client by the producer's replicated <c>Producer.Released</c> count, or on an aircraft held open by its
    /// replicated <c>DropRun</c>; presentation only.
    /// </summary>
    public struct ProductionDoor : IComponentData
    {
        /// <summary>The producer or aircraft that drives the door.</summary>
        public Entity Root;

        /// <summary>Open pose relative to the rest pose: a local translation, rotation and scale factor.</summary>
        public float3 OpenOffset;
        public quaternion OpenRotation;
        public float3 OpenScale;

        public float3 RestPosition;
        public quaternion RestRotation;
        public float3 RestScale;

        /// <summary>Seconds to open, and again to close.</summary>
        public float MoveSeconds;

        public float HoldSeconds;

        /// <summary>The release count this door has answered; client-side.</summary>
        public uint Seen;

        /// <summary>False until the first count is read, so a building seen mid-match skips old releases.</summary>
        public bool Synced;

        /// <summary>Seconds since the door last started opening; baked large so it starts shut.</summary>
        public float Elapsed;

        /// <summary>Linear progress from shut (0) to open (1).</summary>
        public readonly float Openness
        {
            get
            {
                var opening = Elapsed / MoveSeconds;
                var closing = (2f * MoveSeconds + HoldSeconds - Elapsed) / MoveSeconds;
                return math.saturate(math.min(opening, closing));
            }
        }

        /// <summary>Opens on a new release, picking up from where it stands so a busy door stays open.</summary>
        public void Advance(uint released, float deltaTime)
        {
            if (!Synced)
            {
                Seen = released;
                Synced = true;
            }

            if (released != Seen)
            {
                Seen = released;
                Elapsed = Openness * MoveSeconds;
            }

            Elapsed += deltaTime;
        }

        /// <summary>Stays open while <paramref name="open"/>, then holds and closes as after a release.</summary>
        public void Hold(bool open, float deltaTime)
        {
            if (open && Elapsed > MoveSeconds)
            {
                Elapsed = Openness * MoveSeconds;
            }

            Elapsed = open ? math.min(Elapsed + deltaTime, MoveSeconds) : Elapsed + deltaTime;
        }

        private readonly float Eased => math.smoothstep(0f, 1f, Openness);

        /// <summary>Eased pose between rest and open.</summary>
        public readonly void Pose(ref LocalTransform transform)
        {
            var eased = Eased;
            transform.Position = RestPosition + OpenOffset * eased;
            transform.Rotation = math.mul(RestRotation, math.slerp(quaternion.identity, OpenRotation, eased));
        }

        /// <summary>Eased non-uniform scale between rest and open, about the door's own pivot.</summary>
        public readonly float4x4 Stretch() => float4x4.Scale(RestScale * math.lerp(1f, OpenScale, Eased));
    }
}
