using UnityEngine;

// Sphere - Diamond - Cube
// Grup halinde spawn

namespace LoopSortTest.Core.Models
{
    public class ConveyorCube
    {
        public int Id;
        public Color Color;

        /// <summary>Dünya uzayında pozisyon (fizik tarafından yönetilir).</summary>
        public Vector3 Position;

        /// <summary>Dünya uzayında hız (fizik tarafından yönetilir).</summary>
        public Vector3 Velocity;

        /// <summary>Görsel rotasyon.</summary>
        public Quaternion Rotation;

        /// <summary>Küpün boyutu.</summary>
        public Vector3 Size;

        /// <summary>Verlet algoritması için önceki pozisyon.</summary>
        public Vector3 PrevPosition;
    }
}
