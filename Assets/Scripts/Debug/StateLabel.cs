// Assets/Scripts/UI/StateLabel.cs
using UnityEngine;
using TMPro;
using Game.Core;
using System.Collections;

[RequireComponent(typeof(TMP_Text))]
public class StateLabel : MonoBehaviour
{
    private TMP_Text _label;
    private GameDirector _dir;
    private GameState _lastPainted;

    private void Awake()
    {
        _label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        // In case this label is under a panel that gets toggled by HUDStateBinder,
        // consider moving it under a dedicated "DevOverlay" object that stays on.
        StartCoroutine(AttachWhenReady());
    }

    private void OnDisable()
    {
        if (_dir != null) _dir.OnStateChanged -= HandleStateChanged;
    }

    private IEnumerator AttachWhenReady()
    {
        // Wait until GameDirector.Instance exists (handles script execution order)
        while ((_dir = GameDirector.Instance) == null)
            yield return null;

        _dir.OnStateChanged += HandleStateChanged;

        // Initial paint even if no event yet
        Paint(_dir.State);
    }

    private void HandleStateChanged(GameState from, GameState to)
    {
        Paint(to);
    }

    private void Update()
    {
        // Safety net: if state changed but we somehow missed the event, repaint.
        if (_dir != null && _dir.State != _lastPainted)
            Paint(_dir.State);
    }

    private void Paint(GameState s)
    {
        _lastPainted = s;
        if (_label) _label.text = $"STATE: {s}";
    }
}