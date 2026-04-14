using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Algorithms
{
    /// <summary>
    /// AABB: Belt sürtünmesiyle hareket, AABB overlap ile çarpışma,
    /// hard clamp ile sınır kısıtı.
    /// </summary>
    public class AABBPhysics : IPhysicsAlgorithm
    {
        public string AlgorithmName => "AABB";

        public void Tick(List<ConveyorCube> cubes, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];

                // 1. Belt sürtünme kuvveti
                float t = track.GetNearestT(cube.Position, out _, out _);
                Vector3 beltVel = track.GetBeltVelocityAt(t, config.ConveyorSpeed);
                Vector3 friction = config.FrictionCoefficient * (beltVel - cube.Velocity);
                cube.Velocity += friction * dt;

                // 2. Pozisyon güncelle
                cube.Position += cube.Velocity * dt;

                // 3. Y sabit tut (belt yüzeyi)
                cube.Position.y = cube.Size.y * 0.5f;

                // 4. Sınır kısıtı: hard clamp + bounce
                ApplyBoundary(cube, track, config);

                // 5. Rotasyonu hıza göre güncelle
                UpdateRotation(cube, dt);
            }

            // 6. AABB çarpışma çözümü
            for (int i = 0; i < cubes.Count; i++)
            {
                for (int j = i + 1; j < cubes.Count; j++)
                {
                    ResolveAABBCollision(cubes[i], cubes[j]);
                }
            }
        }

        private void ApplyBoundary(ConveyorCube cube, ConveyorTrack track, ConveyorConfig config)
        {
            float halfCube = Mathf.Max(cube.Size.x, cube.Size.z) * 0.5f;
            float t = track.GetNearestT(cube.Position, out Vector3 center, out float signedDist);
            float maxDist = track.BeltHalfWidth - halfCube;
            if (maxDist < 0f) maxDist = 0f;

            if (Mathf.Abs(signedDist) > maxDist)
            {
                // Clamp pozisyon
                Vector3 normal = track.GetNormalAtT(t);
                float clampedDist = Mathf.Clamp(signedDist, -maxDist, maxDist);
                cube.Position = center + normal * clampedDist;
                cube.Position.y = cube.Size.y * 0.5f;

                // Hızın normal bileşenini yansıt (bounce)
                float vn = Vector3.Dot(cube.Velocity, normal);
                if ((signedDist > 0 && vn > 0) || (signedDist < 0 && vn < 0))
                {
                    cube.Velocity -= normal * vn * (1f + config.BoundaryBounciness);
                }
            }
        }

        private void ResolveAABBCollision(ConveyorCube a, ConveyorCube b)
        {
            Vector3 diff = b.Position - a.Position;
            Vector3 halfA = a.Size * 0.5f;
            Vector3 halfB = b.Size * 0.5f;

            float overlapX = (halfA.x + halfB.x) - Mathf.Abs(diff.x);
            float overlapZ = (halfA.z + halfB.z) - Mathf.Abs(diff.z);

            if (overlapX <= 0f || overlapZ <= 0f) return;

            // En küçük overlap ekseninde it
            if (overlapX < overlapZ)
            {
                float push = overlapX * 0.5f * Mathf.Sign(diff.x);
                a.Position.x -= push;
                b.Position.x += push;

                // Hız transferi
                float avgVx = (a.Velocity.x + b.Velocity.x) * 0.5f;
                a.Velocity.x = avgVx;
                b.Velocity.x = avgVx;
            }
            else
            {
                float push = overlapZ * 0.5f * Mathf.Sign(diff.z);
                a.Position.z -= push;
                b.Position.z += push;

                float avgVz = (a.Velocity.z + b.Velocity.z) * 0.5f;
                a.Velocity.z = avgVz;
                b.Velocity.z = avgVz;
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
