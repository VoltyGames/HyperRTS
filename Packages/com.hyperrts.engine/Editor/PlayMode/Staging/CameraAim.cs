using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>Where a cinematic view looks from: the map point it aims at and its lift, distance, pitch and yaw.</summary>
    internal readonly struct CameraAim
    {
        public readonly float2 Look;
        public readonly float Lift;
        public readonly float Distance;
        public readonly float Pitch;
        public readonly float Yaw;

        public CameraAim(float2 look, float lift, float distance, float pitch, float yaw)
        {
            Look = look;
            Lift = lift;
            Distance = distance;
            Pitch = pitch;
            Yaw = yaw;
        }

        /// <summary>The aim <paramref name="share"/> of the way to <paramref name="to"/>; yaw turns the way its numbers run.</summary>
        public CameraAim Toward(CameraAim to, float share) => new(
            math.lerp(Look, to.Look, share),
            math.lerp(Lift, to.Lift, share),
            math.lerp(Distance, to.Distance, share),
            math.lerp(Pitch, to.Pitch, share),
            math.lerp(Yaw, to.Yaw, share));

        /// <summary>The same aim turned <paramref name="degrees"/> further round.</summary>
        public CameraAim Turned(float degrees) => new(Look, Lift, Distance, Pitch, Yaw + degrees);

        /// <summary>The same aim at another map point, for a view that tracks a moving group.</summary>
        public CameraAim At(float2 look) => new(look, Lift, Distance, Pitch, Yaw);

        /// <summary>The camera pose for this aim over ground at <paramref name="groundY"/>.</summary>
        public Pose ViewOver(float groundY)
        {
            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var target = new Vector3(Look.x, groundY + Lift, Look.y);
            return new Pose(target - rotation * Vector3.forward * Distance, rotation);
        }
    }
}
