using System;
using System.Collections.Generic;

namespace DudoGame
{
    // --- RNG interface (so core stays Unity-free) ---
    public interface IRng
    {
        int Next(int minInclusive, int maxExclusive);
        float Value();
    }

    public sealed class SystemRng : IRng
    {
        private readonly Random _rng;
        public SystemRng(int seed) { _rng = new Random(seed); }
        public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
        public float Value() => (float)_rng.NextDouble();
    }

    [Serializable]
    public class Bid
    {
        public int quantity;
        public int value; // 1 = Aces, 2–6 = normal faces

        public Bid(int q, int v) { quantity = q; value = v; }

        public bool IsValid() => quantity > 0 && value >= 1 && value <= 6;

        public override string ToString()
        {
            string valueName = (value == 1) ? "Aces" : value.ToString();
            return $"{quantity} {valueName}";
        }
    }

    [Serializable]
    public class Player
    {
        public string playerName;
        public List<int> dice;
        public int diceCount;
        public int maxDice; // Maximum dice this player can have
        public bool isAI;
        public bool eliminated;

        public Player(string name, int startingDice, bool ai)
        {
            playerName = name;
            diceCount = startingDice;
            maxDice = startingDice; // Set max to starting amount
            isAI = ai;
            eliminated = false;
            dice = new List<int>();
        }

        public void RollDice(IRng rng)
        {
            dice.Clear();
            for (int i = 0; i < diceCount; i++)
                dice.Add(rng.Next(1, 7));
        }

        public void GainDie()
        {
            if (!eliminated && diceCount < maxDice) // Only gain if not eliminated and below max
            {
                diceCount++;
            }
        }
        
        public void LoseDie()
        {
            diceCount--;
            if (diceCount <= 0)
            {
                eliminated = true;
                diceCount = 0;
            }
        }
    }

    public static class DudoRules
    {
        // Count how many dice match the bid (aces are wild for non-ace bids)
        public static int CountDice(List<Player> players, int value)
        {
            int count = 0;
            foreach (var player in players)
            {
                if (player.eliminated) continue;
                foreach (int die in player.dice)
                {
                    if (value == 1)
                    {
                        if (die == 1) count++;
                    }
                    else
                    {
                        if (die == value || die == 1) count++;
                    }
                }
            }
            return count;
        }

        // Validation with table maximum awareness
        public static (bool valid, string reason) IsValidBid(Bid currentBid, Bid newBid, int totalDiceInPlay = -1)
        {
            if (newBid == null || !newBid.IsValid())
                return (false, "Invalid bid values");

            if (totalDiceInPlay > 0 && newBid.quantity > totalDiceInPlay)
                return (false, $"Cannot bid more than the table maximum of {totalDiceInPlay}");

            if (currentBid == null)
            {
                if (newBid.value == 1) return (false, "First bid cannot be Aces");
                return (true, "");
            }

            int prevQty = currentBid.quantity;
            int prevVal = currentBid.value;
            int newQty  = newBid.quantity;
            int newVal  = newBid.value;

            // If quantity is already at the table max, the only legal raise is a higher face (same qty)
            if (totalDiceInPlay > 0 && prevQty >= totalDiceInPlay)
            {
                if (newQty != prevQty)
                    return (false, $"Quantity is already at the table maximum ({totalDiceInPlay}); you must increase the face value");
                if (newVal <= prevVal)
                    return (false, $"Must increase face value above {prevVal}");
                return (true, "");
            }

            // To aces from non-aces
            if (newVal == 1 && prevVal != 1)
            {
                // Next whole number after halving: floor(qty/2) + 1
                int minAces = (prevQty / 2) + 1;
                if (newQty < minAces)
                    return (false, $"Must bid at least {minAces} Aces (next whole number after halving {prevQty})");
                return (true, "");
            }

            // From aces to non-aces  (UPDATED RULE)
            if (newVal != 1 && prevVal == 1)
            {
                int minQty = prevQty * 2 + 1;

                // If the required quantity exceeds table max, conversion is impossible.
                if (totalDiceInPlay > 0 && minQty > totalDiceInPlay)
                    return (false, $"You need at least {minQty} in order to increase face value");

                if (newQty < minQty)
                    return (false, $"You need at least {minQty} in order to increase face value");

                return (true, "");
            }

            // Same face: must increase quantity
            if (newVal == prevVal)
            {
                if (newQty <= prevQty)
                    return (false, $"Must increase quantity above {prevQty}");
                return (true, "");
            }

            // Both non-aces, different values
            if (newVal != 1 && prevVal != 1)
            {
                if (newQty > prevQty) return (true, "");
                if (newQty == prevQty && newVal > prevVal) return (true, "");
                if (newQty == prevQty && newVal <= prevVal)
                    return (false, $"Must increase face value above {prevVal}");
                if (newQty < prevQty && newVal > prevVal)
                    return (false, $"Cannot decrease quantity below {prevQty} when increasing face value");

                return (false, $"Must increase quantity above {prevQty} or (with same quantity) increase face value above {prevVal}");
            }

            return (false, "Invalid bid");
        }

