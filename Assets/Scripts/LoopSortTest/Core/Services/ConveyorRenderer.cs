using System.Collections.Generic;
using UnityEngine;
using Zenject;
using LoopSortTest.Core.Models;
using LoopSortTest.Config;

namespace LoopSortTest.Core.Services
{
    public class ConveyorRenderer : IInitializable
    {
        [Inject] private readonly ConveyorConfig _config;
        [Inject] private readonly ConveyorTrack _track;

        private Matrix4x4[] _matrices;
        private Mesh _cubeMesh;
        private Material _cubeMaterial;

        // Belt render
        private Mesh _beltMesh;
        private Material _beltMaterial;
        private MaterialPropertyBlock _beltMpb;
        private Matrix4x4 _beltMatrix;
        private int _beltSegmentCount;

        // Belt animation
        private float _beltScrollOffset;
        private Color[] _beltVertexColors;

        public void Initialize()
        {
            _cubeMesh = CreateShapeMesh(_config.Shape);
            _cubeMaterial = GetShapeMaterial();

            int maxCount = Mathf.Max(_config.CubeCount, 1023);
            _matrices = new Matrix4x4[maxCount];

            InitBeltVisuals();
        }

        private void InitBeltVisuals()
        {
            _beltMaterial = GetBeltMaterial();
            _beltMpb = new MaterialPropertyBlock();
            _beltSegmentCount = _config.WaypointCount;
            _beltMatrix = Matrix4x4.identity;

            BuildBeltStripMesh();
        }

        private void BuildBeltStripMesh()
        {
            int count = _beltSegmentCount;
            float beltY = 0f;
            float beltThickness = 0.05f;

            // Her waypoint için 4 vertex: üst-iç, üst-dış, alt-iç, alt-dış
            int vertPerSlice = 4;
            var vertices = new Vector3[count * vertPerSlice];
            var normals = new Vector3[count * vertPerSlice];
            _beltVertexColors = new Color[count * vertPerSlice];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                Vector3 center = _track.GetPositionAtT(t);
                Vector3 normal = _track.GetNormalAtT(t);
                float hw = _config.BeltWidth * 0.5f;

                Vector3 innerTop = center - normal * hw + Vector3.up * beltY;
                Vector3 outerTop = center + normal * hw + Vector3.up * beltY;
                Vector3 innerBot = innerTop + Vector3.down * beltThickness;
                Vector3 outerBot = outerTop + Vector3.down * beltThickness;

                int vi = i * vertPerSlice;
                vertices[vi + 0] = innerTop;
                vertices[vi + 1] = outerTop;
                vertices[vi + 2] = innerBot;
                vertices[vi + 3] = outerBot;

                normals[vi + 0] = Vector3.up;
                normals[vi + 1] = Vector3.up;
                normals[vi + 2] = Vector3.down;
                normals[vi + 3] = Vector3.down;

                Color c = new Color(0.25f, 0.25f, 0.25f, 1f);
                _beltVertexColors[vi + 0] = c;
                _beltVertexColors[vi + 1] = c;
                _beltVertexColors[vi + 2] = c;
                _beltVertexColors[vi + 3] = c;
            }

            // Üst yüz + alt yüz + iç yan + dış yan
            var triangles = new List<int>();

            for (int i = 0; i < count; i++)
            {
                int cur = i * vertPerSlice;
                int nxt = ((i + 1) % count) * vertPerSlice;

                // Üst yüz: innerTop(0), outerTop(1)
                triangles.Add(cur + 0); triangles.Add(nxt + 0); triangles.Add(cur + 1);
                triangles.Add(cur + 1); triangles.Add(nxt + 0); triangles.Add(nxt + 1);

                // Alt yüz: innerBot(2), outerBot(3) — ters yön
                triangles.Add(cur + 2); triangles.Add(cur + 3); triangles.Add(nxt + 2);
                triangles.Add(nxt + 2); triangles.Add(cur + 3); triangles.Add(nxt + 3);

                // İç yan: innerTop(0), innerBot(2)
                triangles.Add(cur + 0); triangles.Add(cur + 2); triangles.Add(nxt + 0);
                triangles.Add(nxt + 0); triangles.Add(cur + 2); triangles.Add(nxt + 2);

                // Dış yan: outerTop(1), outerBot(3)
                triangles.Add(cur + 1); triangles.Add(nxt + 1); triangles.Add(cur + 3);
                triangles.Add(cur + 3); triangles.Add(nxt + 1); triangles.Add(nxt + 3);
            }

            _beltMesh = new Mesh();
            _beltMesh.vertices = vertices;
            _beltMesh.normals = normals;
            _beltMesh.colors = _beltVertexColors;
            _beltMesh.triangles = triangles.ToArray();
            _beltMesh.RecalculateBounds();
        }

        public void Render(List<ConveyorCube> cubes)
        {
            RenderBelt();
            RenderCubes(cubes);
        }

