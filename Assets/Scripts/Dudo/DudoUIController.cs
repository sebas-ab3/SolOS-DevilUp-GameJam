using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DudoGame;
using System.Collections;
using Game.Campaign;
using DudoGame.AI;

public class DudoUIController : MonoBehaviour
{
    [Header("Top Info")] [SerializeField] TMP_Text playerDiceLabel;
    [SerializeField] TMP_Text aiDiceLabel;
    [SerializeField] TMP_Text currentBidLabel;

    [Header("Status")] [SerializeField] TMP_Text statusLabel;

    [Header("Bid Editor")] [SerializeField]
    TMP_Text qtyValue;

    [SerializeField] TMP_Text faceValue;
    [SerializeField] Button qtyMinus;
    [SerializeField] Button qtyPlus;
    [SerializeField] Button faceMinus;
    [SerializeField] Button facePlus;
    [SerializeField] GameObject bidEditorRow;

    [Header("Actions")] [SerializeField] Button placeBidBtn;
    [SerializeField] Button callBtn;
    [SerializeField] Button spotOnBtn;
    [SerializeField] GameObject actionsRow;

    [Header("Footer (debug)")] [SerializeField]
    Button restartBtn;

    [SerializeField] Button closeBtn; // goes back to Intermission

    [Header("Game Setup")] [SerializeField]
    string playerName = "You";

    [SerializeField] int startingDice = 5;
    [SerializeField] int seed = 12345; // for determinism during tests
    [SerializeField] bool autoStartFreeplay = true;

    [Header("Timing (seconds)")] [SerializeField]
    float aiThinkSeconds = 1.0f;

    [SerializeField] float interRoundSeconds = 1.0f;

    [Header("UX / Warnings")] [SerializeField]
    private float warningDisplaySeconds = 2.0f;

    [Header("AI Profile")] [SerializeField]
    private EnemyProfileSO enemyProfileSO;

    [SerializeField] private TMP_Text aiProfileLabel;

    // DudoUIController.cs (top)
    [Header("Dev / Debug")] [SerializeField]
    private bool enableDebugFooter = false;


    private float _thinkMin, _thinkMax; // per-profile think window

    // warnings runtime
    private string _activeWarning = "";
    private string _activeWarningKey = "";
    private float _warningHideAt = -1f;

    // status runtime
    private string _lastStatus = "";

    // --- internal ---
    DudoGameManager gm;
    int bidQty = 1, bidFace = 2;
    bool waitingForAI = false;
    bool gameOver = false;

    // reveal AI dice after resolution explicitly until next round starts
    bool revealAIAfterResolution = false;

    bool isPaused = false; // Pause state

    // Track if match has been started
    private bool _matchStarted = false;
    private Game.Core.GameDirector _dir;
    private bool _subscribed = false;

    // fields
    private bool _subscribedState = false;

    // ---------- Lifecycle ----------
    private void OnEnable()
    {
        WireButtons();
        ApplyDebugFooterVisibility();
        StartCoroutine(AttachWhenReady());
    }

    private IEnumerator AttachWhenReady()
    {
        while (Game.Core.GameDirector.Instance == null)
            yield return null;

        _dir = Game.Core.GameDirector.Instance;

        // in AttachWhenReady() after you get _dir:
        if (!_subscribed && _dir != null)
        {
            _dir.OnMatchActive += OnMatchActive;
            _dir.OnStateChanged += HandleStateChanged; // NEW: reset when leaving Match
            _subscribed = true;
            Debug.Log("[DudoUI] Subscribed to OnMatchActive & OnStateChanged");
        }

        switch (_dir.State)
        {
            case Game.Core.GameState.Freeplay:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (autoStartFreeplay) StartMatch();
                else PaintIdle();
#else
    PaintIdle(); // or even hide this panel in production
#endif
                break;


            case Game.Core.GameState.Match:
                // Do NOT auto-start here. Wait for OnMatchActive after player clicks START.
                Debug.Log("[DudoUI] In Match (pre-activation). Waiting for OnMatchActive…");
                SetControls(false);
                SetRowsVisible(false);
                break;

            default: // Intermission etc.
                Debug.Log("[DudoUI] Not in Match. Idling UI.");
                PaintIdle();
                break;
        }
    }

