using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// A pond in map space: its shoreline polygon, and the ground round it: a basin below the water, gentle banks and
    /// an earth dam along one edge.
    /// </summary>
    public sealed class MapPond
    {
        /// <summary>The tallest dam, in metres above the water; the slopes of a taller one stay as long as this one's.</summary>
        public const float MaxDamHeight = 6f;

        private const float BankLift = 0.15f;
        private const float DamRun = 2.5f;
        private const float DamFreeboard = 0.5f;
        private const float DamEndTaper = 8f;

        private readonly Vector2 _damStart;
        private readonly Vector2 _damAlong;
        private readonly Vector2 _damOutward;
        private readonly float _damLength;

        public MapPond(List<Vector2> shore, MapPondSpec spec)
        {
            Shore = shore;
            Spec = spec;
            if (!HasDam)
            {
                Bounds = MapGeometry.Bounds(shore, spec.bankWidth);
                return;
            }

            _damStart = shore[spec.dam];
            var end = shore[(spec.dam + 1) % shore.Count];
            _damLength = (end - _damStart).magnitude;
            _damAlong = (end - _damStart) / _damLength;
            var outward = MapGeometry.Left(_damAlong);
            _damOutward = MapGeometry.Contains(shore, (_damStart + end) * 0.5f + outward * 0.5f) ? -outward : outward;
            var reach = Mathf.Max(spec.bankWidth, MaxDamHeight * DamRun * 2f + spec.damWidth);
            Bounds = MapGeometry.Bounds(shore, reach + DamEndTaper);
        }

        public List<Vector2> Shore { get; }

        public MapPondSpec Spec { get; }

        /// <summary>The shore grown by everything the pond shapes, for quick rejects.</summary>
        public Rect Bounds { get; }

        public bool HasDam => Spec.dam >= 0;

        /// <summary>Distance to the water's edge (negative over the water), or infinity beyond <see cref="Bounds"/>.</summary>
        public float EdgeDistance(Vector2 point) =>
            Bounds.Contains(point) ? -MapGeometry.SignedDistance(Shore, point) : float.PositiveInfinity;

        /// <summary>True on the dam's body and top, from the water's edge to the far side of its flat top.</summary>
        public bool OnDam(Vector2 point)
        {
            if (!HasDam)
            {
                return false;
            }

            var local = point - _damStart;
            var across = Vector2.Dot(local, _damOutward);
            var along = Vector2.Dot(local, _damAlong);
            var reach = Spec.damHeight * DamRun + Spec.damWidth;
            return across >= 0f && across <= reach && along >= 0f && along <= _damLength;
        }

        /// <summary>The ground height at a point: the basin below the water, the banks easing out to the open ground and the dam.</summary>
        public float Ground(Vector2 point, float ground, float water)
        {
            var edge = EdgeDistance(point);
            if (edge < 0f)
            {
                return water - Spec.depth * MapGeometry.Smooth(-edge / Spec.bankWidth);
            }

            var bank = Mathf.Lerp(water + BankLift, ground, MapGeometry.Smooth(edge / Spec.bankWidth));
            return HasDam ? Mathf.Max(bank, Dam(point, bank, ground, water)) : bank;
        }

        /// <summary>The dam rises from the water's edge, stays flat on top, then falls back to the bank on its outer side.</summary>
        private float Dam(Vector2 point, float bank, float ground, float water)
        {
            var local = point - _damStart;
            var across = Vector2.Dot(local, _damOutward);
            if (across < 0f)
            {
                return bank;
            }

            var along = Vector2.Dot(local, _damAlong);
            var overshoot = Mathf.Max(-along, along - _damLength, 0f);
            var top = Mathf.Max(water + Spec.damHeight, ground + DamFreeboard);
            var slope = Mathf.Min(top - water, MaxDamHeight) * DamRun;
            var rise = MapGeometry.Smooth(across / slope);
            var fall = 1f - MapGeometry.Smooth((across - slope - Spec.damWidth) / slope);
            var shape = Mathf.Min(rise, fall) * (1f - MapGeometry.Smooth(overshoot / DamEndTaper));
            return Mathf.Lerp(bank, top, shape);
        }
    }
}
