using UnityEngine;

namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>The four samples of a <see cref="MapGrid"/> round a point and its fractions between them.</summary>
    public readonly struct MapBilinear
    {
        public readonly int X0;
        public readonly int Z0;
        public readonly float Fx;
        public readonly float Fz;

        public MapBilinear(int x0, int z0, float fx, float fz)
        {
            X0 = x0;
            Z0 = z0;
            Fx = fx;
            Fz = fz;
        }

        public int X1 => X0 + 1;

        public int Z1 => Z0 + 1;

        /// <summary>The sample nearest the point.</summary>
        public (int X, int Z) Nearest => (Fx < 0.5f ? X0 : X1, Fz < 0.5f ? Z0 : Z1);

        public float Blend(MapField<float> field) =>
            Blend(field[X0, Z0], field[X1, Z0], field[X0, Z1], field[X1, Z1]);

        public float Blend(float near0, float near1, float far0, float far1) =>
            Mathf.Lerp(Mathf.Lerp(near0, near1, Fx), Mathf.Lerp(far0, far1, Fx), Fz);
    }
}
