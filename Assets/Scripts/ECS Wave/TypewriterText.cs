using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Small reusable helper that types text into a TMP_Text character by character.
/// Attach to the same object as (or reference) the TMP_Text you want to animate.
/// </summary>
public class TypewriterText : MonoBehaviour
{
    public TMP_Text target;
    public float secondsPerChar = 0.04f;

    Coroutine running;

    /// <summary>Type out the given text. Calls onComplete when finished (or immediately if interrupted by a new Type call... not chained).</summary>
    public void Type(string text, System.Action onComplete = null)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(TypeRoutine(text, onComplete));
    }

    /// <summary>Immediately set text with no animation (e.g. for clearing).</summary>
    public void SetInstant(string text)
    {
        if (running != null) StopCoroutine(running);
        if (target != null) target.text = text;
    }

    IEnumerator TypeRoutine(string text, System.Action onComplete)
    {
        if (target == null) yield break;

        target.text = "";
        for (int i = 0; i < text.Length; i++)
        {
            target.text += text[i];
            yield return new WaitForSeconds(secondsPerChar);
        }

        running = null;
        onComplete?.Invoke();
    }
}
