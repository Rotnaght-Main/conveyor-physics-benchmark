using UnityEngine;
using UnityEngine.Profiling;
using Zenject;
using LoopSortTest.Core.Services;
using Cysharp.Threading.Tasks;

namespace LoopSortTest.UI
{
    public class PerformanceStatsUI : MonoBehaviour
    {
        [Inject] private ConveyorSystem _system;

        // FPS
        private float _deltaTime;
        private float _fps;
        private float _fpsMin = float.MaxValue;
        private float _fpsMax;
        private float _fpsResetTimer;

        // Frame timing
        private float _frameMs;

        // Memory
        private long _totalAllocatedMB;
        private long _totalReservedMB;
        private long _monoUsedMB;
        private long _monoHeapMB;
        private float _memoryUpdateTimer;

        // Styles
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _fpsStyle;
        private bool _stylesInitialized;

        // Layout — 1080x1920 dikey ekran baz alınarak
        private const float PanelWidthRatio = 0.42f;
        private const float PanelHeightRatio = 0.22f;
        private const float MarginRatio = 0.012f;

        private void Start()
        {
            TargetFrameRateChange().Forget();
        }

        private async UniTaskVoid TargetFrameRateChange()
        {
            await UniTask.Delay(3000);

            Application.targetFrameRate = 120;
        }

        private void Update()
        {
            // FPS — smoothed
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _fps = 1f / _deltaTime;
            _frameMs = _deltaTime * 1000f;

            // Min/Max — her 3 saniyede sıfırla
            if (_fps < _fpsMin) _fpsMin = _fps;
            if (_fps > _fpsMax) _fpsMax = _fps;
            _fpsResetTimer += Time.unscaledDeltaTime;
            if (_fpsResetTimer > 3f)
            {
                _fpsMin = _fps;
                _fpsMax = _fps;
                _fpsResetTimer = 0f;
            }

            // Memory — her 0.5 saniyede güncelle (GC pressure azalt)
            _memoryUpdateTimer += Time.unscaledDeltaTime;
            if (_memoryUpdateTimer > 0.5f)
            {
                _memoryUpdateTimer = 0f;
                _totalAllocatedMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                _totalReservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                _monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
                _monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
            }
        }

        private void OnGUI()
        {
            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float margin = sh * MarginRatio;
            float panelW = sw * PanelWidthRatio;
            float panelH = sh * PanelHeightRatio;

            // Sağ alt köşe
            Rect panelRect = new(sw - panelW - margin, sh - panelH - margin, panelW, panelH);
            GUI.Box(panelRect, "", _boxStyle);

            float pad = panelW * 0.05f;
            Rect area = new(panelRect.x + pad, panelRect.y + pad, panelW - pad * 2, panelH - pad * 2);
            GUILayout.BeginArea(area);

            // FPS — büyük font
            Color fpsColor = _fps >= 55 ? Color.green : _fps >= 30 ? Color.yellow : Color.red;
            _fpsStyle.normal.textColor = fpsColor;
            GUILayout.Label($"{_fps:0} FPS", _fpsStyle);

            // Frame time
            DrawStat("Frame", $"{_frameMs:0.0} ms");
            DrawStat("Min/Max", $"{_fpsMin:0} / {_fpsMax:0}");

            GUILayout.Space(2);

            // Memory
            DrawStat("Allocated", $"{_totalAllocatedMB} MB / {_totalReservedMB} MB");
            DrawStat("Mono", $"{_monoUsedMB} MB / {_monoHeapMB} MB");

            GUILayout.Space(2);

            // Render info
            DrawStat("Objects", $"{_system.Cubes.Count}");
#if UNITY_EDITOR
            DrawStat("Batches", $"{UnityEditor.UnityStats.batches}");
            DrawStat("Tris", $"{UnityEditor.UnityStats.triangles:N0}");
            DrawStat("SetPass", $"{UnityEditor.UnityStats.setPassCalls}");
#endif

            GUILayout.EndArea();
        }

        private void DrawStat(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(Screen.width * PanelWidthRatio * 0.35f));
            GUILayout.Label(value, _valueStyle);
            GUILayout.EndHorizontal();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            // Font boyutu ekran yüksekliğine orantılı
            int baseFontSize = Mathf.RoundToInt(Screen.height / 70f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.75f));

            _fpsStyle = new GUIStyle(GUI.skin.label);
            _fpsStyle.fontSize = baseFontSize + 6;
            _fpsStyle.fontStyle = FontStyle.Bold;
            _fpsStyle.alignment = TextAnchor.MiddleLeft;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = baseFontSize;
            _labelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.fontSize = baseFontSize;
            _valueStyle.normal.textColor = Color.white;
            _valueStyle.fontStyle = FontStyle.Bold;
        }

        private Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
