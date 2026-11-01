using Unity.Collections;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Network.Commands
{
    /// <summary>
    /// The units a client's commands address, sent only when they change. A selection larger than one RPC goes as
    /// several with the same <see cref="Version"/>: the first replaces the server's copy and the rest append to it.
    /// </summary>
    public struct SelectionRpc : IRpcCommand
    {
        /// <summary>127 ghost references keep the RPC under 1 KB on the wire.</summary>
        public const int Capacity = 127;

        /// <summary>Counts the client's selection changes, so the server matches each command to its selection.</summary>
        public ushort Version;

        public bool Append;

        [GhostFixedListCapacity(Capacity = Capacity)]
        public FixedList4096Bytes<Entity> Units;

        /// <summary>Splits <paramref name="units"/> into RPCs; an empty selection still sends one, to clear the server's.</summary>
        public static void Fill(NativeArray<Entity> units, ushort version, NativeList<SelectionRpc> rpcs)
        {
            var rpc = new SelectionRpc { Version = version };
            foreach (var unit in units)
            {
                if (rpc.Units.Length == Capacity)
                {
                    rpcs.Add(rpc);
                    rpc = new SelectionRpc { Version = version, Append = true };
                }

                rpc.Units.Add(unit);
            }

            rpcs.Add(rpc);
        }

        /// <summary>Whether a command stamped with <paramref name="command"/> was sent after this selection.</summary>
        public readonly bool Precedes(ushort command) => (short)(command - Version) >= 0;
    }
}
