using System;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Common;

/// <summary>A required Unity object, checked with Unity's null test so a destroyed or missing one fails too.</summary>
public static class ObjectChecks
{
    extension<T>(T value) where T : Object
    {
        /// <summary>The object, or throws <paramref name="problem"/> when it is missing or destroyed.</summary>
        public T OrThrow(string problem) => value != null ? value : throw new InvalidOperationException(problem);
    }
}
