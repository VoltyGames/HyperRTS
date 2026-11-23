using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project's team marker bar for the validation rules; the project keeps exactly one asset.</summary>
    [CreateAssetMenu(menuName = "HyperRTS/Team Marker Settings")]
    public sealed class TeamMarkerSettings : ScriptableObject
    {
        [Tooltip("Fewest team markers (Team Marker components) a vehicle or building prefab carries, so its owner shows on the model.")]
        [Min(0)]
        public int minMarkers = 2;
    }
}
