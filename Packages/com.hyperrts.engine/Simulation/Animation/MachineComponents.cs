using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>Running time of a machine (pumpjack, turbine) that its <see cref="MachinePart"/>s move by.</summary>
    public struct MachineClock : IComponentData
    {
        public float Time;

        /// <summary>Stands still while neutral: a captured derrick starts pumping.</summary>
        public bool OwnedOnly;

        public MachineRun Run;

        /// <summary>The unit or building whose power, queue, shots or deploy progress <see cref="Run"/> reads.</summary>
        public Entity Owner;

        /// <summary>Seconds to ease from a stop to full speed, and back.</summary>
        public float RampSeconds;

        /// <summary>Linear progress from stopped (0) to full speed (1).</summary>
        public float Ramp;

        /// <summary>Set by the first frame, which snaps <see cref="Ramp"/>: no spin-up when first seen.</summary>
        public bool Started;

        /// <summary>Wrapping sum of the building's weapon shot counts when last seen, for <see cref="MachineRun.Firing"/>.</summary>
        public byte Shots;

        /// <summary>Seconds since <see cref="Shots"/> last changed.</summary>
        public float SinceShot;

        /// <summary>Seconds a Firing machine keeps running after its building's last shot.</summary>
        public const float FiringSeconds = 1f;

        /// <summary>Takes the building's shot count this frame and says whether it fired within <see cref="FiringSeconds"/>.</summary>
        public bool Fired(byte shots, float deltaTime)
        {
            if (!Started)
            {
                SinceShot = FiringSeconds;
            }
            else if (shots != Shots)
            {
                SinceShot = 0f;
            }
            else
            {
                SinceShot += deltaTime;
            }

            Shots = shots;
            return SinceShot < FiringSeconds;
        }

        /// <summary>Eases the speed toward running or stopped and moves the clock by it.</summary>
        public void Advance(bool runs, float deltaTime)
        {
            var target = runs ? 1f : 0f;
            var step = deltaTime / RampSeconds;
            Ramp = Started ? Ramp + math.clamp(target - Ramp, -step, step) : target;
            Started = true;
            Time += deltaTime * Speed;
        }

        /// <summary>Takes <paramref name="ramp"/> as the speed outright, to follow a unit's deploy progress.</summary>
        public void Follow(float ramp, float deltaTime)
        {
            Ramp = ramp;
            Started = true;
            Time += deltaTime * Speed;
        }

        /// <summary><see cref="Ramp"/> eased in and out, which speeds and Raise and Extend parts move by.</summary>
        public readonly float Speed => math.smoothstep(0f, 1f, Ramp);
    }

    /// <summary>When a machine runs.</summary>
    public enum MachineRun : byte
    {
        Always = 0,

        /// <summary>While its building is complete and not Offline (a radar antenna stops on low power).</summary>
        Powered = 1,

        /// <summary>While its building's queue has work it isn't halted on (a crane over the line).</summary>
        Producing = 2,

        /// <summary>While its building fired a weapon or interceptor in the last second (a gatling's barrels).</summary>
        Firing = 3,

        /// <summary>Its speed is its unit's deploy progress, so Raise and Extend parts unfold as it deploys, each over its own span.</summary>
        Deployed = 4,
    }

    /// <summary>A moving piece of a machine, posed every frame from its machine's clock.</summary>
    public struct MachinePart : IComponentData
    {
        public Entity Machine;
        public MachineMotion Motion;

        /// <summary>Local rotation axis (Spin, Swing), or the direction it slides along (Slide).</summary>
        public float3 Axis;

        /// <summary>Degrees per second of the cycle; a swing completes one rock per 360°.</summary>
        public float Speed;

        /// <summary>Swing: largest angle either way of <see cref="Offset"/>, degrees; Slide: metres. Raise and Extend: at full speed.</summary>
        public float Amplitude;

        /// <summary>Swing: the angle (degrees from rest) it rocks about. Slide: metres along the axis.</summary>
        public float Offset;

        /// <summary>Degrees added to the cycle, to set parts relative to each other.</summary>
        public float Phase;

        /// <summary>Raise and Extend: the span of the machine's speed, 0 to 1, it moves over, so parts unfold in turn.</summary>
        public float From;
        public float To;

        /// <summary>Link: the point it sits on and the point it aims at. Point: the pivot it keeps aiming at.</summary>
        public Entity Follow;
        public Entity Aim;

        /// <summary>Point: where <see cref="Aim"/> sat at rest, in the parent's space.</summary>
        public float3 RestAim;

        /// <summary>The baked local pose the motion starts from.</summary>
        public quaternion Rest;
        public float3 RestPosition;

        /// <summary>
        /// Downwind: the pose turned about <see cref="Axis"/> so its local forward, plus <see cref="Offset"/> degrees,
        /// points along <paramref name="downwind"/>, given in the parent's space. No wind keeps the rest pose.
        /// </summary>
        public readonly quaternion FacingDownwind(float3 downwind) =>
            Turned(math.rotate(Rest, math.forward()), downwind, math.radians(Offset));

        /// <summary>Point: the pose turned about <see cref="Axis"/> so <paramref name="aim"/>, in the parent's space, keeps its rest bearing.</summary>
        public readonly quaternion Pointing(float3 aim) => Turned(RestAim - RestPosition, aim - RestPosition, 0f);

        /// <summary>Raise and Extend: how far through its span it is at machine speed <paramref name="ramp"/>, eased in and out.</summary>
        public readonly float Unfolded(float ramp) => math.smoothstep(From, To, ramp);

        // Turns the rest pose about the axis by the angle from have to want, both flattened onto the axis' plane.
        private readonly quaternion Turned(float3 have, float3 want, float extra)
        {
            var axis = math.rotate(Rest, Axis);
            want -= axis * math.dot(want, axis);
            have -= axis * math.dot(have, axis);
            if (math.lengthsq(want) < 1e-6f || math.lengthsq(have) < 1e-6f)
            {
                return Rest;
            }

            var angle = math.atan2(math.dot(math.cross(have, want), axis), math.dot(have, want));
            return math.mul(Rest, quaternion.AxisAngle(Axis, angle + extra));
        }
    }

    /// <summary>How a <see cref="MachinePart"/> moves.</summary>
    public enum MachineMotion : byte
    {
        /// <summary>Turns steadily about its axis (a crank, a rotor).</summary>
        Spin = 0,

        /// <summary>Rocks to and fro about its axis in step with the spin (a walking beam).</summary>
        Swing = 1,

        /// <summary>Sits on one point and aims its up axis at another (a connecting rod).</summary>
        Link = 2,

        /// <summary>Travels to and fro along its axis in step with the spin (a crane on its rail).</summary>
        Slide = 3,

        /// <summary>Turns about its axis to point downwind with the scene's wind (a windsock).</summary>
        Downwind = 4,

        /// <summary>Turns about its axis by its amplitude in degrees as the speed rises (a launcher raising).</summary>
        Raise = 5,

        /// <summary>Travels along its axis by its amplitude in metres as the speed rises (a jack lowering).</summary>
        Extend = 6,

        /// <summary>Turns about its axis to keep aiming at another part's pivot (a hydraulic ram).</summary>
        Point = 7,
    }
}
