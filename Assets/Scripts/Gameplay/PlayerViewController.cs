using UnityEngine;
using Game.Core; // GameDirector + GameState

namespace Game.Gameplay
{
    /// <summary>
    /// During MATCH: Tab toggles between Dudo UI and a stationary 3D "look" view.
    /// In look view: Left/Right/Center via Arrow keys. You can only return to UI from Center.
    /// </summary>
    public class PlayerViewController : MonoBehaviour
    {
        public enum FocusMode { UI, Look }
        public enum LookZone  { Left, Center, Right }

        [Header("Refs")]
        [SerializeField] private Transform  pivotToRotate;   // usually your Camera or a parent pivot
        [SerializeField] private CanvasGroup dudoUICanvasGroup; // CanvasGroup on dudoUI to hide/show without disabling
        [SerializeField] private GameObject optionalReticle; // small dot shown in Look mode (optional)
        [SerializeField] private DudoUIController dudoController; // Reference to pause/resume game

        [Header("Yaw Angles (deg)")]
        [SerializeField] private float leftYaw   = -60f;
        [SerializeField] private float centerYaw =   0f;
        [SerializeField] private float rightYaw  =  60f;

        [Header("Smoothing")]
        [SerializeField] private float rotateSpeedDegPerSec = 240f; // max rotation speed

        [Header("Input")]
        [SerializeField] private KeyCode keyToggle   = KeyCode.Tab;       // UI <-> Look
        [SerializeField] private KeyCode keyLeft     = KeyCode.LeftArrow; // look LEFT
        [SerializeField] private KeyCode keyRight    = KeyCode.RightArrow;// look RIGHT
        [SerializeField] private KeyCode keyCenter   = KeyCode.DownArrow; // look CENTER

        [Header("State Gating")]
        [Tooltip("Allow the controller to operate in Match.")]
        [SerializeField] private bool allowInMatch    = true;
        [Tooltip("Allow in Freeplay (off by default for now).")]
        [SerializeField] private bool allowInFreeplay = false;
        [Tooltip("Allow in Tutorial (off by default for now).")]
        [SerializeField] private bool allowInTutorial = false;

        // Runtime
        public FocusMode Mode { get; private set; } = FocusMode.UI;
        public LookZone  Zone { get; private set; } = LookZone.Center;

        private GameDirector _dir;
        private float _targetYaw;
        private bool _subscribed;

        private void Reset()
        {
            // If you drop this on the Camera, assume pivot = self
            if (!pivotToRotate) pivotToRotate = transform;
        }

        private void OnEnable()
        {
            if (!pivotToRotate) pivotToRotate = transform;

            _dir = GameDirector.Instance;
            if (_dir != null && !_subscribed)
            {
                _dir.OnStateChanged += HandleStateChanged;
                _subscribed = true;
            }

            // Initial sync to current state
            HandleStateChanged(_dir != null ? _dir.State : GameState.Boot,
                               _dir != null ? _dir.State : GameState.Boot);

            // Start centered + UI mode
            SetLookZone(LookZone.Center, snap: true);
            ApplyMode(FocusMode.UI, reason: "OnEnable");
        }

        private void OnDisable()
        {
            if (_subscribed && _dir != null)
            {
                _dir.OnStateChanged -= HandleStateChanged;
            }
            _subscribed = false;
        }

        private void Update()
        {
            // Only active in allowed states
            if (!IsAllowedInCurrentState()) return;

            // Toggle focus
            if (Input.GetKeyDown(keyToggle))
            {
                if (Mode == FocusMode.UI)
                {
                    // Enter LOOK from UI
                    ApplyMode(FocusMode.Look, "Toggle UI→Look");
                }
                else // Look -> UI only if centered
                {
                    if (Zone == LookZone.Center)
                        ApplyMode(FocusMode.UI, "Toggle Look→UI (center)");
                    else
                        DebugLog("[PlayerView] Blocked: return to UI only when centered.");
                }
            }

            // In LOOK mode, handle arrows for Left/Right/Center
            if (Mode == FocusMode.Look)
            {
                if (Input.GetKeyDown(keyLeft))   SetLookZone(LookZone.Left);
                if (Input.GetKeyDown(keyRight))  SetLookZone(LookZone.Right);
                if (Input.GetKeyDown(keyCenter)) SetLookZone(LookZone.Center);
            }

            // Smooth rotate toward target yaw
            SmoothYaw();
        }

        // --------- Core behavior ---------

        private void ApplyMode(FocusMode newMode, string reason)
        {
            Mode = newMode;

            bool uiOn   = (Mode == FocusMode.UI);
            bool lookOn = (Mode == FocusMode.Look);

            // Hide/show UI using CanvasGroup (keeps GameObject active so scripts keep running)
            if (dudoUICanvasGroup != null)
            {
                dudoUICanvasGroup.alpha = uiOn ? 1f : 0f;              // ← Makes UI invisible/visible
                dudoUICanvasGroup.interactable = uiOn;                 // ← Disables interaction
                dudoUICanvasGroup.blocksRaycasts = uiOn;              // ← Allows clicks to pass through
            }

            // Pause/Resume the Dudo game
            if (dudoController != null)
            {
                if (lookOn)
                    dudoController.PauseGame();
                else
                    dudoController.ResumeGame();
            }

            // Optional reticle for look mode
            if (optionalReticle) optionalReticle.SetActive(lookOn);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLog($"[PlayerView] Mode → {Mode} ({reason})");
#endif
        }

        private void SetLookZone(LookZone z, bool snap = false)
        {
            Zone = z;
            _targetYaw = GetYawFor(z);

            if (snap)
            {
                var e = pivotToRotate.eulerAngles;
                e.y = _targetYaw;
                e.x = 0f; e.z = 0f;
                pivotToRotate.eulerAngles = e;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLog($"[PlayerView] Zone → {z} (targetYaw={_targetYaw:0.0})");
#endif
        }

        private void SmoothYaw()
        {
            if (!pivotToRotate) return;

            float currentYaw = pivotToRotate.eulerAngles.y;
            float nextYaw = Mathf.MoveTowardsAngle(currentYaw, _targetYaw, rotateSpeedDegPerSec * Time.deltaTime);

            var e = pivotToRotate.eulerAngles;
            e.y = nextYaw;
            e.x = 0f; e.z = 0f;
            pivotToRotate.eulerAngles = e;
        }

        private float GetYawFor(LookZone z)
        {
            switch (z)
            {
                case LookZone.Left:   return leftYaw;
                case LookZone.Right:  return rightYaw;
                case LookZone.Center:
                default:              return centerYaw;
            }
        }

        // --------- State gating ---------

        private bool IsAllowedInCurrentState()
        {
            if (_dir == null) _dir = GameDirector.Instance;
            var s = _dir != null ? _dir.State : GameState.Boot;

            if (s == GameState.Match)     return allowInMatch;
            if (s == GameState.Freeplay)  return allowInFreeplay;
            if (s == GameState.Tutorial)  return allowInTutorial;

            return false; // Intermission/Boot/CampaignComplete: no operation
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugLog($"[PlayerView] State {from} → {to}");
#endif
            // If entering a disallowed state, force UI mode and let HUDStateBinder handle panels.
            if (!IsAllowedInCurrentState())
            {
                // Ensure DudoUI (if present) is left enabled for Intermission/Tutorial flows
                ApplyMode(FocusMode.UI, "StateGate");
                SetLookZone(LookZone.Center, snap: true);
            }
        }

        // --------- Helpers ---------

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DebugLog(string msg) => Debug.Log(msg);
    }
}
