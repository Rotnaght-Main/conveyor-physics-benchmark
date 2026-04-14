using UnityEngine;

namespace LoopSortTest.Config
{
    public enum ConveyorShape
    {
        Cube,
        Sphere,
        Diamond
    }

    [CreateAssetMenu(menuName = "LoopSort/ConveyorConfig")]
    public class ConveyorConfig : ScriptableObject
    {
        [Header("Track")]
        public float OvalWidth = 6f;
        public float OvalHeight = 4f;
        public int WaypointCount = 64;
        public float BeltWidth = 1.2f;

        [Header("Cubes")]
        public int CubeCount = 1;
        public ConveyorShape Shape = ConveyorShape.Cube;
        public Material ShapeMaterial;
        public Vector3 CubeSize = new(0.35f, 0.35f, 0.35f);

        [Header("Belt")]
        public Material BeltMaterial;
        public float ConveyorSpeed = 3f;
        public float FrictionCoefficient = 6f;

        [Header("Boundary")]
        public float BoundaryBounciness = 0.3f;

        [Header("Physics - Verlet")]
        public int VerletIterations = 4;

        [Header("Physics - Spatial Hash")]
        public float HashCellSize = 0.5f;

        [Header("Debug")]
        public bool DrawGizmos = true;
    }
}
