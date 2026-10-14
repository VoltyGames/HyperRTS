using System;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace HyperRTS.Presentation.Environment.Ambient
{
    /// <summary>
    /// One flock circling a ground anchor with boids-lite steering (pull onto its ring, separation, noise). An impact
    /// near it scatters the birds away, climbing; after the return delay they settle over a new, distant anchor.
    /// </summary>
    public sealed class BirdFlock
    {
        public const float ResettleDistance = 150f;
        public const float CruiseSpeed = 9f;
        public const float FleeSpeed = 18f;

        private const float MinRing = 12f;
        private const float MaxRing = 30f;
        private const float RingPull = 0.6f;
        private const float Steering = 2f;
        private const float SeparationRadius = 2.5f;
        private const float Separation = 4f;
        private const float Noise = 1.5f;
        private const float ClimbShare = 0.4f;
        private const float ScatterCeiling = 20f;
        private const float GlideFlaps = 5f;
        private const float FleeFlaps = 14f;
        private const int AnchorAttempts = 16;

        private readonly float3[] _positions;
        private readonly float3[] _velocities;
        private readonly float[] _phases;
        private readonly float[] _radii;
        private readonly float[] _altitudes;
        private readonly float[] _flaps;
        private readonly BirdSettings _settings;
        private readonly SkyArea _area;
        private Random _random;
        private float _time;
        private float _scatterLeft;

        public BirdFlock(int count, in BirdSettings settings, in SkyArea area, uint seed)
        {
            _settings = settings;
            _area = area;
            _random = new Random(seed | 1u);
            _positions = new float3[count];
            _velocities = new float3[count];
            _phases = new float[count];
            _radii = new float[count];
            _altitudes = new float[count];
            _flaps = new float[count];
            Anchor = area.RandomPoint(ref _random);
            for (var i = 0; i < count; i++)
            {
                _phases[i] = _random.NextFloat(2f * math.PI);
                _radii[i] = _random.NextFloat(MinRing, MaxRing);
                _altitudes[i] = _random.NextFloat(settings.Height.x, settings.Height.y);
                _flaps[i] = _random.NextFloat(4f);
                _positions[i] = RingPoint(i);
            }
        }

        /// <summary>The ground point the flock circles.</summary>
        public float3 Anchor { get; private set; }

        public bool Scattered => _scatterLeft > 0f;
        public int Count => _positions.Length;
        public float3 Position(int bird) => _positions[bird];
        public float3 Velocity(int bird) => _velocities[bird];

        /// <summary>The 2×2 flipbook frame (0–3) showing the bird's wing pose.</summary>
        public int Frame(int bird) => (int)_flaps[bird] & 3;

        public float3 Center
        {
            get
            {
                var sum = float3.zero;
                foreach (var position in _positions)
                {
                    sum += position;
                }

                return sum / math.max(1, Count);
            }
        }

        /// <summary>Advances the birds; <paramref name="impacts"/> are this frame's hits.</summary>
        public void Step(float deltaTime, ReadOnlySpan<float3> impacts)
        {
            if (!Scattered && TryFindThreat(impacts, out var threat))
            {
                Scatter(threat);
            }
            else if (Scattered)
            {
                _scatterLeft -= deltaTime;
                if (!Scattered)
                {
                    Anchor = DistantAnchor();
                }
            }
            else
            {
                _time += deltaTime;
            }

            for (var i = 0; i < Count; i++)
            {
                StepBird(i, deltaTime);
            }
        }

        private bool TryFindThreat(ReadOnlySpan<float3> impacts, out float3 threat)
        {
            var center = Center.xz;
            foreach (var impact in impacts)
            {
                if (math.distance(impact.xz, center) <= _settings.ScatterRadius)
                {
                    threat = impact;
                    return true;
                }
            }

            threat = default;
            return false;
        }

        private void Scatter(float3 threat)
        {
            _scatterLeft = math.max(1e-3f, _random.NextFloat(_settings.ReturnDelay.x, _settings.ReturnDelay.y));
            for (var i = 0; i < Count; i++)
            {
                var away = math.normalizesafe(_positions[i].xz - threat.xz, _random.NextFloat2Direction());
                away = math.normalize(away + _random.NextFloat2Direction() * 0.3f);
                _velocities[i] = math.normalize(new float3(away.x, ClimbShare, away.y)) * FleeSpeed;
            }
        }

        /// <summary>A random anchor at least <see cref="ResettleDistance"/> away; the farthest tried on a small map.</summary>
        private float3 DistantAnchor()
        {
            var best = Anchor;
            var bestDistance = -1f;
            for (var attempt = 0; attempt < AnchorAttempts; attempt++)
            {
                var candidate = _area.RandomPoint(ref _random);
                var distance = math.distance(candidate.xz, Anchor.xz);
                if (distance >= ResettleDistance)
                {
                    return candidate;
                }

                if (distance > bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void StepBird(int bird, float deltaTime)
        {
            var desired = Scattered ? Fleeing(bird) : Circling(bird);
            desired += Separating(bird) + _random.NextFloat3Direction() * Noise;
            var blend = 1f - math.exp(-Steering * deltaTime);
            _velocities[bird] = math.lerp(_velocities[bird], desired, blend);
            _positions[bird] += _velocities[bird] * deltaTime;
            _flaps[bird] += deltaTime * (Scattered ? FleeFlaps : GlideFlaps);
        }

        private float3 Circling(int bird)
        {
            var angle = _phases[bird] + _time * CruiseSpeed / _radii[bird];
            var tangent = new float3(-math.sin(angle), 0f, math.cos(angle)) * CruiseSpeed;
            var pull = (RingPoint(bird) - _positions[bird]) * RingPull;
            var desired = tangent + pull;
            var speed = math.length(desired);
            return speed > FleeSpeed ? desired * (FleeSpeed / speed) : desired;
        }

        private float3 Fleeing(int bird)
        {
            var velocity = _velocities[bird];
            var position = _positions[bird];
            var ceiling = _area.Ground(position.xz) + _settings.Height.y + ScatterCeiling;
            if (position.y >= ceiling)
            {
                velocity.y = math.min(velocity.y, 0f);
            }

            return velocity;
        }

        private float3 Separating(int bird)
        {
            var push = float3.zero;
            for (var other = 0; other < Count; other++)
            {
                var offset = _positions[bird] - _positions[other];
                var distanceSq = math.lengthsq(offset);
                if (other == bird || distanceSq >= SeparationRadius * SeparationRadius)
                {
                    continue;
                }

                push += offset / math.max(distanceSq, 0.01f);
            }

            return push * Separation;
        }

        private float3 RingPoint(int bird)
        {
            var angle = _phases[bird] + _time * CruiseSpeed / _radii[bird];
            var ring = new float3(math.cos(angle), 0f, math.sin(angle)) * _radii[bird];
            return new float3(Anchor.x + ring.x, Anchor.y + _altitudes[bird], Anchor.z + ring.z);
        }
    }
}
