using System.Collections.Generic;
using UnityEngine;

namespace LoopSortTest.Core.Models
{
    public class ConveyorTrack
    {
        public List<Vector3> Waypoints { get; private set; }
        public float TotalLength { get; private set; }
        public float BeltHalfWidth { get; private set; }

        private float[] _cumulativeLengths;

        public ConveyorTrack(List<Vector3> waypoints, float beltWidth)
        {
            Waypoints = waypoints;
            BeltHalfWidth = beltWidth * 0.5f;
            BuildLengthCache();
        }

        private void BuildLengthCache()
        {
            _cumulativeLengths = new float[Waypoints.Count];
            _cumulativeLengths[0] = 0f;
            float total = 0f;

            for (int i = 1; i < Waypoints.Count; i++)
            {
                total += Vector3.Distance(Waypoints[i - 1], Waypoints[i]);
                _cumulativeLengths[i] = total;
            }

            total += Vector3.Distance(Waypoints[Waypoints.Count - 1], Waypoints[0]);
            TotalLength = total;
        }

        public Vector3 GetPositionAtT(float t)
        {
            t = ((t % 1f) + 1f) % 1f;
            float targetDist = t * TotalLength;

            for (int i = 0; i < _cumulativeLengths.Length; i++)
            {
                int next = (i + 1) % Waypoints.Count;
                float segEnd = (next == 0) ? TotalLength : _cumulativeLengths[next];
                if (targetDist <= segEnd)
                {
                    float segStart = _cumulativeLengths[i];
                    float segLen = segEnd - segStart;
                    float localT = (segLen > 0f) ? (targetDist - segStart) / segLen : 0f;
                    return Vector3.Lerp(Waypoints[i], Waypoints[next], localT);
                }
            }

            return Waypoints[0];
        }

        public Vector3 GetTangentAtT(float t)
        {
            float epsilon = 0.0005f;
            Vector3 p0 = GetPositionAtT(t - epsilon);
            Vector3 p1 = GetPositionAtT(t + epsilon);
            Vector3 dir = p1 - p0;
            dir.y = 0f;
            return dir.normalized;
        }

        /// <summary>Tangent'e dik, XZ düzleminde sağa bakan normal.</summary>
        public Vector3 GetNormalAtT(float t)
        {
            Vector3 tan = GetTangentAtT(t);
            return new Vector3(-tan.z, 0f, tan.x);
        }

        /// <summary>Belt yüzey hızı (tangent * speed).</summary>
        public Vector3 GetBeltVelocityAt(float t, float beltSpeed)
        {
            return GetTangentAtT(t) * beltSpeed;
        }

        /// <summary>
        /// Dünya pozisyonuna en yakın track T değerini bulur.
        /// Ayrıca merkez çizgiye dik mesafeyi ve en yakın merkez noktayı döndürür.
        /// </summary>
        public float GetNearestT(Vector3 worldPos, out Vector3 nearestCenterPoint, out float signedDistance)
        {
            float bestDistSq = float.MaxValue;
            float bestT = 0f;
            Vector3 bestPoint = Waypoints[0];

            for (int i = 0; i < Waypoints.Count; i++)
            {
                int next = (i + 1) % Waypoints.Count;
                Vector3 a = Waypoints[i];
                Vector3 b = Waypoints[next];

                Vector3 ab = b - a;
                float segLen = ab.magnitude;
                if (segLen < 0.0001f) continue;

                // worldPos'un segment üzerindeki projeksiyonu
                float proj = Vector3.Dot(worldPos - a, ab) / (segLen * segLen);
                proj = Mathf.Clamp01(proj);
                Vector3 closest = a + ab * proj;

                float dSq = (worldPos - closest).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestPoint = closest;

                    // Bu noktanın T değerini hesapla
                    float segStart = _cumulativeLengths[i];
                    float segEnd = (next == 0) ? TotalLength : _cumulativeLengths[next];
                    float dist = segStart + (segEnd - segStart) * proj;
                    bestT = dist / TotalLength;
                }
            }

            nearestCenterPoint = bestPoint;

            // İşaretli mesafe: normal yönünde pozitif, tersi negatif
            Vector3 normal = GetNormalAtT(bestT);
            Vector3 offset = worldPos - bestPoint;
            offset.y = 0f;
            signedDistance = Vector3.Dot(offset, normal);

            return bestT;
        }

        /// <summary>
        /// Pozisyonu belt sınırları içine clamp eder.
        /// halfCubeSize: küpün yarı genişliği (sınırdan içeri girme payı).
        /// Dönen değer: düzeltilmiş pozisyon.
        /// </summary>
        public Vector3 ClampToBelt(Vector3 pos, float halfCubeSize)
        {
            float t = GetNearestT(pos, out Vector3 center, out float signedDist);
            float maxDist = BeltHalfWidth - halfCubeSize;
            if (maxDist < 0f) maxDist = 0f;

            if (Mathf.Abs(signedDist) > maxDist)
            {
                Vector3 normal = GetNormalAtT(t);
                float clampedDist = Mathf.Clamp(signedDist, -maxDist, maxDist);
                pos = center + normal * clampedDist;
            }

            pos.y = 0f;
            return pos;
        }

        /// <summary>
        /// Sınır noktalarını döndürür (iç ve dış kenar). Gizmo çizimi için.
        /// </summary>
        public void GetBoundaryPoints(out List<Vector3> inner, out List<Vector3> outer)
        {
            inner = new List<Vector3>(Waypoints.Count);
            outer = new List<Vector3>(Waypoints.Count);

            for (int i = 0; i < Waypoints.Count; i++)
            {
                float t = (float)i / Waypoints.Count;
                Vector3 center = Waypoints[i];
                Vector3 normal = GetNormalAtT(t);
                inner.Add(center - normal * BeltHalfWidth);
                outer.Add(center + normal * BeltHalfWidth);
            }
        }

        public float WorldDistanceToT(float worldDistance)
        {
            return worldDistance / TotalLength;
        }
    }
}
