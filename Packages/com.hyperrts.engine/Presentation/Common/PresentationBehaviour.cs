using UnityEngine;

namespace HyperRTS.Presentation.Common
{
    /// <summary>
    /// Base of every presentation and input MonoBehaviour: it switches itself off in Awake without a graphics device.
    /// Override <see cref="OnAwake"/>, <see cref="OnDisabled"/> and <see cref="OnDestroyed"/> instead of the Unity methods.
    /// </summary>
    public abstract class PresentationBehaviour : MonoBehaviour
    {
        protected void Awake()
        {
            if (!GraphicsSupport.Available)
            {
                enabled = false;
                return;
            }

            OnAwake();
        }

        // Switching off in Awake still calls OnDisable.
        protected void OnDisable()
        {
            if (!GraphicsSupport.Available)
            {
                return;
            }

            OnDisabled();
        }

        protected void OnDestroy()
        {
            if (!GraphicsSupport.Available)
            {
                return;
            }

            OnDestroyed();
        }

        protected virtual void OnAwake()
        {
        }

        protected virtual void OnDisabled()
        {
        }

        protected virtual void OnDestroyed()
        {
        }
    }
}
