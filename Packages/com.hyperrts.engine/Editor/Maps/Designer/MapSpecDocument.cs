using System;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>A map spec shown in the Map Editor, read from its file and reread whenever Unity imports it again.</summary>
    public sealed class MapSpecDocument
    {
        private MapSpecDocument(string specPath) => Load(specPath);

        /// <summary>Raised after every load.</summary>
        public event Action Changed;

        public string Path { get; private set; }

        public string MapName => MapPaths.MapName(Path);

        public MapSpec Spec { get; private set; }

        public static MapSpecDocument Open(string specPath) => new(specPath);

        private void Load(string specPath)
        {
            if (!MapPaths.IsSpec(specPath))
            {
                throw new ArgumentException($"'{specPath}' is not a map spec ({MapPaths.SpecExtension}).");
            }

            Spec = MapSpecJson.Read(File.ReadAllText(specPath));
            Path = specPath;
            Changed?.Invoke();
        }

        /// <summary>Rereads the file; <see cref="MapSpecImport"/> calls it when Unity imports the spec again.</summary>
        public void Reload() => Load(Path);
    }
}
