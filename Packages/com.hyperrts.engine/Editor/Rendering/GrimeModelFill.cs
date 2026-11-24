using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Presentation.Animation.Rigs;
using HyperRTS.Presentation.Rendering.LitTeam;
using UnityEngine;

namespace HyperRTS.Editor.Rendering
{
    /// <summary>Grime's mud line at the top of the road wheels and its dust line at the top of the hull.</summary>
    internal sealed class GrimeModelFill : ModelFill<GrimeAuthoring>
    {
        protected override void Fill(GrimeAuthoring grime)
        {
            var wheels = Parts(grime).Where(part => LocomotionModelFill.RoadWheelName.IsMatch(part.name)).ToList();
            if (wheels.Count == 0)
            {
                throw new InvalidOperationException($"'{grime.name}' has no road wheels (Wheel_L01...) to measure its mud height from.");
            }

            grime.mudHeight = Top(grime, wheels);
            grime.dustHeight = Top(grime, Hulls(grime));
        }

        private static IEnumerable<Transform> Parts(GrimeAuthoring grime) =>
            grime.GetComponentsInChildren<Transform>(true).Where(part => part != grime.transform);

        // A model whose only root node is the hull imports with the hull folded into the model object.
        private static List<Transform> Hulls(GrimeAuthoring grime)
        {
            var named = Parts(grime).Where(part => part.name == "Hull").ToList();
            if (named.Count > 0)
            {
                return named;
            }

            var folded = grime.transform.Cast<Transform>().Where(model => model.TryGetComponent<MeshFilter>(out _)).ToList();
            return folded.Count > 0
                ? folded
                : throw new InvalidOperationException(
                    $"'{grime.name}' has no Hull part, nor a model carrying the hull mesh, to measure its dust height from.");
        }

        // Metres above the unit's pivot, which stands on the ground.
        private static float Top(GrimeAuthoring grime, List<Transform> parts) =>
            parts.Max(part => PartSlot.MeshBounds(part, grime.transform).max.y);
    }
}
