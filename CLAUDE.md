# LOOPSORT — Conveyor Physics Algorithm Test Bench

> **Amaç:** Oval bir conveyor belt üzerinde hareket eden küplerin DrawMesh ile render edilmesi ve farklı fizik/çarpışma algılama algoritmalarının karşılaştırmalı olarak test edilmesi.

---

## Proje Genel Bakış

Görsel referans olarak LoopSort tarzı bir layout kullanılıyor: merkezi oval bir conveyor track üzerinde renkli küpler sırayla ilerliyor. Küpler birbirine çarptığında itmeli, sıkıştığında yığılmamalı. Tüm render işlemi **Graphics.DrawMeshInstanced** ile yapılır — sahneye tek bir GameObject bile eklenmez.

---

## Mimari Kural Seti

> Bu kural seti **mutlak** — istisna kabul edilmez.

| Kural | Açıklama |
|-------|----------|
| **DI** | Yalnızca Zenject. `static`, `singleton`, `FindObjectOfType` yasak. |
| **Async** | Yalnızca UniTask. Coroutine yasak (`UnityWebRequest` sarmalayıcısı hariç). |
| **Update** | Fizik tick'i `ITickable` üzerinden. MonoBehaviour `Update()` sadece render çağrısı için. |
| **Interface** | Her algoritma `IPhysicsAlgorithm` implemente eder. Sınıf başına max 2 iş arayüzü. |
| **Dosya** | Her algoritma kendi dosyasında. `ConveyorSystemInstaller.cs` bağlar. |

---

## Dosya & Klasör Yapısı

```
Assets/
└── LoopSortTest/
    ├── Core/
    │   ├── Interfaces/
    │   │   └── IPhysicsAlgorithm.cs          ← Tüm algoritmalar bunu implemente eder
    │   ├── Models/
    │   │   ├── ConveyorCube.cs               ← Veri modeli (struct veya class)
    │   │   └── ConveyorTrack.cs              ← Path verisi (waypoints, length cache)
    │   └── Services/
    │       ├── ConveyorSystem.cs             ← Ana orchestrator (ITickable)
    │       ├── ConveyorRenderer.cs           ← DrawMeshInstanced wrapper
    │       └── AlgorithmSwitcher.cs          ← Runtime geçiş yöneticisi
    │
    ├── Algorithms/
    │   ├── AABBPhysics.cs                    ← Algoritma 1: AABB overlap
    │   ├── SpatialHashPhysics.cs             ← Algoritma 2: Spatial hashing grid
    │   ├── SATPhysics.cs                     ← Algoritma 3: Separating Axis Theorem
    │   ├── CircleApproxPhysics.cs            ← Algoritma 4: Circle/Sphere yaklaşımı
    │   └── VerletPhysics.cs                  ← Algoritma 5: Verlet integration
    │
    ├── Installers/
    │   └── ConveyorSystemInstaller.cs        ← Zenject binding
    │
    ├── UI/
    │   └── AlgorithmSwitcherUI.cs            ← Dropdown / tuş ile geçiş
    │
    └── Config/
        └── ConveyorConfig.cs                 ← ScriptableObject — tüm parametreler burada
```

---

## Arayüz Tanımları

### `IPhysicsAlgorithm.cs`

```csharp
using System.Collections.Generic;

namespace LoopSortTest.Core.Interfaces
{
    /// <summary>
    /// Tüm fizik algoritmalarının implemente etmesi gereken sözleşme.
    /// Her algoritma bağımsız dosyada bulunur, ConveyorSystemInstaller içinde bağlanır.
    /// </summary>
    public interface IPhysicsAlgorithm
    {
        string AlgorithmName { get; }

        /// <summary>
        /// Tek bir fizik tick'i. dt = deltaTime.
        /// Küplerin track üzerindeki t pozisyonlarını ve hızlarını günceller.
        /// </summary>
        void Tick(List<ConveyorCube> cubes, ConveyorTrack track, float dt);

        /// <summary>
        /// Algoritma değiştirilmeden önce çağrılır (internal state temizliği için).
        /// </summary>
        void Dispose();
    }
}
```

---

### `ConveyorCube.cs`