    private void OnDisable()
    {
        UnwireButtons();
        if (_subscribed && _dir != null)
        {
            _dir.OnMatchActive -= OnMatchActive;
            _dir.OnStateChanged -= HandleStateChanged;
        }
        _subscribed = false;

        // Treat disable as leaving the match – drop stale state
        _matchStarted = false;
        gm = null;
        SetRowsVisible(false);
    }


    // ✅ Always start a fresh match when the start signal arrives and we're not in a live round
    private void OnMatchActive()
    {
        Debug.Log("[DudoUI] OnMatchActive received");
        if (_dir == null || _dir.State != Game.Core.GameState.Match) return;

        if (gm == null || gameOver || !_matchStarted || !gm.roundActive)
        {
            StartMatch();   // ← will show rows and enable buttons on player's first turn
        }
    }


    private void Update()
    {
        TickWarning();
    }

    // ---------- Wiring ----------
    private void ApplyDebugFooterVisibility()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool on = enableDebugFooter;
#else
    bool on = false;
#endif
        if (restartBtn) restartBtn.gameObject.SetActive(on);
        if (closeBtn) closeBtn.gameObject.SetActive(on);
    }


    private void WireButtons()
    {
        UnwireButtons();

        // normal gameplay buttons
        if (qtyMinus) qtyMinus.onClick.AddListener(OnQtyMinus);
        if (qtyPlus) qtyPlus.onClick.AddListener(OnQtyPlus);
        if (faceMinus) faceMinus.onClick.AddListener(OnFaceMinus);
        if (facePlus) facePlus.onClick.AddListener(OnFacePlus);

        if (placeBidBtn) placeBidBtn.onClick.AddListener(OnPlaceBid);
        if (callBtn) callBtn.onClick.AddListener(OnCall);
        if (spotOnBtn) spotOnBtn.onClick.AddListener(OnSpotOn);

        // dev-only wiring
        if (enableDebugFooter)
        {
            if (restartBtn) restartBtn.onClick.AddListener(StartFreeplayMatch);
            if (closeBtn) closeBtn.onClick.AddListener(CloseUIToIntermission);
        }
    }

    private void UnwireButtons()
    {
        if (qtyMinus) qtyMinus.onClick.RemoveListener(OnQtyMinus);
        if (qtyPlus) qtyPlus.onClick.RemoveListener(OnQtyPlus);
        if (faceMinus) faceMinus.onClick.RemoveListener(OnFaceMinus);
        if (facePlus) facePlus.onClick.RemoveListener(OnFacePlus);

        if (placeBidBtn) placeBidBtn.onClick.RemoveListener(OnPlaceBid);
        if (callBtn) callBtn.onClick.RemoveListener(OnCall);
        if (spotOnBtn) spotOnBtn.onClick.RemoveListener(OnSpotOn);

        if (restartBtn) restartBtn.onClick.RemoveListener(StartFreeplayMatch);
        if (closeBtn) closeBtn.onClick.RemoveListener(CloseUIToIntermission);
    }

    // ---------- Bid Editor handlers ----------
    private void OnQtyMinus()
    {
        int desired = bidQty - 1;
        TryApplyBidEditor(desired, bidFace, "qty_dec");
    }

    private void OnQtyPlus()
    {
        int desired = bidQty + 1;
        TryApplyBidEditor(desired, bidFace, "qty_inc");
    }

    private void OnFaceMinus()
    {
        int desired = Mathf.Max(1, bidFace - 1);
        TryApplyBidEditor(bidQty, desired, "face_dec");
    }

    private void OnFacePlus()
    {
        int desired = Mathf.Min(6, bidFace + 1);
        TryApplyBidEditor(bidQty, desired, "face_inc");
    }

    private void TryApplyBidEditor(int proposedQty, int proposedFace, string warnKey)
    {
        proposedQty = Mathf.Clamp(proposedQty, 1, GetTableMax());
        proposedFace = Mathf.Clamp(proposedFace, 1, 6);

        var proposed = new Bid(proposedQty, proposedFace);
        var v = DudoRules.IsValidBid(gm.currentBid, proposed, GetTableMax());

        if (!v.valid)
        {
            ShowWarning(v.reason, $"{warnKey}:{v.reason}");
            RefreshBidEditor();
            return;
        }

        bidQty = proposedQty;
        bidFace = proposedFace;
        RefreshBidEditor();
    }

    // ---------- Match lifecycle ----------
    public void StartMatch()
{
    // 0) Kill any leftover coroutines (AI think, round delays, etc.)
    StopAllCoroutines();

    // 1) Fresh local flags & warning/status UI
    _matchStarted             = true;
    gameOver                  = false;
    isPaused                  = false;
    waitingForAI              = false;
    revealAIAfterResolution   = false;

    _activeWarning            = "";
    _activeWarningKey         = "";
    _warningHideAt            = -1f;
    _lastStatus               = "";
    PaintStatus(""); // clear any “You wins” carryover

    // 2) New game manager each match (never reuse an old gm)
    gm = new DudoGameManager(playerName, startingDice, new SystemRng(seed));

    // 3) Pull enemy profile from campaign (if running), else keep inspector one
    var cm = CampaignManager.Instance;
    if (cm && cm.CurrentEnemy != null)
        enemyProfileSO = cm.CurrentEnemy;

    // 4) Apply profile to GM + AI tempo + starting dice
    if (enemyProfileSO != null)
    {
        var runtimeProfile = ToRuntimeProfile(enemyProfileSO);
        gm.SetEnemyProfile(runtimeProfile);

        // AI starting dice (before rolling)
        if (gm.players != null && gm.players.Count > 1 && !gm.players[1].eliminated)
            gm.players[1].diceCount = Mathf.Max(1, enemyProfileSO.startingDice);

        // Think-time window for AI (fallback to inspector if not set)
        _thinkMin = Mathf.Min(enemyProfileSO.thinkTimeMin, enemyProfileSO.thinkTimeMax);
        _thinkMax = Mathf.Max(enemyProfileSO.thinkTimeMin, enemyProfileSO.thinkTimeMax);
    }
    else
    {
        gm.SetEnemyProfile(null);
        _thinkMin = _thinkMax = 0f; // use aiThinkSeconds in AITurn()
    }

    // 5) Player starts the first round of the match
    gm.currentPlayerIndex = 0;
    gm.StartNewRound(); // sets roundActive = true and rolls dice

    // 6) Prime the bid editor and paint top info
    SetDefaultBidFromCurrent();
    PaintProfileLabel();

    // Make gameplay rows visible now; interactivity is gated next
    SetRowsVisible(true);

    // 7) First UI paint + interactivity gate (should enable player controls)
    RefreshAll("New round. Your turn.");
    RefreshInteractable();

    // 8) Safety log so we can see the state on entry
    Debug.Log(
        $"[DudoUI] StartMatch end: rows bid={bidEditorRow?.activeSelf} act={actionsRow?.activeSelf} " +
        $"btns place={placeBidBtn?.interactable} call={callBtn?.interactable} spot={spotOnBtn?.interactable}"
    );
}


    // Legacy dev button
    public void StartFreeplayMatch()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StartMatch();
