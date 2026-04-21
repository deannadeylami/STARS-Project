using System;
using UnityEngine;

/// <summary>
/// Singleton tracker that waits for SkyMapRenderer, PlanetRender, and ConstellationRenderer
/// to all report completion before firing OnAllRenderersReady.
/// Attach this to any persistent GameObject in SkyScene.
/// </summary>
public class SkySceneReadyTracker : MonoBehaviour
{
    public static SkySceneReadyTracker Instance { get; private set; }

    /// <summary>
    /// Fired each time a single renderer finishes. Passes the renderer key ("Stars", "Planets", "Constellations").
    /// </summary>
    public event Action<string> OnRendererReady;

    /// <summary>
    /// Fired once — when all three renderers have reported ready.
    /// </summary>
    public event Action OnAllRenderersReady;

    private bool _starsReady          = false;
    private bool _planetsReady         = false;
    private bool _constellationsReady  = false;
    private bool _eventFired           = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Called by each renderer when it finishes.
    /// Valid keys: "Stars", "Planets", "Constellations"
    /// </summary>
    public void ReportReady(string rendererKey)
    {
        switch (rendererKey)
        {
            case "Stars":
                _starsReady = true;
                Debug.Log("[SkySceneReadyTracker] Stars ready.");
                break;
            case "Planets":
                _planetsReady = true;
                Debug.Log("[SkySceneReadyTracker] Planets ready.");
                break;
            case "Constellations":
                _constellationsReady = true;
                Debug.Log("[SkySceneReadyTracker] Constellations ready.");
                break;
            default:
                Debug.LogWarning($"[SkySceneReadyTracker] Unknown renderer key: '{rendererKey}'");
                return;
        }

        // Broadcast per-renderer event so LoadingOverlay can update progress
        OnRendererReady?.Invoke(rendererKey);

        CheckAllReady();
    }

    private void CheckAllReady()
    {
        if (_eventFired) return;

        if (_starsReady && _planetsReady && _constellationsReady)
        {
            _eventFired = true;
            Debug.Log("[SkySceneReadyTracker] All renderers ready — firing OnAllRenderersReady.");
            OnAllRenderersReady?.Invoke();
        }
    }
}