// Assets/Scripts/Campaign/CampaignManager.cs
using UnityEngine;
using Game.Core;          // GameDirector, GameState
using DudoGame.AI;        // EnemyProfileSO

namespace Game.Campaign
{
    /// <summary>
    /// Linear 3-match campaign:
    /// Tutorial → Match(Enemy 0) → Intermission → Match(Enemy 1) → Intermission → Match(Enemy 2) → CampaignComplete
    /// </summary>
    public class CampaignManager : MonoBehaviour
    {
        public static CampaignManager Instance { get; private set; }

        [Header("Enemy Order (3 matches)")]
        [SerializeField] private EnemyProfileSO[] enemyOrder = new EnemyProfileSO[3];

        [Header("Intermission Dialog (after M1, after M2)")]
        [TextArea(2, 6)] [SerializeField] private string[] intermission1Lines;
        [TextArea(2, 6)] [SerializeField] private string[] intermission2Lines;

        /// <summary>Index of the current enemy (0..enemyOrder.Length-1).</summary>
        public int CurrentIndex { get; private set; } = 0;

        /// <summary>The enemy profile for the current match, used by DudoUIController.</summary>
        public EnemyProfileSO CurrentEnemy =>
            (enemyOrder != null && CurrentIndex >= 0 && CurrentIndex < enemyOrder.Length)
                ? enemyOrder[CurrentIndex]
                : null;

        private GameDirector _dir;

        // ---------------- Lifecycle ----------------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Optional: keep this across scene loads
            // DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            _dir = GameDirector.Instance;
            if (_dir != null)
            {
                _dir.OnMatchEnded += HandleMatchEnded;
            }
        }

        private void OnDisable()
        {
            if (_dir != null)
            {
                _dir.OnMatchEnded -= HandleMatchEnded;
            }
        }

        // ---------------- Entry Points ----------------

        /// <summary>
        /// Call this once at boot (e.g., from GameDirector.Start or a main menu).
        /// It puts the game into Tutorial first.
        /// </summary>
        public void BeginCampaign()
        {
            CurrentIndex = 0;
            EnsureDirector();
            _dir.StartTutorial();                 // HUD shows Tutorial UI
        }

        /// <summary>
        /// Hook this to your Tutorial "Continue" button.
        /// Starts MatchReady for Enemy 0; player presses Start to begin gameplay.
        /// </summary>
        public void FinishTutorial()
        {
            EnsureDirector();
            _dir.StartMatch();                    // HUD: MatchReady → OnMatchActive → gameplay
        }

        /// <summary>
        /// Hook this to your Intermission "Continue" button.
        /// Advances to the next enemy and starts the next match.
        /// </summary>
        public void FinishIntermission()
        {
            // Move to next enemy
            if (enemyOrder != null && enemyOrder.Length > 0)
                CurrentIndex = Mathf.Clamp(CurrentIndex + 1, 0, enemyOrder.Length - 1);

            EnsureDirector();
            _dir.StartMatch();
        }

        // ---------------- Event Handlers ----------------

        /// <summary>
        /// Called by GameDirector when a match ends.
        /// </summary>
        private void HandleMatchEnded(bool playerWon)
        {
            EnsureDirector();

            if (!playerWon)
            {
                // On loss, your GameDirector already restarts the same match (StartMatch).
                // No change to CurrentIndex here.
                return;
            }

            // Win path
            bool lastMatch = (enemyOrder == null || CurrentIndex >= enemyOrder.Length - 1);
            if (lastMatch)
            {
                _dir.CompleteCampaign();          // Conclusion / credits
            }
            else
            {
                _dir.StartIntermission();         // Intermission before next enemy
            }
        }

        // ---------------- Helpers ----------------

        /// <summary>
        /// Intermission UI can ask CampaignManager for which set of lines to display.
        /// Called while we are between matches (after a win, before advancing index).
        /// </summary>
        public string[] GetCurrentIntermissionLines()
        {
            // After beating enemy 0 (going to enemy 1) → use intermission1Lines
            if (CurrentIndex == 0) return intermission1Lines;
            // After beating enemy 1 (going to enemy 2) → use intermission2Lines
            if (CurrentIndex == 1) return intermission2Lines;
            return System.Array.Empty<string>();
        }

        private void EnsureDirector()
        {
            if (_dir == null) _dir = GameDirector.Instance;
        }
    }
}
