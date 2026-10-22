using System;

namespace HyperRTS.Network.Common
{
    /// <summary>
    /// A client system that mirrors a remote server's state, so a host world, which owns that state, leaves it out.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class RemoteClientOnlyAttribute : Attribute
    {
    }
}