```csharp
using UnityEngine;

namespace LoopSortTest.Core.Models
{
    public class ConveyorCube
    {
        public int Id;
        public Color Color;

        /// <summary>Track üzerindeki normalize edilmiş pozisyon [0, 1).</summary>
        public float TrackT;

        /// <summary>Track boyunca normalize hız (birim/saniye).</summary>
        public float Velocity;

        /// <summary>Her frame DrawMesh için hesaplanan dünya pozisyonu.</summary>
        public Vector3 WorldPosition;

        /// <summary>Küpün görsel boyutu. Çarpışma box'ı bu değerden türetilir.</summary>
        public Vector3 Size;

        /// <summary>Verlet algoritması için önceki pozisyon (track-t uzayında).</summary>
        public float PrevTrackT;
    }
}
```

---

### `ConveyorTrack.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LoopSortTest.Core.Models
{
    /// <summary>
    /// Oval conveyor belt'in path verisi.
    /// Waypoint listesinden oluşturulur; dünya pozisyonu t=[0,1] ile örneklenir.
    /// </summary>
    public class ConveyorTrack
    {
        public List<Vector3> Waypoints { get; private set; }
        public float TotalLength { get; private set; }

        /// <summary>Kümülatif mesafe cache — hızlı t→pozisyon dönüşümü için.</summary>
        private float[] _cumulativeLengths;

        public ConveyorTrack(List<Vector3> waypoints)
        {
            Waypoints = waypoints;
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
            // Loop kapat
            total += Vector3.Distance(Waypoints[^1], Waypoints[0]);
            TotalLength = total;
        }

        /// <summary>
        /// t ∈ [0, 1) → Dünya pozisyonu.
        /// </summary>
        public Vector3 GetPositionAtT(float t)
        {
            t = ((t % 1f) + 1f) % 1f; // negatif t wrap
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

        /// <summary>t uzayındaki mesafeyi normalize uzaya çevirir (mesafe / TotalLength).</summary>
        public float WorldDistanceToT(float worldDistance) => worldDistance / TotalLength;
    }
}
```

---

## Algoritmalar

### Algoritma 1 — AABB (Axis-Aligned Bounding Box)

**Dosya:** `Algorithms/AABBPhysics.cs`

**Mantık:**
- Her küp için track üzerindeki konuma göre dünya-uzayında AABB box hesaplanır.
- Her çift için overlap kontrolü: `Δt * TotalLength < (sizeA + sizeB) / 2`
- Overlap varsa iki küp eşit miktarda itilir (momentum korunumu yok, saf pozisyon düzeltme).

**Avantaj:** Basit, hata ayıklaması kolay, deterministic.  
**Dezavantaj:** O(n²) broad phase, küp sayısı artınca yavaşlar.

```csharp
public void Tick(List<ConveyorCube> cubes, ConveyorTrack track, float dt)
{
    // 1. Conveyor hareketi uygula
    foreach (var cube in cubes)
        cube.TrackT += cube.Velocity * dt;

    // 2. AABB overlap çözümü
    for (int i = 0; i < cubes.Count; i++)
    {
        for (int j = i + 1; j < cubes.Count; j++)
        {
            float delta = cubes[j].TrackT - cubes[i].TrackT;
            // Loop wrap: delta -0.5..0.5 aralığına çek
            if (delta > 0.5f) delta -= 1f;
            if (delta < -0.5f) delta += 1f;

            float minSep = track.WorldDistanceToT(
                (cubes[i].Size.x + cubes[j].Size.x) * 0.5f);

            float overlap = minSep - Mathf.Abs(delta);
            if (overlap > 0f)
            {
                float push = overlap * 0.5f * Mathf.Sign(delta);
                cubes[i].TrackT -= push;
                cubes[j].TrackT += push;
            }
        }
    }

    // 3. Dünya pozisyonlarını güncelle
    foreach (var cube in cubes)
        cube.WorldPosition = track.GetPositionAtT(cube.TrackT);
}
```

---

### Algoritma 2 — Spatial Hashing

**Dosya:** `Algorithms/SpatialHashPhysics.cs`

**Mantık:**
- Track 1D doğrusal bir uzay olarak modellenir (0..TotalLength).
- Grid hücre boyutu = max küp genişliği.
- Her küp kendi hücresine ve komşu hücreye eklenir.
- Yalnızca aynı/bitişik hücre komşularına AABB testi uygulanır.

**Avantaj:** O(n) average case. Küp sayısı artsa bile hızlıdır.  
**Dezavantaj:** Grid boyutu yanlış seçilirse cluster'larda bozulur.

```csharp
// Sözde kod — tam implementasyon AlgorithmSwitcher'dan test edilecek
Dictionary<int, List<ConveyorCube>> _grid = new();
int CellIndex(float worldPos, float cellSize) => Mathf.FloorToInt(worldPos / cellSize);
```

---

### Algoritma 3 — SAT (Separating Axis Theorem)

**Dosya:** `Algorithms/SATPhysics.cs`

**Mantık:**
- Küpler track yönünde hafifçe dönebildiğinde (eğri segmentlerde) devreye girer.
- Her iki küp için track tangent yönü hesaplanır.
- SAT: eğer herhangi bir eksen üzerinde iki şeklin projeksiyon aralıkları ayrışıyorsa çarpışma yok.
- Convex hull yeterli (küpler için 4 eksen: 2 tangent + 2 normal).

**Avantaj:** Dönmüş küpler için doğru overlap tespit eder.  
**Dezavantaj:** AABB'ye göre ~3× daha hesap ağır. Düz segmentlerde gereksiz.

---

### Algoritma 4 — Circle Approximation

**Dosya:** `Algorithms/CircleApproxPhysics.cs`

**Mantık:**
- Her küp en büyük boyutunu kapsayan bir yarıçapla temsil edilir: `r = max(size.x, size.z) * 0.5f`.
- Çarpışma kontrolü: `worldDist < rA + rB`.
- Response: itme vektörü merkez-merkez hattı boyunca.

**Avantaj:** En hızlı algoritma. Yüzlerce küpte bile smooth.  
**Dezavantaj:** Küp köşelerinde false negative (ince dikdörtgenlerde yanlış).

---

### Algoritma 5 — Verlet Integration

**Dosya:** `Algorithms/VerletPhysics.cs`

**Mantık:**
- Pozisyon güncelleme: `newT = 2*currentT - prevT + acceleration * dt²`
- Constraint solve: küpler arası mesafe kısıtı iteratif olarak çözülür (Jakobsen stili).
- `iterationCount` (önerilen: 3–5) kadar pass uygulanır.

**Avantaj:** Enerji korunumuna yakın davranış. Zincir reaksiyon kümelenmesi doğal görünür.  
**Dezavantaj:** Iterasyon sayısı artınca maliyet yükselir. Parametre hassastır.

```csharp
// Her tick başında
cube.PrevTrackT = cube.TrackT;
cube.TrackT += (cube.TrackT - cube.PrevTrackT) + conveyorSpeed * dt;

// Constraint pass (iterationCount kez tekrarla)
for (int iter = 0; iter < iterationCount; iter++)
{
    for (int i = 0; i < cubes.Count - 1; i++)
    {
        var a = cubes[i]; var b = cubes[i + 1];
        float delta = b.TrackT - a.TrackT;
        float minDist = track.WorldDistanceToT((a.Size.x + b.Size.x) * 0.5f);
        if (Mathf.Abs(delta) < minDist)
        {
            float correction = (minDist - Mathf.Abs(delta)) * 0.5f * Mathf.Sign(delta);
            a.TrackT -= correction;
            b.TrackT += correction;
        }
    }
}
```

---

## DrawMesh Render Sistemi

**Dosya:** `Core/Services/ConveyorRenderer.cs`

```csharp
/// Kurallar:
/// - Graphics.DrawMeshInstanced kullan (max 1023 instance/batch).
/// - Matrix4x4[] her frame yeniden hesaplanır — allocasyon yok (cached array).
/// - MaterialPropertyBlock ile her küpün rengi GPU'ya aktarılır.
/// - Renderer hiçbir ConveyorCube verisini değiştirmez (salt okunur).

public class ConveyorRenderer : IInitializable
{
    private Matrix4x4[] _matrices;
    private MaterialPropertyBlock _mpb;
    private Vector4[] _colors;
    private Mesh _cubeMesh;
    private Material _material;

    public void Render(List<ConveyorCube> cubes)
    {
        if (cubes.Count == 0) return;

        for (int i = 0; i < cubes.Count; i++)
        {
            _matrices[i] = Matrix4x4.TRS(
                cubes[i].WorldPosition,
                Quaternion.identity,
                cubes[i].Size);
            _colors[i] = (Vector4)cubes[i].Color;
        }

        _mpb.SetVectorArray("_BaseColor", _colors);
        Graphics.DrawMeshInstanced(_cubeMesh, 0, _material, _matrices, cubes.Count, _mpb);
    }
}
```

---

## AlgorithmSwitcher

**Dosya:** `Core/Services/AlgorithmSwitcher.cs`

```csharp
/// Çalışma anında algoritma değiştirme.
/// UI dropdown veya klavye kısayolu (1-5) ile tetiklenir.

public class AlgorithmSwitcher : IAlgorithmSwitcher
{
    private readonly List<IPhysicsAlgorithm> _algorithms;
    private int _currentIndex;

    public IPhysicsAlgorithm Current => _algorithms[_currentIndex];
    public string CurrentName => Current.AlgorithmName;

    public void Next()
    {
        _algorithms[_currentIndex].Dispose();
        _currentIndex = (_currentIndex + 1) % _algorithms.Count;
    }

    public void SetByIndex(int index)
    {
        if (index < 0 || index >= _algorithms.Count) return;
        _algorithms[_currentIndex].Dispose();
        _currentIndex = index;
    }
}
```

---

## Zenject Installer

**Dosya:** `Installers/ConveyorSystemInstaller.cs`

```csharp
public class ConveyorSystemInstaller : MonoInstaller
{
    [SerializeField] private ConveyorConfig _config;

    public override void InstallBindings()
    {
        // Config
        Container.BindInstance(_config).AsSingle();

        // Track (runtime oluşturulur)
        Container.Bind<ConveyorTrack>()
            .FromMethod(ctx => TrackFactory.CreateOval(_config))
            .AsSingle();

        // Algoritmalar — hepsi inject edilir, SwitchAlgorithm listesine bağlanır
        Container.Bind<IPhysicsAlgorithm>().To<AABBPhysics>().AsTransient().WithId("AABB");
        Container.Bind<IPhysicsAlgorithm>().To<SpatialHashPhysics>().AsTransient().WithId("SpatialHash");
        Container.Bind<IPhysicsAlgorithm>().To<SATPhysics>().AsTransient().WithId("SAT");
        Container.Bind<IPhysicsAlgorithm>().To<CircleApproxPhysics>().AsTransient().WithId("Circle");
        Container.Bind<IPhysicsAlgorithm>().To<VerletPhysics>().AsTransient().WithId("Verlet");

        Container.Bind<AlgorithmSwitcher>().AsSingle();
        Container.Bind<ConveyorRenderer>().AsSingle();

        // Ana sistem
        Container.BindInterfacesAndSelfTo<ConveyorSystem>().AsSingle();
    }
}
```

---

## ConveyorConfig (ScriptableObject)

**Dosya:** `Config/ConveyorConfig.cs`

```csharp
[CreateAssetMenu(menuName = "LoopSort/ConveyorConfig")]
public class ConveyorConfig : ScriptableObject
{
    [Header("Track")]
    public float OvalWidth = 6f;
    public float OvalHeight = 4f;
    public int WaypointCount = 64;

    [Header("Cubes")]
    public int CubeCount = 20;
    public Vector3 CubeSize = new(0.35f, 0.45f, 0.35f);
    public float ConveyorSpeed = 0.08f;   // track-t birim/saniye

    [Header("Physics — Verlet")]
    public int VerletIterations = 4;

    [Header("Physics — Spatial Hash")]
    public float HashCellSize = 0.5f;

    [Header("Debug")]
    public bool DrawGizmos = true;
}
```

---

## Uygulama Sırası

```
Adım 1  → IPhysicsAlgorithm.cs arayüzünü oluştur
Adım 2  → ConveyorCube.cs + ConveyorTrack.cs veri modellerini yaz
Adım 3  → AABBPhysics.cs ile algoritma çerçevesini doğrula
Adım 4  → ConveyorRenderer.cs — DrawMeshInstanced çalışıyor mu kontrol et
Adım 5  → ConveyorSystem.cs ITickable bağla, tek algoritmayı Play Mode'da test et
Adım 6  → AlgorithmSwitcher.cs + AlgorithmSwitcherUI.cs ekle
Adım 7  → Kalan 4 algoritmayı sırayla ekle ve geçişi test et
Adım 8  → ConveyorSystemInstaller.cs tüm bağlamaları yap
Adım 9  → ConveyorConfig parametrelerini Inspector'dan ayarla
```

---

## Beklenen Çıktı

- Oval conveyor track üzerinde renkli küpler sürekli döner.
- Küpler birbirine değdiğinde birbirini iter; yığılma olmaz.
- **1–5 tuşları** veya UI dropdown ile algoritma anında değişir.
- Her algoritmanın davranışı (titreme, gecikme, kayma) Inspector'daki `DrawGizmos` ile görselleştirilebilir.
- Hiçbir Coroutine, singleton veya `FindObjectOfType` kullanılmaz.

---

*Bu belge Claude Code'a `LOOPSORT_PHYSICS_TEST.md` adıyla projenin kök dizininden beslenir.*
