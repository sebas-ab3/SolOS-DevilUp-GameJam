// Assets/Scripts/Debug/DebugHotkeys.cs
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using Game.Core;

namespace Game.Debugging
{
    /// <summary>Global dev hotkeys. Not included in release builds.</summary>
    public class DebugHotkeys : MonoBehaviour
    {
        [SerializeField] private KeyCode overlayToggle = KeyCode.F10;
        [SerializeField] private KeyCode timeScaleCycle = KeyCode.F11;
        [SerializeField] private KeyCode screenshotKey  = KeyCode.F12;

        private readonly float[] _timeScales = { 1f, 0.5f, 0.25f, 1.5f };
        private int _tsIndex = 0;

        private void Update()
        {
            if (!Application.isPlaying || GameDirector.Instance == null) return;

            // State jumps
            if (Input.GetKeyDown(KeyCode.F1)) GameDirector.Instance.SetState(GameState.Freeplay);
            if (Input.GetKeyDown(KeyCode.F2)) GameDirector.Instance.SetState(GameState.Match);
            if (Input.GetKeyDown(KeyCode.F3)) GameDirector.Instance.SetState(GameState.Intermission);
            if (Input.GetKeyDown(KeyCode.F4)) GameDirector.Instance.SetState(GameState.Tutorial);
            if (Input.GetKeyDown(KeyCode.F5)) GameDirector.Instance.SetState(GameState.CampaignComplete);

            // Overlay toggle
            if (Input.GetKeyDown(overlayToggle))
            {
                var overlay = FindObjectOfType<DevOverlay>(includeInactive: true);
                if (overlay != null) overlay.Toggle();
            }

            // TimeScale cycle
            if (Input.GetKeyDown(timeScaleCycle))
            {
                _tsIndex = (_tsIndex + 1) % _timeScales.Length;
                Time.timeScale = _timeScales[_tsIndex];
                Debug.Log($"[DEBUG] timeScale = {Time.timeScale}");
            }

            // Screenshot
            if (Input.GetKeyDown(screenshotKey))
            {
                var path = System.IO.Path.Combine(Application.dataPath, $"../Screenshots");
                System.IO.Directory.CreateDirectory(path);
                var filename = System.IO.Path.Combine(path, $"shot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(filename);
                Debug.Log($"[DEBUG] Screenshot → {filename}");
            }
        }
    }
}
#endif