#endif
    }

    void CloseUIToIntermission()
    {
        _matchStarted = false;
        var dir = Game.Core.GameDirector.Instance;
        if (dir != null) dir.SetState(Game.Core.GameState.Intermission);
        else gameObject.SetActive(false);
    }

    // ---------- Actions ----------
    void OnPlaceBid()
    {
        var proposed = new Bid(bidQty, bidFace);
        var v = DudoRules.IsValidBid(gm.currentBid, proposed, GetTableMax());
        if (!v.valid)
        {
            ShowWarning("Invalid: " + v.reason, v.reason);
            return;
        }

        gm.MakeBid(proposed.quantity, proposed.value);
        RefreshAll("You bid " + proposed + ". AI thinking...");

        if (gm.players[gm.currentPlayerIndex].isAI) StartCoroutine(AITurn());
    }

    void OnCall()
    {
        if (gm.currentBid == null)
        {
            ShowWarning("No bid to call.", "no_bid_call");
            return;
        }

        gm.CallBid();
        revealAIAfterResolution = true;
        RefreshAfterResolution();
        if (!gm.IsGameOver() && !gm.roundActive) StartCoroutine(StartNextRound());
    }

    void OnSpotOn()
    {
        if (gm.currentBid == null)
        {
            ShowWarning("No bid to call exact.", "no_bid_spoton");
            return;
        }

        gm.SpotOn();
        revealAIAfterResolution = true;
        RefreshAfterResolution();
        if (!gm.IsGameOver() && !gm.roundActive) StartCoroutine(StartNextRound());
    }

    // ---------- AI turn & rounds ----------
    IEnumerator AITurn()
    {
        waitingForAI = true;
        RefreshInteractable();

        float delay = aiThinkSeconds; // inspector fallback
        if (_thinkMax > 0f)
            delay = UnityEngine.Random.Range(_thinkMin, _thinkMax);

        yield return new WaitForSeconds(Mathf.Max(0f, delay));

        var (action, bid) = gm.GetAIDecision();

        if (action == "raise")
        {
            gm.MakeBid(bid.quantity, bid.value);
            waitingForAI = false;
            SetDefaultBidFromCurrent();
            RefreshAll("AI bids " + bid + ". Your turn.");
        }
        else if (action == "call")
        {
            gm.CallBid();
            waitingForAI = false;
            revealAIAfterResolution = true;
            RefreshAfterResolution();
            if (!gm.IsGameOver() && !gm.roundActive) StartCoroutine(StartNextRound());
        }
        else // spot on
        {
            gm.SpotOn();
            waitingForAI = false;
            revealAIAfterResolution = true;
            RefreshAfterResolution();
            if (!gm.IsGameOver() && !gm.roundActive) StartCoroutine(StartNextRound());
        }
    }

    IEnumerator StartNextRound()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, interRoundSeconds));

        revealAIAfterResolution = false;
        gm.StartNewRound();

        SetDefaultBidFromCurrent();
        RefreshAll("New round. " + (gm.currentPlayerIndex == 0 ? "Your turn." : "AI's turn."));

        if (gm.players[gm.currentPlayerIndex].isAI) StartCoroutine(AITurn());
    }

    private DudoGame.EnemyProfile ToRuntimeProfile(EnemyProfileSO so)
    {
        if (so == null) return null;
        return new DudoGame.EnemyProfile
        {
            enemyName = so.displayName,
            startingDice = so.startingDice,
            callDudoBase = so.callDudoBase,
            spotOnBase = so.spotOnBase,
            raiseQuantityBias = so.raiseQuantityBias,
            aggression = so.aggression
        };
    }

    // ---------- UI refresh ----------
    void RefreshAfterResolution()
    {
        if (gm.IsGameOver())
        {
            var winner = gm.GetWinner();
            gameOver = true;
            revealAIAfterResolution = true;

            // Paint final status once (optional)
            _lastStatus = $"🏆 {winner.playerName} wins!";
            RefreshAll(_lastStatus);

            // Freeze inputs & rows
            SetControls(false);
            SetRowsVisible(false);

            // Hand off to flow controller
            var dir = Game.Core.GameDirector.Instance;
            if (dir != null)
            {
                bool playerWon = (winner != null && !gm.players[0].eliminated);
                _matchStarted = false; // reset local guard
                dir.EndMatch(playerWon); // ← CampaignManager will react and route properly
            }

            return;
        }

        var top = string.Join("\n", gm.gameLog.GetRange(0, Mathf.Min(3, gm.gameLog.Count)));
        _lastStatus = top;

        RefreshAll(_lastStatus);

        if (!gm.roundActive) SetControls(false);
    }


    void RefreshAll(string status)
    {
        _lastStatus = status ?? "";

        if (playerDiceLabel) playerDiceLabel.text = "Your dice:   " + FormatDice(gm.players[0].dice, false);
        if (aiDiceLabel)
        {
            bool hideAI = gm.roundActive && !revealAIAfterResolution;
            aiDiceLabel.text = "AI dice:     " + FormatDice(gm.players[1].dice, hideAI);
        }

        if (currentBidLabel)
            currentBidLabel.text = gm.currentBid != null ? "Current bid: " + gm.currentBid : "Current bid: —";

        PaintStatus(_lastStatus);

        RefreshInteractable();
        RefreshBidEditor();
    }

    void RefreshInteractable()
    {
        if (gm == null || !gm.roundActive)
        {
            SetControls(false);
            SetRowsVisible(false);
            return;
        }

        bool isPlayerTurn = (gm.currentPlayerIndex == 0)
                            && gm.roundActive
                            && !waitingForAI
                            && !gameOver
                            && !isPaused;

        bool hasBid = (gm.currentBid != null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DudoUI] RefreshInteractable: isPlayerTurn={isPlayerTurn} idx={gm.currentPlayerIndex} " +
                  $"roundActive={gm.roundActive} waitingForAI={waitingForAI} gameOver={gameOver} isPaused={isPaused} " +
                  $"hasBid={hasBid}");
