using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;

/// <summary>
/// Tracks chapter progression based on souls resolved (via EnemyPhaseManager).
/// Chapter breakdown:
///   Ch 1: souls 1-2  (Ghost 1 object active)
///   Ch 2: souls 3-6  (Ghost 3 object active)
///   Ch 3: souls 7-8  (Ghost 7 object active)
///
/// Saves progress ONLY at the end of a chapter — dying mid-chapter means
/// you restart that chapter from its beginning soul count.
///
/// On Awake: reads saved chapter, activates the correct ghost + extras,
/// updates the chapter name TMP, fires the appropriate OnChapterXStart event,
/// and resets soulsResolved on the phase manager to the chapter's start value.
/// </summary>
[DisallowMultipleComponent]
public class GameStateManager : MonoBehaviour
{
    public enum Chapter { Chapter1 = 1, Chapter2 = 2, Chapter3 = 3, Completed = 4 }

    [Header("Enemy Phase Manager (source of soul count)")]
    [SerializeField] private EnemyPhaseManager phaseManager;

    [Header("Chapter Starting Soul (one active at a time)")]
    [Tooltip("Starting soul GameObject for Chapter 1 (soul #1). Handles souls 1-2 internally.")]
    [SerializeField] private GameObject chapter1StartSoul;
    [Tooltip("Starting soul GameObject for Chapter 2 (soul #3). Handles souls 3-6 internally.")]
    [SerializeField] private GameObject chapter2StartSoul;
    [Tooltip("Starting soul GameObject for Chapter 3 (soul #7). Handles souls 7-8 internally.")]
    [SerializeField] private GameObject chapter3StartSoul;

    [Header("Chapter Name UI (optional)")]
    [SerializeField] private TextMeshProUGUI chapterNameText;
    [SerializeField] private string chapter1Name = "Chapter I";
    [SerializeField] private string chapter2Name = "Chapter II";
    [SerializeField] private string chapter3Name = "Chapter III";

    [Header("Save Settings")]
    [Tooltip("PlayerPrefs key used to store the current chapter number.")]
    [SerializeField] private string saveKey = "GameState_Chapter";
    [Tooltip("If true, wipes the save on Awake (useful for testing).")]
    [SerializeField] private bool clearSaveOnAwake = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    [Header("Chapter Start Events (fire when a chapter begins)")]
    public UnityEvent OnChapter1Start;
    public UnityEvent OnChapter2Start;
    public UnityEvent OnChapter3Start;

    [Header("Chapter End Events (fire when a chapter completes)")]
    public UnityEvent OnChapter1End;
    public UnityEvent OnChapter2End;
    public UnityEvent OnChapter3End;

    [Header("Game Completion")]
    public UnityEvent OnAllChaptersCompleted;

    private Chapter currentChapter = Chapter.Chapter1;
    private int lastKnownSoulCount = -1;

    // Chapter boundaries — souls resolved at which chapters start/end
    private const int Ch1EndSouls = 2;
    private const int Ch2EndSouls = 6;
    private const int Ch3EndSouls = 8;

    void Awake()
    {
        if (clearSaveOnAwake)
        {
            PlayerPrefs.DeleteKey(saveKey);
            Log("clearSaveOnAwake was true — save wiped.");
        }

        LoadAndApply();
    }

    void Start()
    {
        // Fire the initial OnChapterXStart AFTER all other components' Start() methods have run.
        // Firing during Awake was too early: any listener that modifies state which another
        // script initializes in its own Start (e.g. EnemyTorch adding charges only to have them
        // reset when EnemyTorch.Start runs) would silently lose that state. Waiting one frame
        // guarantees every Start has completed before we fire.
        StartCoroutine(FireInitialChapterStartNextFrame());
    }

    IEnumerator FireInitialChapterStartNextFrame()
    {
        yield return null; // wait one frame — all Starts have now run
        switch (currentChapter)
        {
            case Chapter.Chapter1: OnChapter1Start?.Invoke(); Log("OnChapter1Start fired (deferred, post-Start)."); break;
            case Chapter.Chapter2: OnChapter2Start?.Invoke(); Log("OnChapter2Start fired (deferred, post-Start)."); break;
            case Chapter.Chapter3: OnChapter3Start?.Invoke(); Log("OnChapter3Start fired (deferred, post-Start)."); break;
        }
    }

    void Update()
    {
        // Poll soul count each frame — cheap int compare, fine for horror-game pacing.
        if (phaseManager == null) return;

        int souls = phaseManager.soulsResolved;
        if (souls != lastKnownSoulCount)
        {
            lastKnownSoulCount = souls;
            EvaluateSoulProgress(souls);
        }
    }

    // ---------------------------------------------------------------------
    // Load / apply
    // ---------------------------------------------------------------------

    private void LoadAndApply()
    {
        int savedInt = PlayerPrefs.GetInt(saveKey, (int)Chapter.Chapter1);

        // Anything invalid or "Completed" from a fresh scene load — treat as Ch 1.
        if (savedInt < 1 || savedInt > 3)
            savedInt = (int)Chapter.Chapter1;

        currentChapter = (Chapter)savedInt;

        int startingSouls = SoulsAtStartOf(currentChapter);

        if (phaseManager != null)
        {
            phaseManager.soulsResolved = startingSouls;
            lastKnownSoulCount = startingSouls;
            Log($"Loaded save: {currentChapter}. Reset phaseManager.soulsResolved to {startingSouls}.");

            // Optional companion method — see note in file header. If it doesn't
            // exist, this line does nothing and phase must be set manually via
            // the OnChapterXStart UnityEvents.
            TrySyncPhaseManager(startingSouls);
        }
        else
        {
            Log("WARNING: phaseManager reference is null. Soul tracking disabled.");
        }

        ApplyChapterState(currentChapter, fireStartEvent: false);
    }

