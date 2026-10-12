namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>Something wrong with the map library: the file at fault (null when the index itself fails) and why.</summary>
    public readonly struct MapLibraryProblem
    {
        public readonly string File;
        public readonly string Message;

        public MapLibraryProblem(string file, string message)
        {
            File = file;
            Message = message;
        }

        public override string ToString() => File == null ? Message : $"{File}: {Message}";
    }
}
