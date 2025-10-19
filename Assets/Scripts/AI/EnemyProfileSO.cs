using UnityEngine;

namespace DudoGame.AI
{
    [CreateAssetMenu(fileName = "EnemyProfileSO", menuName = "Dudo/Enemy Profile", order = 0)]
    public class EnemyProfileSO : ScriptableObject
    {
        [Header("Display")]
        public string displayName = "Rival";

        [Header("Setup")]
        [Min(1)] public int startingDice = 5;

        [Header("AI Tendencies (mapped to your DudoGameManager fields)")]
        [Range(0f, 1f)] public float callDudoBase      = 0.15f;  // chance to Call
        [Range(0f, 1f)] public float spotOnBase        = 0.10f;  // chance to Spot On
        [Range(0f, 1f)] public float raiseQuantityBias = 0.60f;  // bias towards qty raise vs face raise
        [Range(0f, 1f)] public float aggression        = 0.50f;  // general raise vs call tendency (used by your logic)

        [Header("Tempo / Thinking (UI delay only)")]
        [Min(0f)] public float thinkTimeMin = 0.6f;
        [Min(0f)] public float thinkTimeMax = 1.4f;
    }
}