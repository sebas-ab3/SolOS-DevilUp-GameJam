// Assets/Scripts/Core/GameDirector.cs
using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Minimal scene-level state hub.
    /// One instance in the scene. Keeps just enough events to move forward.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [SerializeField] private GameState _initialState = GameState.Boot;
        [SerializeField] private GameState _state;
        
        // True only after player presses START in MatchReady (gameplay actually running)
        [SerializeField] private bool _matchGameplayActive = false;
        public bool MatchGameplayActive => _matchGameplayActive;

        
        public event Action OnMatchActive;   // NEW: Fired when "Start Match" button is pressed
        public GameState State => _state;
        
        
        // Events (subscribe in OnEnable, unsubscribe in OnDisable)
        public event Action<GameState, GameState> OnStateChanged;
        public event Action OnMatchStarted;
        public event Action<bool> OnMatchEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Enter initial state without firing events (listeners may not be ready yet).
            SetState(_initialState, invokeEvents: false);
        }

        // GameDirector.cs
        private void Start()
        {
            // Start the campaign at boot (Tutorial first).
            Game.Campaign.CampaignManager.Instance.BeginCampaign();

            // Let late listeners do an initial sync if they want.
            OnStateChanged?.Invoke(_state, _state);
        }


        public void SetState(GameState next, bool invokeEvents = true)
        {
            if (_state == next) return;
            var prev = _state;
            _state = next;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[STATE] {prev} → {_state}");
            if (_state == GameState.Intermission)
            {
                Debug.Log($"[STATE] Intermission requested by:\n{new System.Diagnostics.StackTrace(true)}");
            }
#endif

            if (_state != GameState.Match)
                _matchGameplayActive = false;

            if (invokeEvents) OnStateChanged?.Invoke(prev, _state);
        }



        // Simple helpers you can call from UI buttons later
        public void StartFreeplay()
        {
            SetState(GameState.Freeplay);
            OnMatchStarted?.Invoke();
            OnMatchActive?.Invoke(); // NEW: In freeplay, auto-start (no ready screen)
        }

        public void StartMatch()
        {
            SetState(GameState.Match);
            _matchGameplayActive = false;  // ← pre-activation (MatchReady)
            OnMatchStarted?.Invoke();      // shows ready screen (HUD binder will read the flag)
            // OnMatchActive is fired separately when player presses Start
        }


        // NEW: Called by MatchReadyUI when Start button is pressed
        public void ActivateMatch()
        {
            Debug.Log($"===== [GameDirector] ActivateMatch() CALLED! Current state: {_state} =====");
            if (_state != GameState.Match) return; // guard

            _matchGameplayActive = true;            // ← gameplay begins now

            if (OnMatchActive != null)
            {
                Debug.Log($"[GameDirector] OnMatchActive has {OnMatchActive.GetInvocationList().Length} subscribers");
                OnMatchActive.Invoke();
                Debug.Log("[GameDirector] OnMatchActive invoked!");
            }
            else
            {
                Debug.LogWarning("[GameDirector] OnMatchActive is NULL - no subscribers!");
            }
        }

        
        public void EndMatch(bool playerWon)
        {
            OnMatchEnded?.Invoke(playerWon);

            if (!playerWon)
            {
                // Loss: restart the SAME enemy (CampaignManager keeps CurrentIndex unchanged)
                StartMatch();            // back to Match state (pre-activation → ready screen)
                return;
            }

            // Win: do nothing here — CampaignManager.HandleMatchEnded(playerWon) will:
            //  - Set Intermission (if not last)
            //  - Or set CampaignComplete (if last)
        }

        public void ResetCampaign()
        {
            var cm = Game.Campaign.CampaignManager.Instance;
            if (cm != null)
            {
                cm.BeginCampaign();   // sets CurrentIndex = 0 and SetState(Tutorial)
            }
            else
            {
                // Fallback: just go to Tutorial so HUD/UI reset safely
                SetState(GameState.Tutorial);
            }
        }

        public void StartIntermission() => SetState(GameState.Intermission);
        public void StartTutorial()     => SetState(GameState.Tutorial);
        public void CompleteCampaign()  => SetState(GameState.CampaignComplete);

        // Tiny debug: F-keys to jump states (dev builds only)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Input.GetKeyDown(KeyCode.F1)) SetState(GameState.Freeplay);
            if (Input.GetKeyDown(KeyCode.F2)) SetState(GameState.Match);
            if (Input.GetKeyDown(KeyCode.F3)) SetState(GameState.Intermission);
            if (Input.GetKeyDown(KeyCode.F4)) SetState(GameState.Tutorial);
            if (Input.GetKeyDown(KeyCode.F5)) SetState(GameState.CampaignComplete);
            if (Input.GetKeyDown(KeyCode.F6)) ResetCampaign(); // NEW: Quick restart campaign
            if (Input.GetKeyDown(KeyCode.F7)) ActivateMatch(); // NEW: Quick test match activation
        }
#endif
    }
}