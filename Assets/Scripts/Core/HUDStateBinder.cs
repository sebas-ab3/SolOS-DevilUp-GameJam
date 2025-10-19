// Assets/Scripts/UI/HUDStateBinder.cs
using UnityEngine;
using System.Collections;
using Game.Core;

namespace Game.UI
{
    /// <summary>Enables exactly one UI panel based on GameState.</summary>
    public class HUDStateBinder : MonoBehaviour
    {
        [Header("Assign SCENE INSTANCES (drag from Hierarchy)")]
        [SerializeField] private GameObject matchReadyUI;    // NEW: Match ready screen
        [SerializeField] private GameObject dudoUI;          // Freeplay/Match (active gameplay)
        [SerializeField] private GameObject intermissionUI;  // Intermission
        [SerializeField] private GameObject tutorialUI;      // Tutorial

        [Header("Optional (Boot/CampaignComplete)")]
        [SerializeField] private GameObject fallbackUI;

        private GameDirector _dir;
        private bool _subscribed;
        private bool _matchIsActive = false; // Track if match gameplay has started

        private void OnEnable()
        {
            StartCoroutine(AttachWhenReady());
        }

        private void OnDisable()
        {
            Detach();
        }

        private IEnumerator AttachWhenReady()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var all = FindObjectsOfType<GameDirector>(includeInactive: true);
            if (all.Length > 1)
                Debug.LogWarning($"[HUDStateBinder] Found {all.Length} GameDirectors. Duplicates can break UI binding.");
#endif

            while (GameDirector.Instance == null)
                yield return null;

            _dir = GameDirector.Instance;

            if (!_subscribed && _dir != null)
            {
                _dir.OnStateChanged += HandleStateChanged;
                _dir.OnMatchActive += HandleMatchActive; // NEW: Listen for match activation
                _subscribed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[HUDStateBinder] Subscribed to GameDirector #{_dir.GetInstanceID()}.");
#endif
                Apply(_dir.State, "Initial sync after attach");
            }
        }

        private void Detach()
        {
            if (_subscribed && _dir != null)
            {
                _dir.OnStateChanged -= HandleStateChanged;
                _dir.OnMatchActive -= HandleMatchActive;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[HUDStateBinder] Unsubscribed from GameDirector #{_dir.GetInstanceID()}.");
#endif
            }
            _subscribed = false;
            _dir = null;
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HUDStateBinder] StateChanged {from} -> {to}");
#endif
            
            // Reset match active flag when leaving Match state
            if (to != GameState.Match)
            {
                _matchIsActive = false;
            }
            
            Apply(to, "OnStateChanged");
        }

        // NEW: When match becomes active, switch from ready screen to gameplay
        private void HandleMatchActive()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[HUDStateBinder] Match activated - switching to gameplay UI");
#endif
            _matchIsActive = true;
            Apply(_dir.State, "OnMatchActive");
        }

        private void Apply(GameState state, string why)
        {
            SetPanel(matchReadyUI,   false, why);
            SetPanel(intermissionUI, false, why);
            SetPanel(tutorialUI,     false, why);
            SetPanel(dudoUI,         false, why);
            SetPanel(fallbackUI,     false, why);

            bool gameplayActive = (_dir != null && _dir.MatchGameplayActive);

            switch (state)
            {
                case GameState.Freeplay:
                    SetPanel(dudoUI, true, why);
                    break;

                case GameState.Match:
                    if (_matchIsActive)
                    {
                        SetPanel(dudoUI, true, why);          // ✅ ON
                        SetPanel(matchReadyUI, false, why);   // ✅ OFF
                    }
                    else
                    {
                        SetPanel(matchReadyUI, true, why);
                        SetPanel(dudoUI, false, why);
                    }
                    break;

                case GameState.Intermission:
                    SetPanel(intermissionUI, true, why);
                    break;

                case GameState.Tutorial:
                    SetPanel(tutorialUI, true, why);
                    break;

                case GameState.Boot:
                case GameState.CampaignComplete:
                default:
                    SetPanel(fallbackUI, true, why);
                    break;
            }
        }


        private static void SetPanel(GameObject go, bool on, string why)
        {
            if (!go) return;
            go.SetActive(on);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[HUDStateBinder] {(on ? "ENABLED" : "disabled")} '{go.name}' ({go.GetInstanceID()}) via {why}. activeSelf={go.activeSelf}, activeInHierarchy={go.activeInHierarchy}");
#endif
        }

#if UNITY_EDITOR
        [ContextMenu("TEST: Show Match Ready")]
        private void TestShowMatchReady()
        {
            _matchIsActive = false;
            Apply(GameState.Match, "ContextMenu Test");
        }

        [ContextMenu("TEST: Show Dudo (Active Match)")]
        private void TestShowDudoActive()
        {
            _matchIsActive = true;
            Apply(GameState.Match, "ContextMenu Test");
        }
#endif
    }
}