    private void TrySyncPhaseManager(int souls)
    {
        // Uses reflection so this script compiles even if you don't add the
        // optional LoadProgress method to EnemyPhaseManager. If the method
        // exists, it will be called; otherwise this is a silent no-op.
        var method = phaseManager.GetType().GetMethod("LoadProgress",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(phaseManager, new object[] { souls });
            Log($"Called EnemyPhaseManager.LoadProgress({souls}).");
        }
    }

    private int SoulsAtStartOf(Chapter c) => c switch
    {
        Chapter.Chapter1 => 0,
        Chapter.Chapter2 => Ch1EndSouls, // 2
        Chapter.Chapter3 => Ch2EndSouls, // 6
        _ => 0
    };

    private void ApplyChapterState(Chapter c, bool fireStartEvent)
    {
        // Only the starting soul of the current chapter is active. The other two
        // stay off. That soul handles all subsequent souls in its chapter internally.
        SetActiveSafe(chapter1StartSoul, c == Chapter.Chapter1);
        SetActiveSafe(chapter2StartSoul, c == Chapter.Chapter2);
        SetActiveSafe(chapter3StartSoul, c == Chapter.Chapter3);

        UpdateChapterText(c);

        if (fireStartEvent)
        {
            switch (c)
            {
                case Chapter.Chapter1: OnChapter1Start?.Invoke(); Log("OnChapter1Start fired."); break;
                case Chapter.Chapter2: OnChapter2Start?.Invoke(); Log("OnChapter2Start fired."); break;
                case Chapter.Chapter3: OnChapter3Start?.Invoke(); Log("OnChapter3Start fired."); break;
            }
        }
    }

    private void UpdateChapterText(Chapter c)
    {
        if (chapterNameText == null) return;

        string label = c switch
        {
            Chapter.Chapter1 => chapter1Name,
            Chapter.Chapter2 => chapter2Name,
            Chapter.Chapter3 => chapter3Name,
            _ => chapterNameText.text
        };

        chapterNameText.text = label;
    }

    // ---------------------------------------------------------------------
    // Chapter transitions
    // ---------------------------------------------------------------------

    private void EvaluateSoulProgress(int souls)
    {
        if (currentChapter == Chapter.Chapter1 && souls >= Ch1EndSouls)
        {
            Log($"Chapter 1 complete at {souls} souls.");
            OnChapter1End?.Invoke();
            AdvanceTo(Chapter.Chapter2);
        }
        else if (currentChapter == Chapter.Chapter2 && souls >= Ch2EndSouls)
        {
            Log($"Chapter 2 complete at {souls} souls.");
            OnChapter2End?.Invoke();
            AdvanceTo(Chapter.Chapter3);
        }
        else if (currentChapter == Chapter.Chapter3 && souls >= Ch3EndSouls)
        {
            Log($"Chapter 3 complete at {souls} souls.");
            OnChapter3End?.Invoke();
            currentChapter = Chapter.Completed;
            SaveChapter(Chapter.Completed);
            OnAllChaptersCompleted?.Invoke();
        }
    }

    private void AdvanceTo(Chapter next)
    {
        currentChapter = next;
        SaveChapter(next);
        Log($"Advanced to {next}. Save written.");
        ApplyChapterState(next, fireStartEvent: true);
    }

    private void SaveChapter(Chapter c)
    {
        PlayerPrefs.SetInt(saveKey, (int)c);
        PlayerPrefs.Save();
    }

    // ---------------------------------------------------------------------
    // Public helpers (wire to buttons, cheats, dev menus, etc.)
    // ---------------------------------------------------------------------

    /// <summary>Wipes the save and forces the next scene load to start at Chapter 1.</summary>
    [ContextMenu("DEBUG: Clear Save")]
    public void ClearSave()
    {
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        Log("Save cleared.");
    }

    /// <summary>Manually jump to a chapter (debug / cheat).</summary>
    public void JumpToChapter(int chapterNumber)
    {
        chapterNumber = Mathf.Clamp(chapterNumber, 1, 3);
        currentChapter = (Chapter)chapterNumber;
        SaveChapter(currentChapter);

        int startingSouls = SoulsAtStartOf(currentChapter);
        if (phaseManager != null)
        {
            phaseManager.soulsResolved = startingSouls;
            lastKnownSoulCount = startingSouls;
            TrySyncPhaseManager(startingSouls);
        }

        ApplyChapterState(currentChapter, fireStartEvent: true);
        Log($"Jumped to {currentChapter}.");
    }

    public Chapter GetCurrentChapter() => currentChapter;
    public int GetCurrentChapterNumber() => (int)currentChapter;

    // ---------------------------------------------------------------------
    // Utility
    // ---------------------------------------------------------------------

    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    private void Log(string msg)
    {
        if (debugLogging) Debug.Log($"[GameStateManager] {msg}");
    }
}