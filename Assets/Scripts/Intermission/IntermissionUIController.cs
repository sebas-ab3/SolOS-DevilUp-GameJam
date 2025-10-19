using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Core;

public class IntermissionUIController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text bodyText;
    [SerializeField] TMP_Text stepCounter;
    [SerializeField] Button nextBtn;

    [Header("Script (simple for now)")]
    [TextArea(2, 4)]
    [SerializeField] string[] steps = new[]
    {
        "You steady your nerves. The dice clatter in the distance.",
        "Eyes left, eyes right — keep the shadows at bay.",
        "Time to face the next opponent."
    };

    [Header("Routing after completion")]
    [SerializeField] NextTarget after = NextTarget.Freeplay;   // for Step 3 testing
    [SerializeField] bool autoStartOnEnable = true;            // show step 0 when panel appears

    int index;

    public enum NextTarget { Freeplay, Match, Tutorial, CampaignComplete }

    void OnEnable()
    {
        // Ask CampaignManager for the correct lines
        var cm = Game.Campaign.CampaignManager.Instance;
        if (cm != null)
        {
            var lines = cm.GetCurrentIntermissionLines();
            StartSequence(lines);
        }

        if (nextBtn) nextBtn.onClick.AddListener(OnNext);
    }



    void OnDisable()
    {
        if (nextBtn) nextBtn.onClick.RemoveListener(OnNext);
    }

    public void StartSequence(string[] customSteps = null, NextTarget? overrideNext = null)
    {
        if (customSteps != null && customSteps.Length > 0) steps = customSteps;
        if (overrideNext.HasValue) after = overrideNext.Value;
        index = 0;
        Paint();
    }

    void OnNext()
    {
        // Optional: you can gate specific steps with extra requirements later:
        // if (index == 1 && !SomeCondition) { Flash("Clear a shadow first!"); return; }

        index++;
        if (index >= steps.Length)
        {
            GoNextState();
            return;
        }
        Paint();
    }

    void Paint()
    {
        if (steps == null || steps.Length == 0)
        {
            if (bodyText) bodyText.text = "Intermission";
            if (stepCounter) stepCounter.text = "";
            if (nextBtn) nextBtn.GetComponentInChildren<TMP_Text>().text = "Start";
            return;
        }

        if (bodyText) bodyText.text = steps[index];
        if (stepCounter) stepCounter.text = $"Step {index + 1} / {steps.Length}";

        var isLast = (index == steps.Length - 1);
        var label = isLast ? "Start" : "Next";
        var labelText = nextBtn ? nextBtn.GetComponentInChildren<TMP_Text>() : null;
        if (labelText) labelText.text = label;
    }

    void GoNextState()
    {
        var dir = GameDirector.Instance;
        if (dir == null)
        {
            Debug.LogWarning("[IntermissionUI] No GameDirector in scene; hiding panel.");
            gameObject.SetActive(false);
            return;
        }

        if (after == NextTarget.Match)
        {
            var cm = Game.Campaign.CampaignManager.Instance;
            if (cm != null)
            {
                cm.FinishIntermission();   // ⬅️ use this name (or keep your call and add a forwarder)
                return;
            }
        }

        // Fallbacks (only used if campaign manager isn’t present)
        switch (after)
        {
            case NextTarget.Freeplay:         dir.SetState(GameState.Freeplay); break;
            case NextTarget.Match:            dir.SetState(GameState.Match); break;
            case NextTarget.Tutorial:         dir.SetState(GameState.Tutorial); break;
            case NextTarget.CampaignComplete: dir.SetState(GameState.CampaignComplete); break;
        }
    }


    // Helper for future mini-gates
    void Flash(string msg)
    {
        if (bodyText) bodyText.text = $"<color=yellow>{msg}</color>\n\n{bodyText.text}";
    }
}
