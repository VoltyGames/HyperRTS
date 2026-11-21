using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>
    /// Labels what the map already placed within a radius of a point, so later steps can damage, destroy or select it.
    /// It takes the live entities with health, whole: a sectioned building is one entity, its sections a buffer on it.
    /// </summary>
    internal sealed class GroupStep : StagingStep
    {
        [JsonProperty("as")]
        public string group;
        public float[] at;
        public float radius;
        public string prefab;

        private float2 _point;

        public override string Defines => group;

        public override bool EndsRunOnError => true;

        public override void Check()
        {
            Need(group, "as");
            _point = Point(at, "at");
            if (radius <= 0f)
            {
                throw new InvalidOperationException($"needs a 'radius' above 0, not {radius}");
            }

            if (group == SampleStep.CameraKey)
            {
                throw new InvalidOperationException($"cannot name its group '{SampleStep.CameraKey}'; sample uses that for the camera");
            }
        }

        public override void Run(StagingRun run)
        {
            var entityManager = run.World;
            using var query = entityManager.CreateEntityQuery(typeof(Health), typeof(Owner), typeof(LocalTransform));
            var found = new List<Entity>();
            foreach (var entity in query.ToEntityArray(Allocator.Temp))
            {
                var near = math.distance(entityManager.GetComponentData<LocalTransform>(entity).Position.xz, _point) <= radius;
                if (near && StagingGroup.IsAlive(entityManager, entity) && Named(entityManager, entity))
                {
                    found.Add(entity);
                }
            }

            var filter = prefab == null ? "" : $" named '{prefab}'";
            if (found.Count == 0)
            {
                throw new InvalidOperationException($"no live entity{filter} within {radius:0.#} m of [{_point.x:0.#}, {_point.y:0.#}]");
            }

            var owner = entityManager.GetComponentData<Owner>(found[0]).Value;
            if (found.Exists(entity => entityManager.GetComponentData<Owner>(entity).Value != owner))
            {
                throw new InvalidOperationException($"the entities{filter} within {radius:0.#} m of [{_point.x:0.#}, {_point.y:0.#}] have different owners; narrow it with 'prefab' or a smaller 'radius'");
            }

            run.AddGroup(new StagingGroup(group, owner, found));
        }

        private bool Named(EntityManager entityManager, Entity entity) =>
            prefab == null || (entityManager.HasComponent<EntityInfo>(entity)
                && entityManager.GetComponentData<EntityInfo>(entity).Name.ToString().Equals(prefab, StringComparison.OrdinalIgnoreCase));
    }
}
