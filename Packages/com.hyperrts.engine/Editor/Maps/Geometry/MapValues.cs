namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>One value per sample of a <see cref="MapGrid"/>, row by row from the south-west.</summary>
    public sealed class MapValues<T>
    {
        public readonly MapGrid Grid;
        public readonly T[] Values;

        public MapValues(MapGrid grid)
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
