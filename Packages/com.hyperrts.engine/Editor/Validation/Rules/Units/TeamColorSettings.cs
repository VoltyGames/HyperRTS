using UnityEngine;

namespace HyperRTS.Editor.Validation.Rules.Units
{
    /// <summary>The project's team colour bar for the validation rules; the project keeps exactly one asset.</summary>
    [CreateAssetMenu(menuName = "HyperRTS/Team Colour Settings")]
    public sealed class TeamColorSettings : ScriptableObject
    {
        [Tooltip("Fewest team markers (Team Marker components) a vehicle or building prefab carries, so its owner shows on the model.")]
        [Min(0)]
        public int minMarkers = 2;
    }
}
