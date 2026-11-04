using HyperRTS.Simulation.Common.Infantry;
using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Common.Authoring;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>
    /// The places on a prop where soldiers take cover. Each spot has a position, the way a soldier in it faces
    /// (toward the side the fire comes from) and a kind. The fillers write the list for you; edit it by hand after.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Combat + "Cover Spots")]
    [HelpURL(HyperRTSDocs.Combat)]
    [DisallowMultipleComponent]
    public class CoverSpotAuthoring : AuthoringBehaviour
    {
        /// <summary>Most spots one prop carries, since a spot is numbered in a byte.</summary>
        public const int MaxSpots = 255;

        [Serializable]
        public struct Spot
        {
            [Tooltip("Where the soldier stands, in this object's space.")]
            public Vector3 position;

            [Tooltip("Direction on the ground (X, Z) the soldier faces, toward the side the fire comes from.")]
            public Vector2 facing;

            public CoverKind kind;
        }

        public List<Spot> spots = new();

        [Header("Fillers")]
        [Tooltip("Footprint (X by Z) of the prop for Fill Both Sides.")]
        public Vector2 fillSize = new(2f, 0.6f);

        [Tooltip("Metres between neighbouring spots.")]
        [Min(0.5f)]
        public float fillSpacing = 1.2f;

        [Tooltip("Metres from the prop's face to the soldier.")]
        [Min(0f)]
        public float fillGap = 0.4f;

        [Tooltip("Radius of the prop for Fill Ring.")]
        [Min(0.1f)]
        public float fillRadius = 1f;

        public CoverKind fillKind = CoverKind.Heavy;

        [InspectorButton("Fill both sides")]
        public void FillBothSidesFromFields() => FillBothSides(fillSize, fillSpacing, fillGap, fillKind);

        [InspectorButton("Fill ring")]
        public void FillRingFromFields() => FillRing(fillRadius, fillSpacing, fillGap, fillKind);

        /// <summary>Replaces the spots with rows along both long faces of a box <paramref name="size"/>, each facing across the prop, toward the far side it covers.</summary>
        public void FillBothSides(Vector2 size, float spacing, float gap, CoverKind kind)
        {
            spots.Clear();
            var alongX = size.x >= size.y;
            var length = alongX ? size.x : size.y;
            var across = alongX ? size.y : size.x;
            var count = Mathf.Clamp(Mathf.FloorToInt(length / spacing), 1, MaxSpots / 2);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var i = 0; i < count; i++)
                {
                    var along = (i - (count - 1) * 0.5f) * spacing;
                    var off = side * (across * 0.5f + gap);
                    var position = alongX ? new Vector3(along, 0f, off) : new Vector3(off, 0f, along);
                    var facing = alongX ? new Vector2(0f, -side) : new Vector2(-side, 0f);
                    spots.Add(new Spot { position = position, facing = facing, kind = kind });
                }
            }
        }

        /// <summary>Replaces the spots with a ring round a prop of <paramref name="radius"/>, each facing in across it.</summary>
        public void FillRing(float radius, float spacing, float gap, CoverKind kind)
        {
            spots.Clear();
            var ring = radius + gap;
            var count = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * ring / spacing), 3, MaxSpots);
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                spots.Add(new Spot
                {
                    position = new Vector3(outward.x * ring, 0f, outward.y * ring), facing = -outward, kind = kind,
                });
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            foreach (var spot in spots)
            {
                Gizmos.color = spot.kind == CoverKind.Heavy ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(spot.position + Vector3.up * 0.1f, 0.2f);
                var facing = new Vector3(spot.facing.x, 0f, spot.facing.y).normalized;
                Gizmos.DrawRay(spot.position + Vector3.up * 0.1f, facing * 0.6f);
            }
        }

        public class Baker : Baker<CoverSpotAuthoring>
        {
            public override void Bake(CoverSpotAuthoring authoring)
            {
                var buffer = this.Writer().AddBuffer<CoverSpot>();
                foreach (var spot in authoring.spots)
                {
                    buffer.Add(new CoverSpot
                    {
                        Position = spot.position,
                        Facing = spot.facing,
                        Kind = spot.kind,
                    });
                }
            }
        }
    }
}
