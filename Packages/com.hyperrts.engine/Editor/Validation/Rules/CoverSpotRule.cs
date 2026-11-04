using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Squads;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Cover spots stand outside the prop's own nav obstacle, near the prop, facing across it, and number at most 255.
    /// </summary>
    public sealed class CoverSpotRule : AuthoringRule<CoverSpotAuthoring>
    {
        private const float MaxReach = 8f;

        protected override void Check(CoverSpotAuthoring cover, ValidationIssues issues)
        {
            if (cover.spots.Count == 0)
            {
                issues.Warn(cover, "No cover spots: soldiers never take cover here.");
            }

            if (cover.spots.Count > CoverSpotAuthoring.MaxSpots)
            {
                issues.Error(cover, "More than 255 spots; the rest are never used.");
            }

            var hasFootprint = cover.TryGetComponent<NavObstacleAuthoring>(out var obstacle);
            for (var i = 0; i < cover.spots.Count; i++)
            {
                var spot = cover.spots[i];
                if (spot.facing.sqrMagnitude < 1e-4f)
                {
                    issues.Error(cover, $"Spot {i} has no facing.");
                }
                else if (Vector2.Dot(spot.facing, -new Vector2(spot.position.x, spot.position.z)) <= 0f)
                {
                    issues.Error(cover, $"Spot {i} faces away from the prop, so it covers the open side.");
                }

                if (new Vector2(spot.position.x, spot.position.z).magnitude > MaxReach)
                {
                    issues.Warn(cover, $"Spot {i} is more than {MaxReach} m from the prop.");
                }

                if (hasFootprint && Mathf.Abs(spot.position.x) < obstacle.size.x * 0.5f
                    && Mathf.Abs(spot.position.z) < obstacle.size.y * 0.5f)
                {
                    issues.Warn(cover, $"Spot {i} is inside the prop's own nav obstacle, where no one can stand.");
                }
            }
        }
    }
}
