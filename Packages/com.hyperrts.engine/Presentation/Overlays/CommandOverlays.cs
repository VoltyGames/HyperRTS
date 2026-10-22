using HyperRTS.Client.Common;
using HyperRTS.Client.Interaction;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Production;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Building placement ghost with a mark at its front, one box per segment of a line, and rally markers of selected
    /// producers.
    /// </summary>
    public sealed class CommandOverlays
    {
        private const float RallyRadius = 0.6f;
        private const float RallyLineWidth = 0.12f;
        private const float Lift = 0.06f;
        private const float FrontMarkDepth = 1f;

        private readonly LiveQuery _placement = new(entityManager =>
            entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlacementState>()));

        private readonly LiveQuery _rallies = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<RallyPoint>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>()));

        private readonly InstanceBatch _validGhosts;
        private readonly InstanceBatch _invalidGhosts;
        private readonly InstanceBatch _rallyRings;
        private readonly InstanceBatch _rallyLines;

        public CommandOverlays(Mesh box, Mesh ring, Mesh flatQuad)
        {
            _validGhosts = new InstanceBatch(box);
            _invalidGhosts = new InstanceBatch(box);
            _rallyRings = new InstanceBatch(ring);
            _rallyLines = new InstanceBatch(flatQuad);
        }

        public void Gather(MatchView view, OverlayStyle style)
        {
            var entityManager = view.EntityManager;
            var placement = _placement.In(entityManager);
            var placing = placement.TryGetSingleton(out PlacementState state) && state.Active;
            if (placing && entityManager.HasComponent<LinePlacement>(state.Prefab))
            {
                var segments = entityManager.GetBuffer<PlacementSegment>(placement.GetSingletonEntity(), true);
                foreach (var segment in segments)
                {
                    AddGhost(segment.Center, segment.Footprint, quaternion.identity, segment.Valid, style);
                }
            }
            else if (placing && entityManager.HasComponent<NavObstacle>(state.Prefab))
            {
                var footprint = entityManager.GetComponentData<NavObstacle>(state.Prefab).Size;
                var facing = PlacementMath.Facing(state.Turn);
                AddGhost(state.Position, footprint, facing, state.Valid, style);
                AddFrontMark(state.Position, footprint, facing, state.Valid, style);
            }

            var query = _rallies.In(entityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var rallies = query.ToComponentDataArray<RallyPoint>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            for (var i = 0; i < rallies.Length; i++)
            {
                if (factions[i].Value == view.Local.Faction)
                {
                    AddRally(transforms[i].Position, rallies[i].Position);
                }
            }
        }

        public void Draw(Material material, OverlayStyle style, int layer)
        {
            _validGhosts.Draw(material, style.placementValid, layer);
            _invalidGhosts.Draw(material, style.placementInvalid, layer);
            _rallyRings.Draw(material, style.rally, layer);
            _rallyLines.Draw(material, style.rally, layer);
        }

        private void AddGhost(float3 center, float2 footprint, quaternion rotation, bool valid, OverlayStyle style)
        {
            var size = new Vector3(footprint.x, style.ghostHeight, footprint.y);
            (valid ? _validGhosts : _invalidGhosts).Add(Matrix4x4.TRS(center, rotation, size));
        }

        /// <summary>A low apron off the front edge, so the player sees where the doors and units will face.</summary>
        private void AddFrontMark(float3 center, float2 footprint, quaternion rotation, bool valid, OverlayStyle style)
        {
            var offset = math.rotate(rotation, new float3(0f, 0f, (footprint.y + FrontMarkDepth) * 0.5f));
            var size = new Vector3(footprint.x * 0.5f, style.ghostHeight * 0.25f, FrontMarkDepth);
            (valid ? _validGhosts : _invalidGhosts).Add(Matrix4x4.TRS(center + offset, rotation, size));
        }

        private void AddRally(float3 from, float3 to)
        {
            var target = new Vector3(to.x, to.y + Lift, to.z);
            _rallyRings.Add(Matrix4x4.TRS(target, Quaternion.identity, new Vector3(RallyRadius, 1f, RallyRadius)));

            var start = new Vector3(from.x, to.y + Lift, from.z);
            var delta = target - start;
            if (delta.sqrMagnitude < 0.01f)
            {
                return;
            }

            var rotation = Quaternion.LookRotation(delta, Vector3.up);
            _rallyLines.Add(Matrix4x4.TRS((start + target) * 0.5f, rotation, new Vector3(RallyLineWidth, 1f, delta.magnitude)));
        }
    }
}