        /// <summary>
        /// Computes the next legal raise above currentBid, preferring:
        /// 1) same qty face+1, 2) qty+1 same face, 3) minimal legal conversion to aces,
        /// 4) otherwise first valid by scan.
        /// </summary>
        public static Bid NextRaise(Bid currentBid, int totalDiceInPlay)
        {
            if (totalDiceInPlay <= 0) totalDiceInPlay = int.MaxValue;

            if (currentBid == null)
                return new Bid(1, 2);

            int q = currentBid.quantity;
            int v = currentBid.value;

            // 1) Try same quantity, face+1
            if (v < 6)
            {
                var b = new Bid(q, v + 1);
                if (IsValidBid(currentBid, b, totalDiceInPlay).valid)
                    return b;
            }

            // 2) Try quantity+1, same face
            if (q + 1 <= totalDiceInPlay)
            {
                var b = new Bid(q + 1, v);
                if (IsValidBid(currentBid, b, totalDiceInPlay).valid)
                    return b;
            }

            // 3) If non-aces → minimal legal conversion to aces
            if (v != 1)
            {
                int minAces = (q / 2) + 1;
                minAces = Math.Min(minAces, totalDiceInPlay);
                var toAces = new Bid(minAces, 1);
                if (IsValidBid(currentBid, toAces, totalDiceInPlay).valid)
                    return toAces;
            }

            // 4) Fallback: scan upward for first valid
            for (int qty = q; qty <= totalDiceInPlay; qty++)
            {
                int startFace = (qty == q) ? Math.Min(6, v + 1) : 1;
                for (int face = startFace; face <= 6; face++)
                {
                    var test = new Bid(qty, face);
                    if (IsValidBid(currentBid, test, totalDiceInPlay).valid)
                        return test;
                }
            }

            return null;
        }
    }

    public class DudoGameManager
    {
        
        
        public readonly List<Player> players;
        public int currentPlayerIndex;
        public Bid currentBid;
        public int lastBidderIndex;
        public bool roundActive;
        public readonly List<string> gameLog;

        private readonly IRng _rng;

        private EnemyProfile _enemy; // optional, for AI tuning

        public DudoGameManager(string playerName, int startingDice, IRng rng = null)
        {
            _rng = rng ?? new SystemRng(Environment.TickCount);

            players = new List<Player>
            {
                new Player(playerName, startingDice, false),
                new Player("AI", startingDice, true)
            };

            gameLog = new List<string>();
            currentPlayerIndex = 0;
            roundActive = false;
        }

        public void SetEnemyProfile(EnemyProfile profile) => _enemy = profile;

        public void StartNewRound()
        {
            currentBid = null;
            lastBidderIndex = -1;
            roundActive = true;

            foreach (var p in players)
                if (!p.eliminated) p.RollDice(_rng);

            AddLog($"New round started. {players[currentPlayerIndex].playerName} goes first.");
        }

        public void MakeBid(int quantity, int value)
        {
            currentBid = new Bid(quantity, value);
            lastBidderIndex = currentPlayerIndex;
            AddLog($"{players[currentPlayerIndex].playerName} bids {currentBid}");
            NextTurn();
        }

        public void CallBid()
        {
            if (currentBid == null) return;

            roundActive = false;
            int actual = DudoRules.CountDice(players, currentBid.value);
            int bidder = lastBidderIndex;
            int caller = currentPlayerIndex;

            string valueName = (currentBid.value == 1) ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            int loser = (actual >= currentBid.quantity) ? caller : bidder;
            AddLog((actual >= currentBid.quantity)
                ? $"Bid was correct! {players[caller].playerName} loses a die!"
                : $"Bid was wrong! {players[bidder].playerName} loses a die!");

            players[loser].LoseDie();
            currentPlayerIndex = loser; // loser starts next round
        }

