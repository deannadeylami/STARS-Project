using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full-screen dark overlay with a circular progress bar and loading label.
/// Fades out once SkySceneReadyTracker reports all renderers are done.
/// 
[RequireComponent(typeof(Canvas))]
public class LoadingOverlay : MonoBehaviour
{
    [Header("Overlay References")]
    [Tooltip("The full-screen background Image.")]
    public Image overlayImage;

    [Tooltip("The filled radial Image used as the progress arc.")]
    public Image progressRing;

    [Tooltip("The label showing what is currently loading.")]
    public TMP_Text loadingLabel;

    [Header("Fade Settings")]
    [Tooltip("Duration of the fade-out in seconds once all renderers are ready.")]
    public float fadeDuration = 1f;

    [Header("Progress Animation")]
    [Tooltip("How quickly the ring animates toward the target fill amount.")]
    public float fillAnimSpeed = 3f;

    [Header("Label Timing")]
    [Tooltip("Minimum time in seconds each loading label is shown, even if the renderer finishes faster.")]
    public float minLabelDisplayTime = 0.4f;

    // Each renderer is worth 1/3 of the total
    private const float StepSize = 3f / 4f;

    private float _targetFill  = 0f;
    private float _currentFill = 0f;

    // Queue of pending label changes — each entry is the label text to show next
    private Queue<string> _labelQueue   = new Queue<string>();
    private bool          _labelBusy    = false;
    private bool          _allReady     = false;

    private void Start()
    {
        if (overlayImage == null || progressRing == null || loadingLabel == null)
        {
            Debug.LogError("[LoadingOverlay] One or more references are not assigned in the Inspector!");
            return;
        }

        // Start fully opaque, ring empty
        SetOverlayAlpha(1f);
        progressRing.fillAmount = 0f;
        _currentFill            = 0f;
        _targetFill             = 0f;

        // Show the first label immediately — stars are loading from frame one
        loadingLabel.text = "Loading Stars...";

        if (SkySceneReadyTracker.Instance != null)
        {
            SkySceneReadyTracker.Instance.OnRendererReady     += OnRendererReady;
            SkySceneReadyTracker.Instance.OnAllRenderersReady += OnAllReady;
        }
        else
        {
            Debug.LogError("[LoadingOverlay] SkySceneReadyTracker instance not found!");
        }
    }

    private void OnDestroy()
    {
        if (SkySceneReadyTracker.Instance != null)
        {
            SkySceneReadyTracker.Instance.OnRendererReady     -= OnRendererReady;
            SkySceneReadyTracker.Instance.OnAllRenderersReady -= OnAllReady;
        }
    }

    private void Update()
    {
        // Smoothly animate the ring fill toward the target
        if (!Mathf.Approximately(_currentFill, _targetFill))
        {
            _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillAnimSpeed * Time.deltaTime);
            progressRing.fillAmount = _currentFill;
        }
    }

    private void OnRendererReady(string rendererKey)
    {
        // Advance the ring by one step immediately
        _targetFill = Mathf.Min(_targetFill + StepSize, 1f);

        // Queue the next label — it will be displayed for at least minLabelDisplayTime
        // regardless of how fast the next renderer finishes
        switch (rendererKey)
        {
            case "Stars":
                EnqueueLabel("Loading Planets...");
                break;
            case "Planets":
                EnqueueLabel("Loading Constellations...");
                break;
            // "Constellations" — no label queued here; "Complete!" is set in FadeOut
        }
    }

    private void OnAllReady()
    {
        _allReady = true;
        // Don't start FadeOut immediately — let the label queue drain first
        StartCoroutine(WaitForLabelsAndFade());
    }

    /// <summary>
    /// Adds a label to the queue and starts the display coroutine if not already running.
    /// </summary>
    private void EnqueueLabel(string text)
    {
        _labelQueue.Enqueue(text);
        if (!_labelBusy)
            StartCoroutine(DrainLabelQueue());
    }

    /// <summary>
    /// Processes each queued label, holding it on screen for minLabelDisplayTime.
    /// </summary>
    private IEnumerator DrainLabelQueue()
    {
        _labelBusy = true;

        while (_labelQueue.Count > 0)
        {
            string next = _labelQueue.Dequeue();
            loadingLabel.text = next;
            yield return new WaitForSeconds(minLabelDisplayTime);
        }

        _labelBusy = false;
    }

    /// <summary>
    /// Waits for the label queue to finish displaying, then triggers the fade.
    /// </summary>
    private IEnumerator WaitForLabelsAndFade()
    {
        // Wait until the label queue has fully drained
        yield return new WaitUntil(() => !_labelBusy && _labelQueue.Count == 0);

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        // Wait for the ring to finish animating to 100%
        while (!Mathf.Approximately(_currentFill, 1f))
        {
            _currentFill = Mathf.MoveTowards(_currentFill, 1f, fillAnimSpeed * Time.deltaTime);
            progressRing.fillAmount = _currentFill;
            yield return null;
        }
        progressRing.fillAmount = 1f;

        // Ring is visually full — now safe to show Complete
        loadingLabel.text = "Complete!";

        // Brief pause so the user sees 100% before the fade
        yield return new WaitForSeconds(0.3f);

        // Fade out the entire overlay
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            SetOverlayAlpha(Mathf.Lerp(1f, 0f, t));
            SetRingAlpha(Mathf.Lerp(1f, 0f, t));
            SetLabelAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        gameObject.SetActive(false);
    }

    // --- Alpha helpers ---

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage == null) return;
        Color c = overlayImage.color;
        c.a = alpha;
        overlayImage.color = c;
    }

    private void SetRingAlpha(float alpha)
    {
        if (progressRing == null) return;
        Color c = progressRing.color;
        c.a = alpha;
        progressRing.color = c;
    }

    private void SetLabelAlpha(float alpha)
    {
        if (loadingLabel == null) return;
        Color c = loadingLabel.color;
        c.a = alpha;
        loadingLabel.color = c;
    }
}