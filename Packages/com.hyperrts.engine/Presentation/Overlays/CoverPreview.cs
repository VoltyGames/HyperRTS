using HyperRTS.Simulation.Common.Infantry;
using System;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Interaction;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Squads.Cover;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// With squads selected and the pointer over open ground, rings on the cover spots a move there would use (green
    /// heavy, yellow light), or a red ring when it would find none. Uses the search of <c>SquadCoverSystem</c>.
    /// </summary>
    public sealed class CoverPreview : IDisposable
    {
        private const float SpotRadius = 0.45f;
        private const float NoneRadius = 0.9f;
        private const float Lift = 0.08f;

        private readonly LiveQuery _squads = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<SquadMember>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalTransform>()));

        private readonly LiveSingleton<CoverSpots> _spots = new();
        private readonly LiveSingleton<CoverRules> _rules = new();
        private readonly LiveSingleton<SquadRules> _squadRules = new();
        private readonly LiveSingleton<HoveredEntity> _hovered = new();
        private readonly InstanceBuffer<OverlayInstance> _rings;
        private NativeList<int> _near;

        public CoverPreview(Mesh ring)
        {
            _rings = new InstanceBuffer<OverlayInstance>(ring);
            _near = new NativeList<int>(32, Allocator.Persistent);
        }

        public void Gather(MatchView view, OverlayStyle style)
        {
            var entityManager = view.EntityManager;
            if (view.Placement.Active || !_hovered.TryGet(entityManager, out var hovered)
                || !hovered.HasGround || hovered.Value != Entity.Null)
            {
                return;
            }

            _spots.Complete(entityManager);
            if (!_spots.TryGet(entityManager, out var spots) || !spots.IsCreated || !_rules.TryGet(entityManager, out var rules)
                || !_squadRules.TryGet(entityManager, out var squads)
                || !TryFindSquadCenter(view, out var center))
            {
                return;
            }

            var ground = hovered.Ground;
            var heading = math.normalizesafe(ground.xz - center);
            _near.Clear();
            spots.FindNear(ground.xz, rules.Reach(squads.MaxSpread), _near);
            var found = false;
            foreach (var index in _near)
            {
                var spot = spots.Spots[index];
                if (rules.Faces(spot.Facing, heading))
                {
                    found = true;
                    var color = spot.Kind == CoverKind.Heavy ? style.heavyCover : style.lightCover;
                    Add(spot.Position, SpotRadius, color);
                }
            }

            if (!found)
            {
                Add(ground, NoneRadius, style.noCover);
            }
        }

        public void Draw(OverlayMaterials materials, int layer) => _rings.Draw(materials.OnTop, layer);

        public void Dispose()
        {
            _rings.Dispose();
            _near.Dispose();
        }

        /// <summary>The middle of the local player's selected squads; false when none is selected.</summary>
        private bool TryFindSquadCenter(MatchView view, out float2 center)
        {
            var query = _squads.In(view.EntityManager);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var sum = float2.zero;
            var count = 0;
            for (var i = 0; i < factions.Length; i++)
            {
                if (factions[i].Value == view.Local.Faction)
                {
                    sum += transforms[i].Position.xz;
                    count++;
                }
            }

            center = count > 0 ? sum / count : float2.zero;
            return count > 0;
        }

        private void Add(float3 position, float radius, Color color)
        {
            var matrix = Matrix4x4.TRS(new Vector3(position.x, position.y + Lift, position.z), Quaternion.identity,
                new Vector3(radius, 1f, radius));
            _rings.Add(new OverlayInstance(matrix, color));
        }
    }
}
