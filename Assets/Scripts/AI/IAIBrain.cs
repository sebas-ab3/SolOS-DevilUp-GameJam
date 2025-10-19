// Assets/Scripts/AI/IAIBrain.cs
namespace DudoGame.AI
{
    public interface IAIBrain
    {
        // Return: ("raise" | "call" | "spoton", Bid)  ; Bid is ignored for call/spoton
        (string action, DudoGame.Bid bid) Decide(DudoGame.DudoGameManager gm);
        float NextThinkDelay(); // randomized between profile min/max
    }
}