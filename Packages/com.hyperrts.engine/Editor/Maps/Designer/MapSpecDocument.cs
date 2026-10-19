using System;
using System.IO;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>A map spec shown in the Map Editor, read from its file and reread whenever the file changes.</summary>
    public sealed class MapSpecDocument
    {
        private long _diskTicks;

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
            _diskTicks = File.GetLastWriteTimeUtc(specPath).Ticks;
            Changed?.Invoke();
        }

        /// <summary>Reloads when the file changed on disk since it was read.</summary>
        public void ReloadIfChangedOnDisk()
        {
            if (!File.Exists(Path))
            {
                return;
            }

            if (File.GetLastWriteTimeUtc(Path).Ticks == _diskTicks)
            {
                return;
            }

            Load(Path);
        }
    }
}
