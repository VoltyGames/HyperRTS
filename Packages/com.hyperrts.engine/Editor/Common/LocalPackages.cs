using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HyperRTS.Editor.Common
{
    /// <summary>Imports files written into local (<c>file:</c>) packages, which Unity doesn't watch. Agent tools call it before a refresh.</summary>
    public static class LocalPackages
    {
        private const string ImportedKey = "HyperRTS.LocalPackages.Imported";

        /// <summary>Imports every local package file written since the last call in this Editor session, or since the Editor started.</summary>
        public static void ImportChanged()
        {
            var started = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var stored = SessionState.GetString(ImportedKey, started.Ticks.ToString());
            var since = new DateTime(Convert.ToInt64(stored), DateTimeKind.Utc);
            SessionState.SetString(ImportedKey, DateTime.UtcNow.Ticks.ToString());
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var package in PackageInfo.GetAllRegisteredPackages())
                {
                    if (package.source == PackageSource.Local)
                    {
                        ImportChanged(package, since);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void ImportChanged(PackageInfo package, DateTime since)
        {
            foreach (var file in Directory.EnumerateFiles(package.resolvedPath, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta") || File.GetLastWriteTimeUtc(file) <= since)
                {
                    continue;
                }

                var relative = file.Substring(package.resolvedPath.Length).Replace(Path.DirectorySeparatorChar, '/');
                AssetDatabase.ImportAsset(package.assetPath + relative);
            }
        }
    }
}
