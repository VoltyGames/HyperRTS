using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Cells standing above the ground round them, blue for a small sight bonus to red for the largest.</summary>
    public sealed class HighGroundLayer : DebugLayer
    {
        public override string Label => "High ground";

        public override string Detail => "Cells above the ground round them, blue for a small sight bonus to red for the largest.";

        public override string Group => "Vision";

        public override string Glyph => "mountain";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out HighGround high) || !high.IsCreated)
            {
                return;
            }

            shapes.Cells(high.Grid, new Colors { Rise = high.Rise, PerMetre = high.Rules.SightPerMetre, MaxSight = high.Rules.MaxSight });
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<float> Rise;
            public float PerMetre;
            public float MaxSight;

            public Color32 At(int index) => Rise[index] > 0f ? DebugShapes.Heat(Rise[index] * PerMetre / MaxSight) : default;
        }
    }
}
