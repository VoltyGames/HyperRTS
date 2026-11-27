using Unity.Collections;

namespace HyperRTS.Simulation.Common;

/// <summary>In-place list edits that Unity.Collections lacks.</summary>
public static class ListExtensions
{
    extension<T>(NativeList<T> list) where T : unmanaged
    {
        /// <summary>Reverses the entries from <paramref name="begin"/> to the end in place, as a search's path is built back to front.</summary>
        public void ReverseFrom(int begin)
        {
            for (int i = begin, j = list.Length - 1; i < j; i++, j--)
            {
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
