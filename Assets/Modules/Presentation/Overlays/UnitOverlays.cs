using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Fog;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>Selection rings under selected entities and health bars over selected or damaged ones.</summary>
    public sealed class UnitOverlays
    {
        private const float RingLift = 0.05f;

        private readonly LiveQuery _living = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Health>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>()));

        private readonly InstanceBatch _ownRings;
        private readonly InstanceBatch _enemyRings;
        private readonly InstanceBatch _neutralRings;
        private readonly InstanceBatch _barBacks;
        private readonly InstanceBatch _barsHigh;
        private readonly InstanceBatch _barsMid;
        private readonly InstanceBatch _barsLow;

        public UnitOverlays(Mesh ring, Mesh billboard)
        {
            _ownRings = new InstanceBatch(ring);
            _enemyRings = new InstanceBatch(ring);
            _neutralRings = new InstanceBatch(ring);
            _barBacks = new InstanceBatch(billboard);
            _barsHigh = new InstanceBatch(billboard);
            _barsMid = new InstanceBatch(billboard);
            _barsLow = new InstanceBatch(billboard);
        }

        public void Gather(MatchView view, OverlayStyle style, Transform camera)
        {
            var entityManager = view.EntityManager;
            var query = _living.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var healths = query.ToComponentDataArray<Health>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var selected = entityManager.HasComponent<Selected>(entity) && entityManager.IsComponentEnabled<Selected>(entity);
                var damaged = healths[i].Current < healths[i].Max;
                if (!selected && !damaged)
                {
                    continue;
                }

                var position = transforms[i].Position;
                var radius = EntityExtent.Radius(entityManager, entity);
                if (selected)
                {
                    var size = radius * style.ringScale;
                    RingBatch(view.RelationTo(factions[i].Value)).Add(Matrix4x4.TRS(
                        new Vector3(position.x, position.y + RingLift, position.z), Quaternion.identity, new Vector3(size, 1f, size)));
                }

                var top = EntityExtent.Top(entityManager, entity, position, radius) + style.barOffset;
                AddBar(new Vector3(position.x, top, position.z), Mathf.Clamp(radius * 2f, 1f, 4f), healths[i].Fraction,
                    style.barHeight, camera);
            }
        }

        /// <summary><paramref name="front"/> renders after <paramref name="material"/> so fills stay above backgrounds.</summary>
        public void Draw(Material material, Material front, OverlayStyle style, int layer)
        {
            _ownRings.Draw(material, style.ownRing, layer);
            _enemyRings.Draw(material, style.enemyRing, layer);
            _neutralRings.Draw(material, style.neutralRing, layer);
            _barBacks.Draw(material, style.healthBack, layer);
            _barsHigh.Draw(front, style.healthHigh, layer);
            _barsMid.Draw(front, style.healthMid, layer);
            _barsLow.Draw(front, style.healthLow, layer);
        }

        private InstanceBatch RingBatch(Relation relation) => relation switch
        {
            Relation.Own or Relation.Ally => _ownRings,
            Relation.Enemy => _enemyRings,
            _ => _neutralRings,
        };

        private void AddBar(Vector3 center, float width, float fraction, float height, Transform camera)
        {
            var rotation = camera.rotation;
            _barBacks.Add(Matrix4x4.TRS(center, rotation, new Vector3(width, height, 1f)));

            var fillCenter = center - camera.right * ((1f - fraction) * width * 0.5f);
            var fills = fraction > 0.6f ? _barsHigh : fraction > 0.3f ? _barsMid : _barsLow;
            fills.Add(Matrix4x4.TRS(fillCenter, rotation, new Vector3(width * fraction, height, 1f)));
        }
    }
}
