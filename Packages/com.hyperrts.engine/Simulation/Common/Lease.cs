using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// A slot one entity holds by renewing it every frame. A holder that dies or stops renewing loses it when the
    /// owning system calls <see cref="Expire"/>, so nothing has to check the holder is still alive.
    /// </summary>
    public struct Lease
    {
        public Entity Holder;

        /// <summary>The holder renewed it since <see cref="Expire"/> last ran.</summary>
        public bool Renewed;

        public readonly bool IsFree => Holder == Entity.Null;

        /// <summary>Gives the slot to <paramref name="holder"/> and keeps it for another frame.</summary>
        public void Hold(Entity holder)
        {
            Holder = holder;
            Renewed = true;
        }

        /// <summary>Holds the slot unless another entity has it.</summary>
        public bool TryHold(Entity holder)
        {
            if (!IsFree && Holder != holder)
            {
                return false;
            }

            Hold(holder);
            return true;
        }

        public void Release(Entity holder)
        {
            if (Holder == holder)
            {
                this = default;
            }
        }

        /// <summary>Frees the slot if nobody renewed it, then starts a new frame.</summary>
        public void Expire()
        {
            if (!Renewed)
            {
                Holder = Entity.Null;
            }

            Renewed = false;
        }
    }
}
