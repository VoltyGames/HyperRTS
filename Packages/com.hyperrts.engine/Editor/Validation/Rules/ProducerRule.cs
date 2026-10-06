using HyperRTS.Simulation.Buildings;
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

            if (producer.TryGetComponent(out BuildingAuthoring building) &&
                Mathf.Abs(producer.spawnOffset.x) < building.footprint.x * 0.5f &&
                Mathf.Abs(producer.spawnOffset.z) < building.footprint.y * 0.5f)
            {
                issues.Warn(producer, "Spawn offset is inside the footprint; units will spawn blocked.", "Move Outside",
                    () => QuickFixes.Edit(producer, "Move Spawn Point",
                        () => producer.spawnOffset = new Vector3(0f, 0f, -(building.footprint.y * 0.5f + 1.5f))));
            }
        }
    }
}
