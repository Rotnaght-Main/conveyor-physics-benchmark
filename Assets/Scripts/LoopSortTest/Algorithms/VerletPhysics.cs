using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Algorithms
{
    /// <summary>
    /// Verlet: Belt sürtünmesi acceleration olarak uygulanır,
    /// pozisyon Verlet integration ile güncellenir,
    /// constraint-based çarpışma ve sınır çözümü.
    /// </summary>
    public class VerletPhysics : IPhysicsAlgorithm
    {
        public string AlgorithmName => "Verlet";

        public void Tick(List<ConveyorCube> cubes, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            // 1. Verlet integration + belt sürtünme
            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];

                // Belt sürtünme → ivme
                float t = track.GetNearestT(cube.Position, out _, out _);
                Vector3 beltVel = track.GetBeltVelocityAt(t, config.ConveyorSpeed);

                // Verlet'de hız: (current - prev) / dt
                Vector3 implicitVel = (cube.Position - cube.PrevPosition);
                Vector3 friction = config.FrictionCoefficient * (beltVel * dt - implicitVel);

                Vector3 newPos = 2f * cube.Position - cube.PrevPosition + friction * dt;
                cube.PrevPosition = cube.Position;
                cube.Position = newPos;

                cube.Position.y = cube.Size.y * 0.5f;
                cube.PrevPosition.y = cube.Size.y * 0.5f;
            }

            // 2. Constraint solve (iteratif)
            int iterations = config.VerletIterations;
            for (int iter = 0; iter < iterations; iter++)
            {
                // Küp-küp mesafe kısıtı
                for (int i = 0; i < cubes.Count; i++)
                {
                    for (int j = i + 1; j < cubes.Count; j++)
                    {
                        ResolveDistanceConstraint(cubes[i], cubes[j]);
                    }
                }

                // Sınır kısıtı
                for (int i = 0; i < cubes.Count; i++)
                {
                    ApplyBoundaryConstraint(cubes[i], track);
                }
            }

            // 3. Velocity ve rotasyon güncelle
            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];
                cube.Velocity = (cube.Position - cube.PrevPosition) / Mathf.Max(dt, 0.0001f);
                UpdateRotation(cube, dt);
            }
        }

        private void ResolveDistanceConstraint(ConveyorCube a, ConveyorCube b)
        {
            Vector3 diff = b.Position - a.Position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minDist = (Mathf.Max(a.Size.x, a.Size.z) + Mathf.Max(b.Size.x, b.Size.z)) * 0.5f;

            if (dist < minDist && dist > 0.0001f)
            {
                Vector3 n = diff / dist;
                float correction = (minDist - dist) * 0.5f;

                a.Position -= n * correction;
                b.Position += n * correction;
                a.Position.y = a.Size.y * 0.5f;
                b.Position.y = b.Size.y * 0.5f;
            }
        }

        private void ApplyBoundaryConstraint(ConveyorCube cube, ConveyorTrack track)
        {
            float halfCube = Mathf.Max(cube.Size.x, cube.Size.z) * 0.5f;
            float t = track.GetNearestT(cube.Position, out Vector3 center, out float signedDist);
            float maxDist = track.BeltHalfWidth - halfCube;
            if (maxDist < 0f) maxDist = 0f;

            if (Mathf.Abs(signedDist) > maxDist)
            {
                Vector3 normal = track.GetNormalAtT(t);
                float clampedDist = Mathf.Clamp(signedDist, -maxDist, maxDist);
                cube.Position = center + normal * clampedDist;
                cube.Position.y = cube.Size.y * 0.5f;
            }
        }

        private void UpdateRotation(ConveyorCube cube, float dt)
        {
            float speed = cube.Velocity.magnitude;
            if (speed < 0.001f) return;
            Vector3 dir = cube.Velocity.normalized;
            Vector3 rollAxis = Vector3.Cross(Vector3.up, dir);
            float rollAngle = (speed * dt / (cube.Size.y * 0.5f)) * Mathf.Rad2Deg;
            cube.Rotation = Quaternion.Normalize(Quaternion.AngleAxis(rollAngle, rollAxis) * cube.Rotation);
        }

        public void Dispose() { }
    }
}
