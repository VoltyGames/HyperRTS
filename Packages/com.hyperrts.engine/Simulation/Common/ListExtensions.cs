using Unity.Collections;

namespace HyperRTS.Simulation.Common
{
    /// <summary>In-place list edits that Unity.Collections lacks.</summary>
    public static class ListExtensions
    {
        /// <summary>Reverses the entries from <paramref name="begin"/> to the end in place, as a search's path is built back to front.</summary>
        public static void ReverseFrom<T>(this NativeList<T> list, int begin) where T : unmanaged
        {
            for (int i = begin, j = list.Length - 1; i < j; i++, j--)
            {
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
