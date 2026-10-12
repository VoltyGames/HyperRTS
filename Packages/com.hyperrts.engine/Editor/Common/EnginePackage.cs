using System;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace HyperRTS.Editor.Common
{
    /// <summary>Where the engine package lives; tools that write into it need it embedded or local, not installed.</summary>
    internal static class EnginePackage
    {
        public static bool Writable
        {
            get
            {
                var package = PackageInfo.FindForAssembly(typeof(EnginePackage).Assembly);
                if (package == null)
                {
                    throw new InvalidOperationException("HyperRTS: the editor assembly is not inside a package.");
                }

                var embedded = package.source == PackageSource.Embedded;
                var local = package.source == PackageSource.Local;
                return embedded || local;
            }
        }
    }
}
