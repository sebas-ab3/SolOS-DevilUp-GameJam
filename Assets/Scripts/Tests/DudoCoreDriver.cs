// Assets/Scripts/Gameplay/Tests/DudoCoreDriver.cs
using UnityEngine;
using DudoGame;

public class DudoCoreDriver : MonoBehaviour
{
    [SerializeField] int startingDice = 5;
    [SerializeField] int seed = 12345;
    [SerializeField] int maxRounds = 200;
    [SerializeField] bool logEachAction = true;

    void Start()
    {
        var rng = new SystemRng(seed);
        var gm  = new DudoGameManager("You", startingDice, rng);

        // OPTIONAL: if you already have EnemyProfile class, you can pass one:
        // gm.SetEnemyProfile(new EnemyProfile{ enemyName="AI", startingDice=5, callDudoBase=0.15f, spotOnBase=0.10f, raiseQuantityBias=0.60f, aggression=0.5f });

        int rounds = 0;
        while (!gm.IsGameOver() && rounds < maxRounds)
        {
            gm.StartNewRound();
            if (logEachAction) Debug.Log($"[DUDO] Round {rounds+1} start. P{gm.currentPlayerIndex} to act.");

            while (gm.roundActive && !gm.IsGameOver())
            {
                // Step 2: use AI for both sides to fully automate
                var (action, bid) = gm.GetAIDecision();

                if (action == "raise")
                {
                    gm.MakeBid(bid.quantity, bid.value);
                    if (logEachAction) Debug.Log($"[DUDO] Bid {bid}");
                }
                else if (action == "call")
                {
                    gm.CallBid();
                    if (logEachAction) DumpTopLog(gm, 3);
                }
                else
                {
                    gm.SpotOn();
                    if (logEachAction) DumpTopLog(gm, 3);
                }
            }

            rounds++;
        }

        var winner = gm.GetWinner()?.playerName ?? "(n/a)";
        Debug.Log($"[DUDO] Winner: {winner} after {rounds} rounds.");
    }

    static void DumpTopLog(DudoGameManager gm, int lines)
    {
        int take = Mathf.Min(lines, gm.gameLog.Count);
        for (int i = 0; i < take; i++)
            Debug.Log("[LOG] " + gm.gameLog[i]);
    }
}
