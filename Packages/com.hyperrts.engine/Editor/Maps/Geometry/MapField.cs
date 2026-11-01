namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>One value per sample of a <see cref="MapGrid"/>, row by row from the south-west.</summary>
    public sealed class MapField<T>
    {
        public readonly MapGrid Grid;
        public readonly T[] Values;

        public MapField(MapGrid grid)
        {
            Grid = grid;
            Values = new T[grid.Count];
        }

        public T this[int x, int z]
        {
            get => Values[Grid.Index(x, z)];
            set => Values[Grid.Index(x, z)] = value;
        }

        public T this[int index]
        {
            get => Values[index];
            set => Values[index] = value;
        }
    }
}
