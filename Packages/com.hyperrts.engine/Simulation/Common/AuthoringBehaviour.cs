using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Base of every authoring component, engine or game, so the inspector, summaries and validation find them all.
    /// </summary>
    public abstract class AuthoringBehaviour : MonoBehaviour
    {
        /// <summary>A socket's position in <paramref name="root"/>'s local space; none is the origin. Moving it rebakes.</summary>
        public static float3 Socket(IBaker baker, Transform root, Transform socket)
        {
            if (socket == null)
            {
                return float3.zero;
            }

            baker.DependsOn(socket);
            return root.InverseTransformPoint(socket.position);
        }
    }
}