#endif

        // Rows stay visible during gameplay; only interactivity changes
        SetRowsVisible(true);

        if (placeBidBtn) placeBidBtn.interactable = isPlayerTurn;
        if (callBtn) callBtn.interactable = isPlayerTurn && hasBid;
        if (spotOnBtn) spotOnBtn.interactable = isPlayerTurn && hasBid;

        bool stepperEnabled = isPlayerTurn;
        if (qtyMinus) qtyMinus.interactable = stepperEnabled;
        if (qtyPlus) qtyPlus.interactable = stepperEnabled;
        if (faceMinus) faceMinus.interactable = stepperEnabled;
        if (facePlus) facePlus.interactable = stepperEnabled;
    }

    // Helper: show/hide the two rows together
    void SetRowsVisible(bool show)
    {
        if (bidEditorRow) bidEditorRow.SetActive(show);
        if (actionsRow) actionsRow.SetActive(show);
    }

    public void PauseGame()
    {
        isPaused = true;
        RefreshInteractable();
    }

    public void ResumeGame()
    {
        isPaused = false;
        RefreshInteractable();

        if (gm != null && gm.players[gm.currentPlayerIndex].isAI && gm.roundActive && !waitingForAI)
        {
            StartCoroutine(AITurn());
        }
    }

    void SetControls(bool on)
    {
        if (placeBidBtn) placeBidBtn.interactable = on;
        if (callBtn) callBtn.interactable = on;
        if (spotOnBtn) spotOnBtn.interactable = on;
        if (qtyMinus) qtyMinus.interactable = on;
        if (qtyPlus) qtyPlus.interactable = on;
        if (faceMinus) faceMinus.interactable = on;
        if (facePlus) facePlus.interactable = on;
    }

    void RefreshBidEditor()
    {
        bidQty = Mathf.Clamp(bidQty, 1, GetTableMax());
        bidFace = Mathf.Clamp(bidFace, 1, 6);

        if (gm.currentBid == null && bidFace == 1) bidFace = 2;

        if (qtyValue) qtyValue.text = bidQty.ToString();
        if (faceValue) faceValue.text = (bidFace == 1) ? "A" : bidFace.ToString();
    }

    private void SetDefaultBidFromCurrent()
    {
        int max = GetTableMax();
        Bid next = DudoRules.NextRaise(gm.currentBid, max);
        if (next != null)
        {
            bidQty = next.quantity;
            bidFace = next.value;
        }
        else
        {
            bidQty = Mathf.Clamp(bidQty, 1, max);
            if (gm.currentBid == null && bidFace == 1) bidFace = 2;
        }

        RefreshBidEditor();
    }

    // ---------- Warning system ----------
    private void ShowWarning(string message, string key = null)
    {
        key ??= message ?? "";
        if (!string.IsNullOrEmpty(_activeWarning) && key == _activeWarningKey)
            return;

        _activeWarning = message ?? "";
        _activeWarningKey = key;
        _warningHideAt = Time.time + Mathf.Max(0.1f, warningDisplaySeconds);

        PaintStatus(_lastStatus);
    }

    private void TickWarning()
    {
        if (string.IsNullOrEmpty(_activeWarning)) return;
        if (Time.time < _warningHideAt) return;

        _activeWarning = "";
        _activeWarningKey = "";
        _warningHideAt = -1f;

        PaintStatus(_lastStatus);
    }

    private void PaintStatus(string baseStatus)
    {
        if (!statusLabel) return;

        if (!string.IsNullOrEmpty(_activeWarning))
            statusLabel.text = $"<color=red>{_activeWarning}</color>\n\n{baseStatus ?? ""}";
        else
            statusLabel.text = baseStatus ?? "";
    }

    // ---------- Helpers ----------
    int GetTableMax()
    {
        int total = 0;
        foreach (var p in gm.players)
            if (!p.eliminated)
                total += p.diceCount;
        return Mathf.Max(1, total);
    }

    static string FormatDice(System.Collections.Generic.List<int> dice, bool hidden)
    {
        if (dice == null || dice.Count == 0) return "—";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < dice.Count; i++)
        {
            sb.Append(hidden ? '?' : (dice[i] == 1 ? 'A' : (char)('0' + dice[i])));
            if (i < dice.Count - 1) sb.Append(' ');
        }

        return sb.ToString();
    }

    void PaintIdle()
    {
        if (playerDiceLabel) playerDiceLabel.text = "Your dice: —";
        if (aiDiceLabel) aiDiceLabel.text = "AI dice: —";
        if (currentBidLabel) currentBidLabel.text = "Current bid: —";
        _lastStatus = "Freeplay: press Start";
        PaintStatus(_lastStatus);
        SetControls(false);
        SetRowsVisible(false);
    }

    private void PaintProfileLabel()
    {
        if (!aiProfileLabel) return;
        aiProfileLabel.text = enemyProfileSO
            ? $"AI: {enemyProfileSO.displayName}"
            : "AI: Default";
    }

    // handler
    private void HandleStateChanged(Game.Core.GameState from, Game.Core.GameState to)
    {
        if (to != Game.Core.GameState.Match)
        {
            // hard reset local gameplay state so next match is fresh
            _matchStarted = false;
            waitingForAI = false;
            gameOver = false;
            isPaused = false;
            revealAIAfterResolution = false;

            gm = null; // drop the old manager

            // hide / disable rows until next activation
            SetRowsVisible(false);
            SetControls(false);
            PaintStatus(""); // clear lingering "You wins" text
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void AssertUIRefs()
        {
            if (!bidEditorRow) Debug.LogError("[DudoUI] bidEditorRow is NOT assigned in Inspector");
            if (!actionsRow) Debug.LogError("[DudoUI] actionsRow is NOT assigned in Inspector");
            if (!placeBidBtn) Debug.LogError("[DudoUI] placeBidBtn is NOT assigned");
            if (!callBtn) Debug.LogError("[DudoUI] callBtn is NOT assigned");
            if (!spotOnBtn) Debug.LogError("[DudoUI] spotOnBtn is NOT assigned");
        }
#endif
    }
}