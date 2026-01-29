using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Game.Core;

namespace Game.Gameplay
{
    /// <summary>
    /// Manages the shadow timer mechanic during Match state.
    /// Randomly spawns shadow threats that player must check by looking left/right.
    /// </summary>
    public class ShadowTimerManager : MonoBehaviour
    {
        [Header("Timer Settings")]
        [SerializeField] private float checkIntervalSeconds = 30f; // How often to roll for spawn
        [SerializeField] [Range(0f, 100f)] private float spawnChancePercent = 15f; // Chance to spawn each interval
        [SerializeField] private float shadowDurationSeconds = 20f; // Time before shadow catches player
        
        [Header("Difficulty Scaling (Optional)")]
        [SerializeField] private bool increaseDifficultyOverTime = true;
        [SerializeField] private float chanceIncreasePerMinute = 5f; // +5% chance per minute
        [SerializeField] private float durationDecreasePerMinute = 2f; // -2 seconds per minute

        [Header("References")]
        [SerializeField] private PlayerViewController playerViewController;
        [SerializeField] private Image fadeOverlay; // Black image for screen fade
        [SerializeField] private TMP_Text deathMessageText; // "Life caught up to you"
        [SerializeField] private GameObject shadowObject; // 3D shadow in scene (optional for now)

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource warningAudioSource;
        [SerializeField] private AudioClip shadowApproachingClip;
        [SerializeField] private AudioClip shadowDeathClip;
        
        [Header("Visual Feedback")]
        [SerializeField] [Range(0f, 1f)] private float maxDarknessAlpha = 0.7f; // Max darkness
        [SerializeField] private AnimationCurve darknessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // ShadowTimerManager.cs (fields)
        [Header("Pause Behavior (Optional)")]
        [SerializeField] private DudoUIController dudoController;
        [SerializeField] private bool pauseWithDudoPause = false;
        
        
        // Runtime state
        private bool _isActive = false;
        private bool _shadowActive = false;
        private float _shadowTimer = 0f;
        private float _nextCheckTime = 0f;
        private float _matchStartTime = 0f;
        
        private float _currentSpawnChance;
        private float _currentShadowDuration;
        
        private float _shadowMaxDuration = 0f; // Store for percentage calculation
        
        public bool ShadowActive => _shadowActive;

// 0 = just spawned (far), 1 = time's up (near)
        public float ShadowProgress01
        {
            get
            {
                float max = (_shadowMaxDuration > 0f) ? _shadowMaxDuration : Mathf.Max(0.0001f, _currentShadowDuration);
                float remaining = Mathf.Clamp(_shadowTimer, 0f, max);
                return 1f - (remaining / max);
            }
        }

        public event System.Action OnShadowSpawned;
        public event System.Action OnShadowCleared;
        public event System.Action OnShadowCaught;


        private GameDirector _dir;
        private bool _subscribed = false;

        private void OnEnable()
        {
            StartCoroutine(AttachWhenReady());
        }

        private IEnumerator AttachWhenReady()
        {
            // Wait for GameDirector to exist
            while (GameDirector.Instance == null)
                yield return null;

            _dir = GameDirector.Instance;

            if (!_subscribed && _dir != null)
            {
                _dir.OnStateChanged += HandleStateChanged;
                _dir.OnMatchActive  += OnMatchActive;   // start timer system only after START pressed
                _subscribed = true;
                DebugLog("[ShadowTimer] Subscribed to GameDirector events");
            }

            // Initial sync: don't run timers unless gameplay is actually activated
            _isActive = false;
            StopShadow();  // ensure visuals/audio cleared
        }



        private void OnDisable()
        {
            if (_subscribed && _dir != null)
            {
                _dir.OnStateChanged -= HandleStateChanged;
                _dir.OnMatchActive -= OnMatchActive; // CHANGED: Unsubscribe from new event
            }
            _subscribed = false;
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            // Deactivate when leaving Match state
            if (to != GameState.Match)
            {
                _isActive = false;
                StopShadow();
                if (deathMessageText) deathMessageText.gameObject.SetActive(false);
            }
    
            // Don't auto-activate on entering Match - wait for OnMatchActive
        }
        
