using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>Burial depth and slope tilt, worked out from the finished ground at write time so mirrored copies match.</summary>
    public static class MapPlacementPose
    {
        public static Pose OnGround(MapPlacement placement, MapHeights heights, float groundLevel)
        {
            var point = placement.Position;
            var y = (placement.Height ?? heights.Height(point)) - groundLevel - placement.Entry.buriedDepth;
            var align = placement.Entry.alignToGround;
            var tilt = align > 0f ? Tilt(heights.Normal(point, placement.Radius * 2f), align) : Quaternion.identity;
            var rotation = tilt * Quaternion.Euler(0f, placement.Yaw, 0f);
            return new Pose(new Vector3(point.x, y, point.y), rotation);
        }

        /// <summary>Up turned toward <paramref name="normal"/> by at most <paramref name="maxDegrees"/>.</summary>
        public static Quaternion Tilt(Vector3 normal, float maxDegrees)
        {
            if (maxDegrees <= 0f)
            {
                return Quaternion.identity;
            }

            var leaning = Vector3.RotateTowards(Vector3.up, normal, maxDegrees * Mathf.Deg2Rad, 0f);
            return Quaternion.FromToRotation(Vector3.up, leaning);
        }
    }
}
