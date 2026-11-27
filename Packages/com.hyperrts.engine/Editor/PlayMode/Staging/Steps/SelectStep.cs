using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Selection;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Editor.PlayMode.Staging.Steps;

/// <summary>Selects a group for the local player and clears the selection before it, so rings, markers and previews show.</summary>
internal sealed class SelectStep : StagingStep
{
    private readonly string _units;

    public SelectStep(string units) => _units = units;

    public override IEnumerable<string> Uses => new[] { _units };

    public override void Run(StagingRun run)
    {
        var entityManager = run.World;
        var presented = PlayWorld.TryGetPresented(out var view) && view.World == entityManager.World;
        if (!presented)
        {
            throw new InvalidOperationException("the selection lives in another world than the one the staging writes to");
        }

        var units = run.LiveUnits(_units, "select");
        using var query = entityManager.CreateEntityQuery(typeof(Selected));
        foreach (var entity in query.ToEntityArray(Allocator.Temp))
        {
            entityManager.SetComponentEnabled<Selected>(entity, false);
        }

        foreach (var unit in units)
        {
            if (entityManager.HasComponent<Selectable>(unit))
            {
                entityManager.SetComponentEnabled<Selected>(unit, true);
            }
        }
    }
}
