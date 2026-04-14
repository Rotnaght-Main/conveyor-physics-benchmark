using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Algorithms
{
    /// <summary>
    /// Spatial Hash: Belt sürtünmesiyle hareket, spatial grid ile hızlı çarpışma,
    /// spring-force ile sınır kısıtı (yumuşak duvar).
    /// </summary>
    public class SpatialHashPhysics : IPhysicsAlgorithm
    {
        public string AlgorithmName => "Spatial Hash";

        private readonly Dictionary<long, List<ConveyorCube>> _grid = new();

        public void Tick(List<ConveyorCube> cubes, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            float cellSize = config.HashCellSize;

            // 1. Belt sürtünmesi + pozisyon güncelleme
            for (int i = 0; i < cubes.Count; i++)
            {
                var cube = cubes[i];

                float t = track.GetNearestT(cube.Position, out _, out _);
                Vector3 beltVel = track.GetBeltVelocityAt(t, config.ConveyorSpeed);
                Vector3 friction = config.FrictionCoefficient * (beltVel - cube.Velocity);
                cube.Velocity += friction * dt;

                cube.Position += cube.Velocity * dt;
                cube.Position.y = cube.Size.y * 0.5f;

                // Sınır: spring force (yumuşak duvar)
                ApplyBoundarySpring(cube, track, config, dt);

                UpdateRotation(cube, dt);
            }

            // 2. Spatial hash grid oluştur
            _grid.Clear();
            for (int i = 0; i < cubes.Count; i++)
            {
                long key = HashKey(cubes[i].Position, cellSize);
                if (!_grid.TryGetValue(key, out var list))
                {
                    list = new List<ConveyorCube>();
                    _grid[key] = list;
                }
                list.Add(cubes[i]);
            }

            // 3. Komşu hücrelerdeki çiftler için çarpışma
            for (int i = 0; i < cubes.Count; i++)
            {
                int cx = Mathf.FloorToInt(cubes[i].Position.x / cellSize);
                int cz = Mathf.FloorToInt(cubes[i].Position.z / cellSize);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        long key = PackKey(cx + dx, cz + dz);
                        if (!_grid.TryGetValue(key, out var list)) continue;

                        for (int j = 0; j < list.Count; j++)
                        {
                            if (list[j].Id <= cubes[i].Id) continue;
                            ResolveSphereCollision(cubes[i], list[j]);
                        }
                    }
                }
            }
        }

        private void ApplyBoundarySpring(ConveyorCube cube, ConveyorTrack track, ConveyorConfig config, float dt)
        {
            float halfCube = Mathf.Max(cube.Size.x, cube.Size.z) * 0.5f;
            float t = track.GetNearestT(cube.Position, out Vector3 center, out float signedDist);
            float maxDist = track.BeltHalfWidth - halfCube;
            if (maxDist < 0f) maxDist = 0f;

            float penetration = Mathf.Abs(signedDist) - maxDist;
            if (penetration > 0f)
            {
                Vector3 normal = track.GetNormalAtT(t);
                float pushDir = -Mathf.Sign(signedDist);
                float springForce = penetration * 50f;
                cube.Velocity += normal * pushDir * springForce * dt;

                // Damping
                float vn = Vector3.Dot(cube.Velocity, normal);
                cube.Velocity -= normal * vn * 0.2f;
            }
        }

        private void ResolveSphereCollision(ConveyorCube a, ConveyorCube b)
        {
            Vector3 diff = b.Position - a.Position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minDist = (Mathf.Max(a.Size.x, a.Size.z) + Mathf.Max(b.Size.x, b.Size.z)) * 0.5f;

            if (dist < minDist && dist > 0.0001f)
            {
                Vector3 n = diff / dist;
                float overlap = minDist - dist;

                a.Position -= n * overlap * 0.5f;
                b.Position += n * overlap * 0.5f;

                float relVn = Vector3.Dot(b.Velocity - a.Velocity, n);
                if (relVn < 0f)
                {
                    Vector3 impulse = n * relVn * 0.5f;
                    a.Velocity += impulse;
                    b.Velocity -= impulse;
                }
            }
        }

        private long HashKey(Vector3 pos, float cellSize)
        {
            int cx = Mathf.FloorToInt(pos.x / cellSize);
            int cz = Mathf.FloorToInt(pos.z / cellSize);
            return PackKey(cx, cz);
        }

        private long PackKey(int x, int z)
        {
            return ((long)x << 32) | (uint)z;
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

        public void Dispose()
        {
            _grid.Clear();
        }
    }
}