        public void SpotOn()
        {
            if (currentBid == null) return;

            roundActive = false;
            int actual = DudoRules.CountDice(players, currentBid.value);
            int caller = currentPlayerIndex;

            string valueName = (currentBid.value == 1) ? "Aces" : currentBid.value.ToString();
            AddLog($"{players[caller].playerName} calls Spot On! Checking...");
            AddLog($"Actual count: {actual} {valueName}");

            if (actual == currentBid.quantity)
            {
                // Spot on was correct - caller gains a die!
                AddLog($"Spot On! {players[caller].playerName} gains a die!");
                players[caller].GainDie();
                currentPlayerIndex = lastBidderIndex;
            }
            else
            {
                AddLog($"Wrong! {players[caller].playerName} loses a die!");
                players[caller].LoseDie();
                currentPlayerIndex = caller; // caller starts next
            }
        }

        public bool IsGameOver()
        {
            int active = 0;
            foreach (var p in players) if (!p.eliminated) active++;
            return active <= 1;
        }

        public Player GetWinner()
        {
            foreach (var p in players) if (!p.eliminated) return p;
            return null;
        }

        private void NextTurn()
        {
            do
            {
                currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
            } while (players[currentPlayerIndex].eliminated);
        }

        public void AddLog(string message)
        {
            gameLog.Insert(0, message);
            if (gameLog.Count > 20) gameLog.RemoveAt(20);
        }

        // --- Simple AI (unchanged structure) ---
        public (string action, Bid bid) GetAIDecision()
        {
            int maxDice = GetTotalDiceInPlay();

            if (currentBid != null && currentBid.quantity >= maxDice && currentBid.value >= 6)
                return ("call", null);

            float callBase  = (_enemy != null) ? _enemy.callDudoBase      : 0.15f;
            float spotBase  = (_enemy != null) ? _enemy.spotOnBase        : 0.10f;
            float raiseBias = (_enemy != null) ? _enemy.raiseQuantityBias : 0.60f;
            float aggr      = (_enemy != null) ? _enemy.aggression        : 0.50f;

            if (currentBid == null)
            {
                int q = Math.Clamp(_rng.Next(1, 4), 1, Math.Max(1, maxDice));
                int v = _rng.Next(2, 7);
                return ("raise", new Bid(q, v));
            }

            float r = _rng.Value();
            if (r < callBase) return ("call", null);
            if (r < callBase + spotBase) return ("spoton", null);

            Bid candidate = GenerateAIBidWithBias(maxDice, raiseBias, aggr);
            if (candidate != null) return ("raise", candidate);

            return ("call", null);
        }

        private Bid GenerateAIBidWithBias(int maxDice, float raiseQuantityBias, float aggression)
        {
            if (currentBid == null)
                return new Bid(Math.Clamp(_rng.Next(1, 4), 1, Math.Max(1, maxDice)), _rng.Next(2, 7));

            if (currentBid.quantity >= maxDice)
            {
                if (currentBid.value < 6)
                {
                    var b = new Bid(currentBid.quantity, currentBid.value + 1);
                    var v = DudoRules.IsValidBid(currentBid, b, maxDice);
                    return v.valid ? b : null;
                }
                return null;
            }

            Bid qtyRaise = null;
            Bid faceRaise = null;

            if (currentBid.quantity + 1 <= maxDice)
                qtyRaise = new Bid(currentBid.quantity + 1, currentBid.value);

            if (currentBid.value < 6)
                faceRaise = new Bid(currentBid.quantity, currentBid.value + 1);

            Bid toAces = null;
            if (currentBid.value != 1)
            {
                int aceQty = (currentBid.quantity / 2) + 1;
                aceQty = Math.Min(aceQty, maxDice);
                toAces = new Bid(aceQty, 1);
            }

            Bid fromAces = null;
            if (currentBid.value == 1)
            {
                int nonAceQty = currentBid.quantity * 2 + 1;
                nonAceQty = Math.Min(nonAceQty, maxDice);
                fromAces = new Bid(nonAceQty, _rng.Next(2, 7));
            }

            Bid[] preferenceOrder;
            if (_rng.Value() < raiseQuantityBias)
                preferenceOrder = new Bid[] { qtyRaise, faceRaise, toAces, fromAces };
            else
                preferenceOrder = new Bid[] { faceRaise, qtyRaise, toAces, fromAces };

            foreach (var b in preferenceOrder)
            {
                if (b == null) continue;
                var v = DudoRules.IsValidBid(currentBid, b, maxDice);
                if (v.valid) return b;
            }

            return null;
        }

        private int GetTotalDiceInPlay()
        {
            int total = 0;
            foreach (var p in players) if (!p.eliminated) total += p.diceCount;
            return total;
        }
    }

    // Optional stub; replace with your real ScriptableObject later
    public class EnemyProfile
    {
        public string enemyName;
        public int startingDice = 5;
        public float callDudoBase = 0.15f;
        public float spotOnBase = 0.10f;
        public float raiseQuantityBias = 0.60f;
        public float aggression = 0.50f;
    }
}
