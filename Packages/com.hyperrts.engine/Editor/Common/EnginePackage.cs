using System;
using Unity.Scripting.LifecycleManagement;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HyperRTS.Editor.Common
{
    /// <summary>Where the engine package lives; tools that write into it need it embedded or local, not installed.</summary>
    internal static partial class EnginePackage
    {
        public static bool Writable
        {
            get
            {
                var package = Info;
                var embedded = package.source == PackageSource.Embedded;
                var local = package.source == PackageSource.Local;
                return embedded || local;
            }
        }

        /// <summary>The package's project path, such as <c>Packages/com.hyperrts.engine</c>.</summary>
        public static string Folder => Info.assetPath;

        [AutoStaticsCleanup]
        private static PackageInfo _info;

        // A package change reloads the domain, so the lookup never goes stale.
        private static PackageInfo Info => _info ??= PackageInfo.FindForAssembly(typeof(EnginePackage).Assembly)
            ?? throw new InvalidOperationException("HyperRTS: the editor assembly is not inside a package.");
    }
}
