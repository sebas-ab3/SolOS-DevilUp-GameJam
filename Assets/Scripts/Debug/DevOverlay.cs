// Assets/Scripts/Debug/DevOverlay.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.Debugging
{
    /// <summary>Tiny corner overlay showing state/FPS/timeScale/input mode. Dev builds only.</summary>
    public class DevOverlay : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Text _label; // Use TMP if you prefer; swap type accordingly.

        private float _fpsTimer;
        private int _frames;
        private float _fps;

        private void Awake()
        {
            if (_canvas == null) _canvas = GetComponentInChildren<Canvas>(true);
            if (_label == null) _label = GetComponentInChildren<Text>(true);
            if (_canvas != null) _canvas.enabled = true;

            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
            {
                GameDirector.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            // FPS calc
            _frames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps = _frames / _fpsTimer;
                _frames = 0;
                _fpsTimer = 0;
            }

            RefreshText();
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            RefreshText();
        }

        private void RefreshText()
        {
            if (_label == null) return;

            string state = GameDirector.Instance != null ? GameDirector.Instance.State.ToString() : "N/A";
            string inputMode = InputLockedToUI ? "UI" : "Look"; // TODO: wire to your InputRouter when it exists.

            _label.text =
                $"STATE: {state}\n" +
                $"FPS: {Mathf.RoundToInt(_fps)}\n" +
                $"timeScale: {Time.timeScale:0.00}\n" +
                $"Input: {inputMode}";
        }

        // Placeholder until you add a real InputRouter.
        private bool InputLockedToUI => Cursor.visible;

        public void Toggle()
        {
            if (_canvas != null) _canvas.enabled = !_canvas.enabled;
        }
    }
}
#endif
