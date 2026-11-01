using System;
using System.Threading.Tasks;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Maps.Common
{
    /// <summary>
    /// Runs the rows of a per-texel painter on worker threads. Each row must write only its own texels and read
    /// nothing another row writes, so the output is the same in any order.
    /// </summary>
    public static partial class MapParallel
    {
        /// <summary>Runs the rows in order on this thread, for tests that compare the threaded output with it.</summary>
        [AutoStaticsCleanup]
        public static bool Serial;

        public static void ForRows(int rows, Action<int> row)
        {
            if (Serial)
            {
                for (var i = 0; i < rows; i++)
                {
                    row(i);
                }

                return;
            }

            Parallel.For(0, rows, row);
        }

        /// <summary><see cref="ForRows(int, Action{int})"/> with scratch made once per worker thread.</summary>
        public static void ForRows<T>(int rows, Func<T> scratch, Action<int, T> row)
        {
            if (Serial)
            {
                var only = scratch();
                for (var i = 0; i < rows; i++)
                {
                    row(i, only);
                }

                return;
            }

            Parallel.For(0, rows, scratch, (i, _, local) =>
            {
                row(i, local);
                return local;
            }, _ => { });
        }
    }
}
