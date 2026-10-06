using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Production;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>Production and research options are prefabs and units spawn clear of the building.</summary>
    public sealed class ProducerRule : AuthoringRule<ProducerAuthoring>
    {
        protected override void Check(ProducerAuthoring producer, ValidationIssues issues)
        {
            CheckPrefabOptions(producer, producer.productionOptions, "Production option", issues);
            CheckPrefabOptions(producer, producer.researchOptions, "Research option", issues);

            CheckSpawnPoint(producer, issues);
        }

        private static void CheckSpawnPoint(ProducerAuthoring producer, ValidationIssues issues)
        {
            if (!producer.TryGetComponent(out BuildingAuthoring building))
            {
                return;
            }

            var half = building.footprint * 0.5f;
            var insideX = Mathf.Abs(producer.spawnOffset.x) < half.x;
            var insideZ = Mathf.Abs(producer.spawnOffset.z) < half.y;
            if (!insideX || !insideZ)
            {
                return;
            }

            issues.Warn(producer, "Spawn offset is inside the footprint; units will spawn blocked.", "Move Outside",
                () => EditorUndo.Record(producer, "Move Spawn Point",
                    () => producer.spawnOffset = new Vector3(0f, 0f, -(half.y + 1.5f))));
        }
    }
}
