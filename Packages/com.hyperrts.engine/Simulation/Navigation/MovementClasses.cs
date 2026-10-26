using System;
using Unity.Collections;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// The <see cref="MovementClass"/>es a match uses, registered once from unit prefabs and live agents, and the
    /// field keys they read. Indices never change, so fields and regions are laid out by them.
    /// </summary>
    public struct MovementClasses
    {
        /// <summary>Most classes one match may register; each costs a region layer and maybe a field.</summary>
        public const int Capacity = 16;

        public NativeList<MovementClass> Classes;

        /// <summary>Distinct <see cref="MovementClass.FieldKey"/>s, by field slot.</summary>
        public NativeList<MovementClass> Keys;

        public readonly bool IsCreated => Classes.IsCreated;

        public static MovementClasses Create(Allocator allocator) => new()
        {
            Classes = new NativeList<MovementClass>(Capacity, allocator),
            Keys = new NativeList<MovementClass>(Capacity, allocator),
        };

        public void Dispose()
        {
            Classes.Dispose();
            Keys.Dispose();
        }

        /// <summary>The class's index, or -1 when it isn't registered yet.</summary>
        public readonly int Find(in MovementClass movementClass) => IndexIn(Classes, movementClass);

        /// <summary>The field slot a registered class reads.</summary>
        public readonly int FieldSlot(int classIndex) => IndexIn(Keys, Classes[classIndex].FieldKey);

        /// <summary>Adds the class (and its field key) when new; throws once <see cref="Capacity"/> is reached.</summary>
        public void Register(in MovementClass movementClass)
        {
            if (Find(movementClass) >= 0)
            {
                return;
            }

            if (Classes.Length == Capacity)
            {
                throw new InvalidOperationException(
                    "More than 16 movement classes: give units fewer distinct radii, layers or crush limits.");
            }

            Classes.Add(movementClass);
            if (IndexIn(Keys, movementClass.FieldKey) < 0)
            {
                Keys.Add(movementClass.FieldKey);
            }
        }

        private static int IndexIn(in NativeList<MovementClass> list, in MovementClass movementClass)
        {
            for (var i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(movementClass))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