        // NEW: Only activate when match gameplay actually starts (Start button pressed)
        private void OnMatchActive()
        {
            if (_dir != null && _dir.State == GameState.Match)
            {
                DebugLog("[ShadowTimer] Match activated - starting timer system");
                ResetTimerSystem();
                _isActive = true;
            }
        }

        
        private void Update()
        {
            if (!_isActive) return;

            // Roll for spawn on the interval
            if (!_shadowActive && Time.time >= _nextCheckTime)
            {
                RollForShadowSpawn();
                _nextCheckTime = Time.time + checkIntervalSeconds;
            }

            // Active shadow countdown + visuals
            if (_shadowActive)
            {
                _shadowTimer -= Time.deltaTime;
                UpdateGradualDarkness(); // darken as time runs out

                if (_shadowTimer <= 0f)
                {
                    OnShadowCaughtPlayer();
                }
            }

            // Check if player looked left/right while in Look mode to clear the shadow
            if (_shadowActive && playerViewController != null)
            {
                if (playerViewController.Mode == PlayerViewController.FocusMode.Look)
                {
                    if (playerViewController.Zone == PlayerViewController.LookZone.Left ||
                        playerViewController.Zone == PlayerViewController.LookZone.Right)
                    {
                        OnPlayerCheckedShadow();
                    }
                }
            }
        }


        private void ResetTimerSystem()
        {
            _matchStartTime = Time.time;
            _nextCheckTime  = Time.time + checkIntervalSeconds;
            _shadowActive   = false;
            _shadowTimer    = 0f;
            _shadowMaxDuration = 0f;

            _currentSpawnChance   = spawnChancePercent;
            _currentShadowDuration = shadowDurationSeconds;

            if (fadeOverlay)
            {
                var col = fadeOverlay.color;
                col.a = 0f;
                fadeOverlay.color = col;
                fadeOverlay.gameObject.SetActive(false);
                fadeOverlay.raycastTarget = false;
            }
            if (deathMessageText) deathMessageText.gameObject.SetActive(false);
            if (shadowObject) shadowObject.SetActive(false);
            if (warningAudioSource)
            {
                warningAudioSource.Stop();
                warningAudioSource.volume = 1f;
            }

            DebugLog("[ShadowTimer] System reset for new match");
        }

        private void RollForShadowSpawn()
        {
            // Apply difficulty scaling if enabled
            if (increaseDifficultyOverTime)
            {
                float minutesElapsed = (Time.time - _matchStartTime) / 60f;
                _currentSpawnChance = spawnChancePercent + (chanceIncreasePerMinute * minutesElapsed);
                _currentShadowDuration = Mathf.Max(5f, shadowDurationSeconds - (durationDecreasePerMinute * minutesElapsed));
            }

            float roll = Random.Range(0f, 100f);
            
            DebugLog($"[ShadowTimer] Rolling for spawn: {roll:F1}% (need < {_currentSpawnChance:F1}%)");
            
            if (roll < _currentSpawnChance)
            {
                SpawnShadow();
            }
        }

        private void SpawnShadow()
        {
            _shadowActive = true;
            _currentSpawnChance = Mathf.Clamp(_currentSpawnChance, 0f, 100f);

            _shadowTimer       = _currentShadowDuration;
            _shadowMaxDuration = Mathf.Max(0.0001f, _currentShadowDuration); // avoid div by 0

            if (shadowObject) shadowObject.SetActive(true);

            // Enable and reset overlay alpha → starts from clear, then darkens
            if (fadeOverlay)
            {
                fadeOverlay.gameObject.SetActive(true);
                var col = fadeOverlay.color;
                col.a = 0f;
                fadeOverlay.color = col;
                // Optional: ensure it doesn't block clicks
                fadeOverlay.raycastTarget = false;
            }

            // Play audio cue
            if (warningAudioSource && shadowApproachingClip)
            {
                warningAudioSource.PlayOneShot(shadowApproachingClip);
            }
            
            OnShadowSpawned?.Invoke();

            DebugLog($"[ShadowTimer] Shadow spawned! Duration: {_currentShadowDuration:F1}s");
        }


        
        private void UpdateGradualDarkness()
        {
            if (!fadeOverlay) return;

            float maxDur = (_shadowMaxDuration > 0f) ? _shadowMaxDuration : Mathf.Max(0.0001f, _currentShadowDuration);
            float remaining = Mathf.Clamp(_shadowTimer, 0f, maxDur);

            // 0 → just spawned (bright), 1 → time's up (darkest)
            float progress = 1f - (remaining / maxDur);
            progress = Mathf.Clamp01(progress);

            float curved = darknessCurve != null ? darknessCurve.Evaluate(progress) : progress;

            var col = fadeOverlay.color;
            col.a = curved * maxDarknessAlpha;
            fadeOverlay.color = col;

            // Optional: escalate warning volume with darkness
            if (warningAudioSource && shadowApproachingClip)
            {
                warningAudioSource.volume = 0.25f + 0.75f * curved; // 0.25 → 1.0
            }
        }


