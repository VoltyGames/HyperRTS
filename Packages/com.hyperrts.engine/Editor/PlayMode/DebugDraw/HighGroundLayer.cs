using HyperRTS.Simulation.Vision.Sight;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Cells standing above the ground round them, blue for a small sight bonus to red for the largest.</summary>
    public sealed class HighGroundLayer : CellTextureLayer
    {
        public override string Label => "High ground";

        public override string Detail => "Cells that give a sight bonus";

        public override string Group => "Vision";

        public override string Glyph => "mountain";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out HighGround high) || !high.IsCreated)
            {
                return false;
            }

            texture.Paint(high.Grid, new Colors { Rise = high.Rise, PerMetre = high.Rules.SightPerMetre, MaxSight = high.Rules.MaxSight });
            return true;
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<float> Rise;
            public float PerMetre;
            public float MaxSight;

            public Color32 At(int index) => Rise[index] > 0f ? CellTexture.Heat(Rise[index] * PerMetre / MaxSight) : default;
        }
    }
}
