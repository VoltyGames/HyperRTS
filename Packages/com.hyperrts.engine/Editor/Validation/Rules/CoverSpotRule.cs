using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Squads.Cover;
using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules
{
    /// <summary>
    /// Cover spots stand outside the prop's own nav obstacle, near the prop, facing across it (or any way from inside a building's footprint), and number at most 255.
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

            cover.TryGetComponent<NavObstacleAuthoring>(out var obstacle);
            for (var i = 0; i < cover.spots.Count; i++)
            {
                CheckSpot(cover, obstacle, i, issues);
            }
        }

        private static void CheckSpot(CoverSpotAuthoring cover, NavObstacleAuthoring obstacle, int index, ValidationIssues issues)
        {
            var spot = cover.spots[index];
            var ground = new Vector2(spot.position.x, spot.position.z);
            if (spot.facing.sqrMagnitude < 1e-4f)
            {
                issues.Error(cover, $"Spot {index} has no facing.");
            }
            else if (!IsInsideBuilding(cover, spot.position) && Vector2.Dot(spot.facing, -ground) <= 0f)
            {
                issues.Error(cover, $"Spot {index} faces away from the prop, so it covers the open side.");
            }

            if (ground.magnitude > MaxReach)
            {
                issues.Warn(cover, $"Spot {index} is more than {MaxReach} m from the prop.");
            }

            if (obstacle != null && Mathf.Abs(ground.x) < obstacle.size.x * 0.5f && Mathf.Abs(ground.y) < obstacle.size.y * 0.5f)
            {
                issues.Warn(cover, $"Spot {index} is inside the prop's own nav obstacle, where no one can stand.");
            }
        }

        // A soldier inside a trench or a nest has the prop on every side, so it may face any way.
        private static bool IsInsideBuilding(CoverSpotAuthoring cover, Vector3 position)
        {
            if (!cover.TryGetComponent<BuildingAuthoring>(out var building))
            {
                return false;
            }

            var inside = Mathf.Abs(position.x) < building.footprint.x * 0.5f;
            return inside && Mathf.Abs(position.z) < building.footprint.y * 0.5f;
        }
    }
}
