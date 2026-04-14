using System.Collections.Generic;
using UnityEngine;
using LoopSortTest.Config;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Models;

namespace LoopSortTest.Algorithms
{
    /// <summary>
    /// SAT: Belt sürtünmesiyle hareket, Separating Axis Theorem ile
    /// dönmüş küpler arası çarpışma, hard clamp + velocity reflect ile sınır.
    /// </summary>
    public class SATPhysics : IPhysicsAlgorithm
    {
        public string AlgorithmName => "SAT";

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

                // Sınır
                ApplyBoundary(cube, track, config);

                // Rotasyonu hıza göre güncelle (SAT dönmüş küplerle çalışır)
                UpdateRotation(cube, dt);
            }

            // SAT çarpışma
            for (int i = 0; i < cubes.Count; i++)
            {
                for (int j = i + 1; j < cubes.Count; j++)
                {
                    ResolveSATCollision(cubes[i], cubes[j]);
                }
            }
        }

        private void ResolveSATCollision(ConveyorCube a, ConveyorCube b)
        {
            // Her küpün 2 ekseni (XZ düzleminde dönmüş)
            Vector3[] axesA = GetAxes(a.Rotation);
            Vector3[] axesB = GetAxes(b.Rotation);

            float minOverlap = float.MaxValue;
            Vector3 minAxis = Vector3.zero;

            // 4 eksen test et
            Vector3[] allAxes = { axesA[0], axesA[1], axesB[0], axesB[1] };

            foreach (var axis in allAxes)
            {
                if (axis.sqrMagnitude < 0.0001f) continue;

                ProjectBox(a, axis, out float minA, out float maxA);
                ProjectBox(b, axis, out float minB, out float maxB);

                float overlap = Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB);
                if (overlap <= 0f) return; // Ayırma ekseni bulundu

                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    minAxis = axis;
                }
            }

            // Çarpışma var — itme yönünü belirle
            Vector3 diff = b.Position - a.Position;
            if (Vector3.Dot(diff, minAxis) < 0f)
                minAxis = -minAxis;

            a.Position -= minAxis * minOverlap * 0.5f;
            b.Position += minAxis * minOverlap * 0.5f;

            a.Position.y = a.Size.y * 0.5f;
            b.Position.y = b.Size.y * 0.5f;

            // Hız: normal bileşeni takas
            float relVn = Vector3.Dot(b.Velocity - a.Velocity, minAxis);
            if (relVn < 0f)
            {
                Vector3 impulse = minAxis * relVn * 0.5f;
                a.Velocity += impulse;
                b.Velocity -= impulse;
            }
        }

        private Vector3[] GetAxes(Quaternion rot)
        {
            Vector3 right = rot * Vector3.right;
            Vector3 forward = rot * Vector3.forward;
            right.y = 0f; right.Normalize();
            forward.y = 0f; forward.Normalize();
            return new[] { right, forward };
        }

        private void ProjectBox(ConveyorCube cube, Vector3 axis, out float min, out float max)
        {
            Vector3 half = cube.Size * 0.5f;
            Vector3 right = cube.Rotation * Vector3.right;
            Vector3 forward = cube.Rotation * Vector3.forward;

            float extent = Mathf.Abs(Vector3.Dot(right * half.x, axis))
                         + Mathf.Abs(Vector3.Dot(forward * half.z, axis));

            float center = Vector3.Dot(cube.Position, axis);
            min = center - extent;
            max = center + extent;
        }

        private void ApplyBoundary(ConveyorCube cube, ConveyorTrack track, ConveyorConfig config)
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

                float vn = Vector3.Dot(cube.Velocity, normal);
                if ((signedDist > 0 && vn > 0) || (signedDist < 0 && vn < 0))
                {
                    cube.Velocity -= normal * vn * (1f + config.BoundaryBounciness);
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
