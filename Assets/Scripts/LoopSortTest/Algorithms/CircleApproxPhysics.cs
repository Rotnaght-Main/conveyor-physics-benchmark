using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Algorithms
{
    /// <summary>
    /// Circle Approx: Belt sürtünmesiyle hareket, daire yaklaşımıyla çarpışma
    /// (en hızlı algoritma), merkeze doğru yumuşak kuvvet ile sınır kısıtı.
    /// </summary>
    public class CircleApproxPhysics : IPhysicsAlgorithm
    {
        public string AlgorithmName => "Circle Approx";

        public void Tick(List<ConveyorCube> cubes, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];

                // Belt sürtünme
                float t = track.GetNearestT(cube.Position, out _, out _);
                Vector3 beltVel = track.GetBeltVelocityAt(t, config.ConveyorSpeed);
                Vector3 friction = config.FrictionCoefficient * (beltVel - cube.Velocity);
                cube.Velocity += friction * dt;

                cube.Position += cube.Velocity * dt;
                cube.Position.y = cube.Size.y * 0.5f;

                // Sınır: merkeze doğru yumuşak kuvvet
                ApplySoftBoundary(cube, track, config, dt);

                UpdateRotation(cube, dt);
            }

            // Circle çarpışma
            for (int i = 0; i < cubes.Count; i++)
            {
                for (int j = i + 1; j < cubes.Count; j++)
                {
                    ResolveCircleCollision(cubes[i], cubes[j]);
                }
            }
        }

        private void ApplySoftBoundary(ConveyorCube cube, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            float halfCube = Mathf.Max(cube.Size.x, cube.Size.z) * 0.5f;
            float t = track.GetNearestT(cube.Position, out Vector3 center, out float signedDist);
            float maxDist = track.BeltHalfWidth - halfCube;
            if (maxDist < 0f) maxDist = 0f;

            float penetration = Mathf.Abs(signedDist) - maxDist;
            if (penetration > 0f)
            {
                // Merkeze doğru güçlü çekme
                Vector3 toCenter = (center - cube.Position);
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.0001f)
                {
                    toCenter.Normalize();
                    cube.Velocity += toCenter * penetration * 40f * dt;
                }

                // Hard clamp (aşırı penetrasyonu önle)
                if (penetration > 0.1f)
                {
                    Vector3 normal = track.GetNormalAtT(t);
                    float clampedDist = Mathf.Clamp(signedDist, -maxDist, maxDist);
                    cube.Position = center + normal * clampedDist;
                    cube.Position.y = cube.Size.y * 0.5f;
                }
            }
        }

        private void ResolveCircleCollision(ConveyorCube a, ConveyorCube b)
        {
            Vector3 diff = b.Position - a.Position;
            diff.y = 0f;
            float dist = diff.magnitude;

            float rA = Mathf.Max(a.Size.x, a.Size.z) * 0.5f;
            float rB = Mathf.Max(b.Size.x, b.Size.z) * 0.5f;
            float minDist = rA + rB;

            if (dist < minDist && dist > 0.0001f)
            {
                Vector3 n = diff / dist;
                float overlap = minDist - dist;

                a.Position -= n * overlap * 0.5f;
                b.Position += n * overlap * 0.5f;
                a.Position.y = a.Size.y * 0.5f;
                b.Position.y = b.Size.y * 0.5f;

                float relVn = Vector3.Dot(b.Velocity - a.Velocity, n);
                if (relVn < 0f)
                {
                    Vector3 impulse = n * relVn * 0.5f;
                    a.Velocity += impulse;
                    b.Velocity -= impulse;
                }
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
