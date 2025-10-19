// Assets/Scripts/AI/EnemyProfile.cs
using UnityEngine;

namespace DudoGame.AI
{
    [CreateAssetMenu(fileName = "EnemyProfile", menuName = "Dudo/Enemy Profile", order = 0)]
    public class EnemyProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Rival";

        [Header("Setup")]
        [Tooltip("How many dice the AI starts with at the very beginning of a match.")]
        [Min(1)] public int startingDice = 5;

        [Header("Tempo / Thinking")]
        [Tooltip("Min seconds the AI waits before acting.")]
        [Min(0f)] public float thinkTimeMin = 0.6f;
        [Tooltip("Max seconds the AI waits before acting.")]
        [Min(0f)] public float thinkTimeMax = 1.4f;

        [Header("Tendencies")]
        [Range(0f, 1f)] public float aggression = 0.55f;   // higher → raise more, call less
        [Range(0f, 1f)] public float raiseBias = 0.40f;     // when raising, how often to skip to a stronger legal raise
        [Range(0f, 1f)] public float bluffChance = 0.10f;   // small chance to push a dubious raise
        [Range(0f, 1f)] public float spotOnChance = 0.05f;  // very rare “spot-on” attempts

        [Header("Risk Controls")]
        [Tooltip("If current bid quantity / total dice exceeds this, prefer to call.")]
        [Range(0.2f, 1.2f)] public float callPressureRatio = 0.65f;

        [Tooltip("Reduce aggression when AI has few dice left (0 = no effect).")]
        [Range(0f, 1f)] public float lowDiceNerf = 0.35f;
        [Min(1)] public int lowDiceThreshold = 2;
    }
}