        private void RenderBelt()
        {
            if (_beltMaterial == null || _beltMesh == null) return;

            _beltScrollOffset += _config.ConveyorSpeed * Time.deltaTime;

            int vertPerSlice = 4;
            for (int i = 0; i < _beltSegmentCount; i++)
            {
                float t = (float)i / _beltSegmentCount;
                float wave = Mathf.Sin((t + _beltScrollOffset * 0.1f) * Mathf.PI * 8f) * 0.08f;
                float gray = 0.25f + wave;
                Color c = new Color(gray, gray, gray, 1f);

                int vi = i * vertPerSlice;
                _beltVertexColors[vi + 0] = c;
                _beltVertexColors[vi + 1] = c;
                _beltVertexColors[vi + 2] = c;
                _beltVertexColors[vi + 3] = c;
            }

            _beltMesh.colors = _beltVertexColors;
            Graphics.DrawMesh(_beltMesh, _beltMatrix, _beltMaterial, 0, null, 0, _beltMpb);
        }

        private void RenderCubes(List<ConveyorCube> cubes)
        {
            if (cubes == null || cubes.Count == 0 || _cubeMaterial == null || _cubeMesh == null) return;

            int count = Mathf.Min(cubes.Count, 1023);

            for (int i = 0; i < count; i++)
            {
                _matrices[i] = Matrix4x4.TRS(
                    cubes[i].Position,
                    cubes[i].Rotation,
                    cubes[i].Size);
            }

            Graphics.DrawMeshInstanced(_cubeMesh, 0, _cubeMaterial, _matrices, count);
        }

        private Mesh CreateShapeMesh(Config.ConveyorShape shape)
        {
            switch (shape)
            {
                case Config.ConveyorShape.Sphere:
                    return CreatePrimitiveMesh(PrimitiveType.Sphere);
                case Config.ConveyorShape.Diamond:
                    return CreateDiamondMesh();
                case Config.ConveyorShape.Cube:
                default:
                    return CreatePrimitiveMesh(PrimitiveType.Cube);
            }
        }

        private Mesh CreatePrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            // Collider'ı hemen kaldır — Android'de physics modülü yüklenmemişse sorun çıkarır
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(go);
            return mesh;
        }

        private Mesh CreateDiamondMesh()
        {
            // Brilliant-cut diamond: table (üst düz yüzey), crown (taç), girdle (kuşak), pavilion (alt koni)
            var mesh = new Mesh();

            int sides = 8; // sekizgen simetri — gerçek pırlanta kesim
            float tableRadius = 0.25f;
            float tableY = 0.45f;
            float crownRadius = 0.45f;
            float crownY = 0.3f;
            float girdleRadius = 0.5f;
            float girdleY = 0.0f;
            float pavilionTip = -0.5f;

            // Flat-shaded: her üçgen kendi vertex'lerine sahip
            var verts = new List<Vector3>();
            var tris = new List<int>();

            void AddTri(Vector3 a, Vector3 b, Vector3 c)
            {
                int idx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            }

            // Precompute ring points
            var tableRing = new Vector3[sides];
            var crownRing = new Vector3[sides];
            var girdleUpperRing = new Vector3[sides];
            var girdleLowerRing = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = Mathf.PI * 2f * i / sides;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                tableRing[i] = new Vector3(cos * tableRadius, tableY, sin * tableRadius);
                crownRing[i] = new Vector3(cos * crownRadius, crownY, sin * crownRadius);
                // Girdle: star facet efekti — çift indeksler biraz yukarı, tek indeksler biraz aşağı
                float girdleOffset = (i % 2 == 0) ? 0.03f : -0.03f;
                girdleUpperRing[i] = new Vector3(cos * girdleRadius, girdleY + girdleOffset, sin * girdleRadius);
                girdleLowerRing[i] = new Vector3(cos * girdleRadius, girdleY + girdleOffset - 0.04f, sin * girdleRadius);
            }

            Vector3 tableCenter = new Vector3(0, tableY, 0);
            Vector3 pavilionBottom = new Vector3(0, pavilionTip, 0);

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                // --- TABLE: üst düz yüzey (sekizgen) ---
                AddTri(tableCenter, tableRing[i], tableRing[next]);

                // --- CROWN: table → crown star facets ---
                // Üst üçgen: table edge → crown vertex
                AddTri(tableRing[i], crownRing[i], tableRing[next]);
                AddTri(tableRing[next], crownRing[i], crownRing[next]);

                // --- CROWN → GIRDLE: bezel facets ---
                AddTri(crownRing[i], girdleUpperRing[i], crownRing[next]);
                AddTri(crownRing[next], girdleUpperRing[i], girdleUpperRing[next]);

                // --- GIRDLE: ince kuşak bandı ---
                AddTri(girdleUpperRing[i], girdleLowerRing[i], girdleUpperRing[next]);
                AddTri(girdleUpperRing[next], girdleLowerRing[i], girdleLowerRing[next]);

                // --- PAVILION: girdle → alt sivri uç ---
                AddTri(girdleLowerRing[i], pavilionBottom, girdleLowerRing[next]);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material GetShapeMaterial()
        {
            if (_config.ShapeMaterial == null)
            {
                Debug.LogError("[ConveyorRenderer] ShapeMaterial is not assigned in ConveyorConfig!");
                return null;
            }

            // Config'deki material'i doğrudan kullan — kopyalama, shader referansı korunsun
            _config.ShapeMaterial.enableInstancing = true;
            return _config.ShapeMaterial;
        }

        private Material GetBeltMaterial()
        {
            if (_config.BeltMaterial == null)
            {
                Debug.LogError("[ConveyorRenderer] BeltMaterial is not assigned in ConveyorConfig!");
                return null;
            }

            return _config.BeltMaterial;
        }
    }
}