        private void OnPlayerCheckedShadow()
        {
            DebugLog("[ShadowTimer] Player checked shadow - resetting!");
            StopShadow();
            _nextCheckTime = Time.time + checkIntervalSeconds; // Reset check interval
        }

        private void StopShadow()
        {
            _shadowActive = false;
            _shadowTimer = 0f;

            if (shadowObject) shadowObject.SetActive(false);
            if (warningAudioSource)
            {
                warningAudioSource.Stop();
                warningAudioSource.volume = 1f;
            }
            if (fadeOverlay)
            {
                var col = fadeOverlay.color;
                col.a = 0f;
                fadeOverlay.color = col;
                fadeOverlay.gameObject.SetActive(false);
            }
            
            OnShadowCleared?.Invoke();
        }

        private void OnShadowCaughtPlayer()
        {
            DebugLog("[ShadowTimer] Shadow caught player - MATCH LOST");
            
            _shadowActive = false;
            StartCoroutine(DeathSequence());
            
            OnShadowCaught?.Invoke();
        }

        private IEnumerator DeathSequence()
        {
            // Stop all gameplay
            _isActive = false;
            
            // Play death sound
            if (warningAudioSource && shadowDeathClip)
            {
                warningAudioSource.PlayOneShot(shadowDeathClip);
            }

            // Fade to black
            if (fadeOverlay)
            {
                fadeOverlay.gameObject.SetActive(true);
                float fadeTime = 1.5f;
                float elapsed = 0f;
                
                Color col = fadeOverlay.color;
                while (elapsed < fadeTime)
                {
                    elapsed += Time.deltaTime;
                    col.a = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
                    fadeOverlay.color = col;
                    yield return null;
                }
            }

            // Show death message
            if (deathMessageText)
            {
                deathMessageText.gameObject.SetActive(true);
                deathMessageText.text = "Life caught up to you...";
            }

            yield return new WaitForSeconds(2f);

            // Return to MatchReady for the SAME enemy (treat as a loss)
            if (_dir != null)
            {
                _dir.EndMatch(false);  // loss → Director restarts Match; CampaignIndex unchanged
            }


        }

        private void UpdateShadowVisuals()
        {
            // Placeholder for visual feedback
            // You'll implement this later with actual shadow approach animation
            // For now, just log when timer is getting low
            
            if (_shadowTimer <= 5f && _shadowTimer > 4.9f)
            {
                DebugLog("[ShadowTimer] WARNING: Shadow very close!");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void DebugLog(string msg) => Debug.Log(msg);

#if UNITY_EDITOR
        private void OnGUI()
        {
            
            if (!_isActive) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label($"<b>SHADOW TIMER DEBUG</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"Next Check: {Mathf.Max(0, _nextCheckTime - Time.time):F1}s");
            GUILayout.Label($"Spawn Chance: {_currentSpawnChance:F1}%");
            GUILayout.Label($"Shadow Active: {_shadowActive}");
            if (_shadowActive)
            {
                GUILayout.Label($"Time Remaining: {_shadowTimer:F1}s");
                GUILayout.Label($"<color=red>LOOK LEFT OR RIGHT TO RESET!</color>", new GUIStyle(GUI.skin.label) { richText = true });
            }
            
            if (GUILayout.Button("Force Spawn Shadow"))
            {
                SpawnShadow();
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}