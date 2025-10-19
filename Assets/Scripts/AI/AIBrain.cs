// Assets/Scripts/AI/AIBrain.cs
using UnityEngine;
using DudoGame;

namespace DudoGame.AI
{
    public class AIBrain : IAIBrain
    {
        readonly EnemyProfile _p;
        readonly System.Random _rng;

        public AIBrain(EnemyProfile profile, int seed = 0)
        {
            _p = profile;
            _rng = (seed == 0) ? new System.Random() : new System.Random(seed);
        }

        float R() => (float)_rng.NextDouble();

        public float NextThinkDelay()
        {
            if (_p == null) return 1.0f;
            return Mathf.Lerp(_p.thinkTimeMin, _p.thinkTimeMax, R());
        }

        public (string action, Bid bid) Decide(DudoGameManager gm)
        {
            // Clamp helpers
            int TableMax()
            {
                int total = 0;
                foreach (var pl in gm.players) if (!pl.eliminated) total += pl.diceCount;
                return Mathf.Max(1, total);
            }

            var tableMax = TableMax();

            // If no current bid, always open with the minimal legal bid (e.g. 1×2)
            if (gm.currentBid == null)
            {
                var first = DudoRules.NextRaise(null, tableMax) ?? new Bid(1, 2);
                return ("raise", first);
            }

            // Compute pressure: claimed qty vs. total dice
            float pressure = (float)gm.currentBid.quantity / tableMax;

            // Adjusted aggression if AI is low on dice
            var me = gm.players[1]; // assuming index 1 is AI as in your UI code
            float aggr = _p.aggression;
            if (me.diceCount <= _p.lowDiceThreshold)
                aggr = Mathf.Clamp01(aggr * (1f - _p.lowDiceNerf));

            // Rare SPOT-ON attempt when pressure is moderate
            if (R() < _p.spotOnChance && pressure > 0.3f && pressure < 0.6f)
                return ("spoton", null);

            // Prefer CALL when pressure too high or aggression test fails
            bool tooHigh = pressure >= _p.callPressureRatio;
            bool wantsRaise = (R() < aggr) && !tooHigh;

            if (!wantsRaise)
                return ("call", null);

            // Raise: optionally “skip” to a stronger legal raise depending on raiseBias
            // NextRaise gives the minimal legal raise; we can step forward a few times.
            Bid candidate = DudoRules.NextRaise(gm.currentBid, tableMax);
            if (candidate == null) return ("call", null);

            // With some bluff chance, try stepping even if it becomes dubious
            int extraSteps = (R() < _p.raiseBias) ? 1 : 0;
            if (R() < _p.raiseBias) extraSteps++;
            if (R() < _p.bluffChance) extraSteps++; // occasionally overreach

            for (int i = 0; i < extraSteps; i++)
            {
                var next = DudoRules.NextRaise(candidate, tableMax);
                if (next == null) break;
                candidate = next;
            }

            // Final safety clamp to table bounds
            candidate.quantity = Mathf.Clamp(candidate.quantity, 1, tableMax);
            candidate.value = Mathf.Clamp(candidate.value, 1, 6);

            return ("raise", candidate);
        }
    }
}
