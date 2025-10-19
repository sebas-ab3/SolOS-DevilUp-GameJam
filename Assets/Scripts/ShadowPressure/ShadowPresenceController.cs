using UnityEngine;
using Game.Gameplay; // wherever ShadowTimerManager lives

/// <summary>
/// Moves a shadow object from FarPoint -> NearPoint based on ShadowTimerManager progress.
/// Progress 0 = far (out of view), 1 = near (in your face).
/// </summary>
public class ShadowPresenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShadowTimerManager timer; // assign in Inspector
    [SerializeField] private Transform farPoint;       // empty at back of room
    [SerializeField] private Transform nearPoint;      // empty near camera/front

    [Header("Motion")]
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float followLerpSpeed = 8f;   // how quickly we follow target progress
    [SerializeField] private bool faceMainCamera = true;   // optional: billboard toward player

    private float _smoothedT = 0f;

    private void OnEnable()
    {
        // Optional: snap to far on enable
        _smoothedT = 0f;

        if (timer != null)
        {
            // Optional event hookups (nice for snappy push-back on clear)
            timer.OnShadowSpawned += HandleSpawned;
            timer.OnShadowCleared += HandleCleared;
            timer.OnShadowCaught  += HandleCaught;
        }
    }

    private void OnDisable()
    {
        if (timer != null)
        {
            timer.OnShadowSpawned -= HandleSpawned;
            timer.OnShadowCleared -= HandleCleared;
            timer.OnShadowCaught  -= HandleCaught;
        }
    }

    private void LateUpdate()
    {
        if (timer == null || farPoint == null || nearPoint == null)
            return;

        // Target progress (polling is fine; events are just a bonus)
        float targetT = timer.ShadowActive ? Mathf.Clamp01(timer.ShadowProgress01) : 0f;

        // Smooth towards target
        _smoothedT = Mathf.MoveTowards(_smoothedT, targetT, Time.deltaTime * followLerpSpeed);

        // Eased interpolation
        float eased = travelCurve.Evaluate(_smoothedT);
        transform.position = Vector3.Lerp(farPoint.position, nearPoint.position, eased);

        if (faceMainCamera && Camera.main)
        {
            Vector3 lookPos = Camera.main.transform.position;
            lookPos.y = transform.position.y; // keep upright
            transform.LookAt(lookPos);
        }
    }

    // Optional snappier behavior with events (instant push-back on clear)
    private void HandleSpawned()
    {
        // Ensure we start from far (or keep current)
        if (!timer.ShadowActive) _smoothedT = 0f;
    }

    private void HandleCleared()
    {
        // Snap back quickly when the player looks left/right
        _smoothedT = 0f;
        transform.position = farPoint.position;
    }

    private void HandleCaught()
    {
        // Optionally snap to near
        _smoothedT = 1f;
        transform.position = nearPoint.position;
    }
}
