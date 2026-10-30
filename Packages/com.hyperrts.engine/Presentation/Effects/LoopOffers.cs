using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>This frame's streamed loops whose rigs carry a <typeparamref name="T"/>, nearest the camera first once sorted.</summary>
    public sealed class LoopOffers<T> where T : Component
    {
        /// <summary>One streamed loop: its rig's component, where it plays, how strongly, its stream key and seconds since it began.</summary>
        public struct Offer
        {
            public T Source;
            public Vector3 Position;
            public float Strength;
            public uint Key;
            public float Age;
            public float DistanceSq;
        }

        private static readonly Comparison<Offer> Nearer = (a, b) => a.DistanceSq.CompareTo(b.DistanceSq);

        private readonly List<Offer> _offers = new();

        public int Count => _offers.Count;

        public Offer this[int index] => _offers[index];

        public void Clear() => _offers.Clear();

        /// <summary>Takes the loop when its rig carries a <typeparamref name="T"/>.</summary>
        public void Add(EffectRig rig, Vector3 position, float strength, uint key, float age)
        {
            if (rig.TryGetComponent<T>(out var source))
            {
                _offers.Add(new Offer { Source = source, Position = position, Strength = strength, Key = key, Age = age });
            }
        }

        public void SortFrom(Vector3 eye)
        {
            for (var i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                offer.DistanceSq = (offer.Position - eye).sqrMagnitude;
                _offers[i] = offer;
            }

            _offers.Sort(Nearer);
        }
    }
}
