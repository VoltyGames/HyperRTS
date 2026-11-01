using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// A slot one entity holds by reserving it again every frame. A holder that dies or stops reserving loses it when the
    /// owning system calls <see cref="Expire"/>, so nothing has to check the holder is still alive.
    /// </summary>
    public struct Reservation
    {
        public Entity Holder;

        /// <summary>The holder reserved it since <see cref="Expire"/> last ran.</summary>
        public bool ReservedThisFrame;

        public readonly bool IsFree => Holder == Entity.Null;

        /// <summary>Gives the slot to <paramref name="holder"/> for this frame.</summary>
        public void Reserve(Entity holder)
        {
            Holder = holder;
            ReservedThisFrame = true;
        }

        /// <summary>Reserves the slot unless another entity has it.</summary>
        public bool TryReserve(Entity holder)
        {
            if (!IsFree && Holder != holder)
            {
                return false;
            }

            Reserve(holder);
            return true;
        }

        public void Release(Entity holder)
        {
            if (Holder == holder)
            {
                this = default;
            }
        }

        /// <summary>Frees the slot if nobody reserved it, then starts a new frame.</summary>
        public void Expire()
        {
            if (!ReservedThisFrame)
            {
                Holder = Entity.Null;
            }

            ReservedThisFrame = false;
        }
    }
}
