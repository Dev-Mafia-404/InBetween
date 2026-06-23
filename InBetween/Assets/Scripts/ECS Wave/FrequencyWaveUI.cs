using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws an animated sine-ish wave directly as a UI mesh (no camera/RenderTexture needed).
/// Supports three visual states: Flatline (inert, pre-start), Haywire (erratic, value
/// jittering), and Locked (steady oscillation at a fixed captured value).
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class FrequencyWaveUI : MaskableGraphic
{
    public enum WaveState { Flatline, Tracking, Haywire, Locked }

    [Header("Wave Shape")]
    public float value;
    [Tooltip("How much the current value (normalized 0-1 within range) boosts wiggle density on top of baseFrequency")]
    public float frequencyValueInfluence = 0.04f;
    public float lineWidth = 4f;
    [Range(8, 200)] public int resolution = 80;
    [Tooltip("0 = pure sine wave. Higher = a second, busier harmonic layered on top for a richer, less uniform silhouette.")]
    [Range(0f, 0.6f)] public float harmonicMix = 0.3f;

    [Header("Haywire Jitter (applied on top of base shape while state == Haywire)")]
    public float haywireNoiseSpeed = 8f;

    [Header("Color Grading")]
    public Color neutralColor = new Color(0.4f, 0.85f, 1f);
    public Color greenColor = new Color(0.3f, 1f, 0.4f);
    public Color yellowColor = new Color(1f, 0.85f, 0.2f);
    public Color redColor = new Color(1f, 0.3f, 0.3f);
    [Tooltip("Seconds to tween into the graded color after a match result")]
    public float colorTweenDuration = 0.5f;

    // These are ALWAYS set per-frame-of-ghost-switch from the active ghost's
    // GhostMatchDifficulty (via ApplyGhostTuning below) — not exposed as public
    // Inspector fields because editing them directly here would have no lasting
    // effect, it'd just get overwritten on the next ghost selection.
    float baseFrequency = 3f;
    float scrollSpeed = 4f;
    float haywireIntensity = 0.8f;
    float valueRangeMin = 0f;
    float valueRangeMax = 100f;

    public WaveState State { get; private set; } = WaveState.Flatline;

    float phaseAccum;
    float noiseSeedA, noiseSeedB;
    Color targetColor;
    Color currentColor;
    float colorTweenT = 1f;

    /// <summary>Push per-ghost wave tuning in one call — replaces setting baseFrequency/scrollSpeed/haywireIntensity/range individually.</summary>
    public void ApplyGhostTuning(GhostMatchDifficulty difficulty)
    {
        if (difficulty == null) return;
        baseFrequency = difficulty.waveBaseFrequency;
        scrollSpeed = difficulty.waveScrollSpeed;
        haywireIntensity = difficulty.targetHaywireIntensity;
        valueRangeMin = difficulty.minFrequency;
        valueRangeMax = difficulty.maxFrequency;
    }

    protected override void Awake()
    {
        base.Awake();
        currentColor = neutralColor;
        targetColor = neutralColor;
        color = neutralColor;
        noiseSeedA = Random.Range(0f, 1000f);
        noiseSeedB = Random.Range(0f, 1000f);
    }

    /// <summary>Reset to a flat, inert line (e.g. player wave before target is captured). Color is left as-is — call SetGradeColor separately if you need a specific tint.</summary>
    public void SetFlatline()
    {
        State = WaveState.Flatline;
    }

    /// <summary>Smooth distance-tracked motion, no jitter — used while approaching but not yet in close range.</summary>
    public void SetTracking()
    {
        State = WaveState.Tracking;
        SetColorInstant(neutralColor);
    }

    /// <summary>Begin erratic haywire animation. Feed live values via SetLiveValue each frame.</summary>
    public void SetHaywire()
    {
        State = WaveState.Haywire;
        SetColorInstant(neutralColor);
    }

    /// <summary>Lock at the current value — keeps oscillating steadily, stops tracking new input.</summary>
    public void SetLocked()
    {
        State = WaveState.Locked;
    }

    /// <summary>Feed this every frame while State == Haywire or Tracking.</summary>
    public void SetLiveValue(float v)
    {
        if (State != WaveState.Haywire && State != WaveState.Tracking) return;
        value = v;
    }

    /// <summary>
    /// Directly set the value regardless of state — used for the scroll-wheel-controlled
    /// player wave, which sits in Locked (steady, non-jittery) styling but still needs its
    /// underlying value updated live as the player scrolls.
    /// </summary>
    public void SetValueDirect(float v)
    {
        value = Mathf.Clamp(v, valueRangeMin, valueRangeMax);
    }

    /// <summary>Smoothly tween color toward a grade color (e.g. live hint or final result).</summary>
    public void SetGradeColor(Color c, bool instant = false)
    {
        if (instant) { SetColorInstant(c); return; }
        targetColor = c;
        colorTweenT = 0f;
    }

    void SetColorInstant(Color c)
    {
        currentColor = c;
        targetColor = c;
        colorTweenT = 1f;
        color = c;
    }

    void Update()
    {
        if (State != WaveState.Flatline)
            phaseAccum += Time.deltaTime * scrollSpeed;

        if (colorTweenT < 1f)
        {
            colorTweenT = Mathf.MoveTowards(colorTweenT, 1f, Time.deltaTime / Mathf.Max(0.001f, colorTweenDuration));
            currentColor = Color.Lerp(currentColor, targetColor, colorTweenT);
            color = currentColor;
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        float width = r.width;

        if (State == WaveState.Flatline)
        {
            // Perfectly flat line across the middle.
            Vector2 a = new Vector2(-width * 0.5f, 0f);
            Vector2 b = new Vector2(width * 0.5f, 0f);
            AddQuad(vh, a, b, lineWidth, color);
            return;
        }

        float normalized = valueRangeMax > valueRangeMin
            ? Mathf.InverseLerp(valueRangeMin, valueRangeMax, value)
            : 0f;

        float amp = Mathf.Lerp(r.height * 0.08f, r.height * 0.45f, normalized);
        float freq = baseFrequency + normalized * frequencyValueInfluence * 100f;

        // While haywire, layer extra noise onto amplitude AND frequency so the
        // wave itself looks unstable, not just the number ticking underneath it.
        float ampJitter = 1f;
        float freqJitter = 1f;
        if (State == WaveState.Haywire)
        {
            float n1 = Mathf.PerlinNoise(noiseSeedA, Time.time * haywireNoiseSpeed);
            float n2 = Mathf.PerlinNoise(noiseSeedB, Time.time * haywireNoiseSpeed * 1.3f);
            ampJitter = 1f + (n1 - 0.5f) * 2f * haywireIntensity;
            freqJitter = 1f + (n2 - 0.5f) * 1.5f * haywireIntensity;
        }

        amp *= Mathf.Max(0.15f, ampJitter);
        freq *= Mathf.Max(0.3f, freqJitter);

        // Second harmonic layered on top of the base sine breaks up the "just gets
        // taller/shorter" look — gives each wave a richer, less uniform silhouette
        // without needing per-ghost custom shapes. harmonicMix controls how present it is.
        float harmonicAmp = amp * harmonicMix;
        float harmonicFreqMultiplier = 2.5f + normalized * 1.5f; // higher values = busier harmonic

        Vector2 prev = Vector2.zero;
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float x = -width * 0.5f + t * width;
            float y = amp * Mathf.Sin(freq * t * Mathf.PI * 2f + phaseAccum)
                     + harmonicAmp * Mathf.Sin(freq * harmonicFreqMultiplier * t * Mathf.PI * 2f + phaseAccum * 1.7f);
            Vector2 point = new Vector2(x, y);

            if (i > 0)
                AddQuad(vh, prev, point, lineWidth, color);

            prev = point;
        }
    }

    static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
    {
        Vector2 dir = (b - a).normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        Vector2 normal = new Vector2(-dir.y, dir.x) * width * 0.5f;

        int idx = vh.currentVertCount;
        vh.AddVert(a - normal, col, Vector2.zero);
        vh.AddVert(a + normal, col, Vector2.zero);
        vh.AddVert(b + normal, col, Vector2.zero);
        vh.AddVert(b - normal, col, Vector2.zero);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
}