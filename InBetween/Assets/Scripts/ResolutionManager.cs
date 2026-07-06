using System.Collections;
using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
    // Ordered from most-preferred to last-resort. The script walks this list
    // until Unity reports the switch actually took effect.
    private static readonly (int w, int h)[] Fallbacks = new (int, int)[]
    {
        (1920, 1440), // target (4:3)
        (2560, 1440), // 16:9 QHD
        (1920, 1080), // 16:9 FHD — near-universally supported
        (1600, 900),
        (1366, 768),
        (1280, 720),
    };

    private const FullScreenMode Mode = FullScreenMode.ExclusiveFullScreen;
    private const FullScreenMode WindowedFallback = FullScreenMode.FullScreenWindow;

    private static bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceResolutionEarly()
    {
        TryApply(Fallbacks[0].w, Fallbacks[0].h, Mode);
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (!applied) StartCoroutine(ApplyWithFallback());
    }

    private IEnumerator ApplyWithFallback()
    {
        // Give Unity a frame so Screen.width/height reflect the actual window.
        yield return null;

        foreach (var (w, h) in Fallbacks)
        {
            TryApply(w, h, Mode);
            // SetResolution is async — wait for it to settle before checking.
            yield return null;
            yield return null;

            if (Screen.width == w && Screen.height == h)
            {
                Debug.Log($"[ResolutionManager] Applied {w}x{h} in {Mode}.");
                applied = true;
                yield break;
            }

            Debug.LogWarning($"[ResolutionManager] {w}x{h} rejected (got {Screen.width}x{Screen.height}). Trying next.");
        }

        // Last resort: borderless window at desktop resolution. Always succeeds.
        Debug.LogWarning("[ResolutionManager] All target resolutions failed. Falling back to borderless desktop.");
        Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, WindowedFallback);
        applied = true;
    }

    private static void TryApply(int w, int h, FullScreenMode mode)
    {
        if (Screen.width == w && Screen.height == h && Screen.fullScreenMode == mode) return;
        Screen.SetResolution(w, h, mode);
    }
}