using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
using LoopSortTest.Core.Interfaces;
using LoopSortTest.Core.Services;

namespace LoopSortTest.UI
{
    public class AlgorithmSwitcherUI : MonoBehaviour
    {
        [Inject] private IAlgorithmSwitcher _switcher;
        [Inject] private ConveyorSystem _system;
        [Inject] private ConveyorRenderer _renderer;

        private int _selectedIndex;
        private string[] _names;
        private GUIStyle _boxStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _selectedButtonStyle;

        private void Start()
        {
            _names = _switcher.AlgorithmNames;
        }

        private void Update()
        {
            // Render cubes via DrawMeshInstanced
            _renderer.Render(_system.Cubes);

            // Keyboard shortcuts 1-5
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Key[] digitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < digitKeys.Length; i++)
                {
                    if (keyboard[digitKeys[i]].wasPressedThisFrame)
                    {
                        _switcher.SetByIndex(i);
                        _selectedIndex = i;
                    }
                }

                if (keyboard[Key.Tab].wasPressedThisFrame)
                {
                    _switcher.Next();
                    _selectedIndex = _switcher.CurrentIndex;
                }
            }
        }

        private void OnGUI()
        {
            if (_names == null) return;

            InitStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float margin = sh * 0.012f;
            float panelWidth = sw * 0.55f;
            float buttonHeight = sh * 0.035f;
            float panelHeight = buttonHeight + _names.Length * (buttonHeight + margin * 0.4f) + buttonHeight + margin * 2;

            Rect panelRect = new(margin, margin, panelWidth, panelHeight);
            GUI.Box(panelRect, "", _boxStyle);

            float pad = panelWidth * 0.04f;
            Rect area = new(panelRect.x + pad, panelRect.y + pad, panelWidth - pad * 2, panelHeight - pad * 2);
            GUILayout.BeginArea(area);

            GUILayout.Label("Algorithm", _labelStyle);
            GUILayout.Space(margin * 0.3f);

            _selectedIndex = _switcher.CurrentIndex;

            for (int i = 0; i < _names.Length; i++)
            {
                var style = (i == _selectedIndex) ? _selectedButtonStyle : _buttonStyle;
                if (GUILayout.Button($"[{i + 1}] {_names[i]}", style, GUILayout.Height(buttonHeight)))
                {
                    _switcher.SetByIndex(i);
                    _selectedIndex = i;
                }
            }

            GUILayout.Space(margin * 0.3f);
            GUILayout.Label($"Cubes: {_system.Cubes.Count}", _labelStyle);

            GUILayout.EndArea();
        }

        private void InitStyles()
        {
            if (_boxStyle != null) return;

            int baseFontSize = Mathf.RoundToInt(Screen.height / 70f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.fontStyle = FontStyle.Bold;
            _labelStyle.fontSize = baseFontSize + 2;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.alignment = TextAnchor.MiddleLeft;
            _buttonStyle.fontSize = baseFontSize;

            _selectedButtonStyle = new GUIStyle(_buttonStyle);
            _selectedButtonStyle.normal.textColor = Color.yellow;
            _selectedButtonStyle.fontStyle = FontStyle.Bold;
            _selectedButtonStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.4f, 0.6f, 0.8f));
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            var pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
