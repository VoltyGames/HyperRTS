using HyperRTS.Client.Common;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Overlays
{
    /// <summary>
    /// Selection rings under selected entities and health bars over selected or damaged ones. The local player's own
    /// selected units draw on top of scenery so they stay visible behind buildings.
    /// </summary>
    public sealed class UnitOverlays
    {
        private const float RingLift = 0.05f;

        private readonly LiveQuery _living = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Health>(),
            ComponentType.ReadOnly<Faction>(),
            ComponentType.ReadOnly<LocalToWorld>(),
            ComponentType.Exclude<FogHidden>(),
            ComponentType.Exclude<Inside>()));

        // Enabled-only, so the loop never asks EntityManager per entity whether it is selected.
        private readonly LiveQuery _selected = new(entityManager => entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<Selected>(),
            ComponentType.ReadOnly<Health>()));

        private readonly UnitOverlayBatches _depthTested;
        private readonly UnitOverlayBatches _onTop;

        public UnitOverlays(Mesh ring, Mesh billboard)
        {
            _depthTested = new UnitOverlayBatches(ring, billboard);
            _onTop = new UnitOverlayBatches(ring, billboard);
        }

        public void Gather(MatchView view, OverlayStyle style, Transform camera)
        {
            var entityManager = view.EntityManager;
            var query = _living.In(entityManager);
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var factions = query.ToComponentDataArray<Faction>(Allocator.Temp);
            using var healths = query.ToComponentDataArray<Health>(Allocator.Temp);
            using var transforms = query.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            using var selection = SelectedSet(entityManager);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var selected = selection.Contains(entity);
                var damaged = healths[i].Current < healths[i].Max;
                if (!selected && !damaged)
                {
                    continue;
                }

                var relation = view.RelationTo(factions[i].Value);
                var batches = selected && relation == Relation.Own ? _onTop : _depthTested;
                var position = transforms[i].Position;
                var radius = EntityRadius.Get(entityManager, entity);
                if (selected)
                {
                    var length = HalfLength(entityManager, entity, radius) * style.ringScale;
                    var size = radius * style.ringScale;
                    batches.AddRing(relation, Matrix4x4.TRS(new Vector3(position.x, position.y + RingLift, position.z),
                        Facing(transforms[i]), new Vector3(size, 1f, length)));
                }

                var top = EntityExtent.Top(entityManager, entity, position, radius) + style.barOffset;
                batches.AddBar(new Vector3(position.x, top, position.z), Mathf.Clamp(radius * 2f, 1f, 4f),
                    healths[i].Fraction, style.barHeight, camera);
            }
        }

        public void Draw(OverlayMaterials materials, OverlayStyle style, int layer)
        {
            _depthTested.Draw(materials.DepthTested, materials.DepthTestedFront, style, layer);
            _onTop.Draw(materials.OnTop, materials.OnTopFront, style, layer);
        }

        // Capsule hulls get an ellipse along their facing, so a long truck's ring spans the truck.
        private static float HalfLength(EntityManager entityManager, Entity entity, float radius) =>
            entityManager.HasComponent<NavAgent>(entity) ? entityManager.GetComponentData<NavAgent>(entity).HalfLength : radius;

        private static Quaternion Facing(in LocalToWorld transform) =>
            quaternion.LookRotationSafe(new float3(transform.Forward.x, 0f, transform.Forward.z), math.up());

        private NativeHashSet<Entity> SelectedSet(EntityManager entityManager)
        {
            using var selected = _selected.In(entityManager).ToEntityArray(Allocator.Temp);
            var set = new NativeHashSet<Entity>(selected.Length, Allocator.Temp);
            foreach (var entity in selected)
            {
                set.Add(entity);
            }

            return set;
        }
    }
}
