// Assets/Scripts/UI/MatchReadyUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// Shows before match gameplay begins. Player must press Start to begin.
    /// </summary>
    public class MatchReadyUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text matchNumberText;
        [SerializeField] private TMP_Text instructionsText;
        [SerializeField] private Button startButton;

        [Header("Optional Flavor Text")]
        [SerializeField] private string[] matchIntroTexts = new string[]
        {
            "Match 1: The game begins...",
            "Match 2: The stakes rise...",
            "Match 3: Final confrontation..."
        };

        private GameDirector _dir;
        private bool _subscribed = false;

        private void OnEnable()
        {
            WireButton();
            
            // Wait for GameDirector and refresh UI
            if (GameDirector.Instance != null)
            {
                _dir = GameDirector.Instance;
                RefreshUI();
            }
            
        }

        private void OnDisable()
        {
            UnwireButton();
        }

        private void WireButton()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonPressed); // Safety
                startButton.onClick.AddListener(OnStartButtonPressed);
            }
        }

        private void UnwireButton()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartButtonPressed);
            }
        }

        private void OnStartButtonPressed()
        {
            if (_dir == null) _dir = GameDirector.Instance;
            if (_dir != null)
            {
                _dir.ActivateMatch(); // fires OnMatchActive; HUDStateBinder will switch to DudoUI
                // REMOVE the SetActive(false) here
                // gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[MatchReadyUI] No GameDirector found!");
            }
        }


        private void RefreshUI()
        {
            if (_dir == null) return;

            int matchNum = _dir.CurrentMatchNumber;

            // Update match number
            if (matchNumberText != null)
            {
                matchNumberText.text = $"MATCH {matchNum}";
            }

            // Update flavor text
            if (instructionsText != null)
            {
                string intro = "";
                if (matchNum > 0 && matchNum <= matchIntroTexts.Length)
                {
                    intro = matchIntroTexts[matchNum - 1];
                }
                
                instructionsText.text = intro + "\n\nPress START when ready.";
            }
        }

#if UNITY_EDITOR
        // For testing in editor
        private void Reset()
        {
            // Try to auto-find button in children
            if (startButton == null)
            {
                startButton = GetComponentInChildren<Button>();
            }
        }
#endif
    }
}