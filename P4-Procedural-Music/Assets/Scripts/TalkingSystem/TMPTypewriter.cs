using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Typewriter effect for TextMeshPro. Reveals text character-by-character
/// using TMP's maxVisibleCharacters — no string slicing, so rich text tags
/// (colour, bold, sprites) keep working correctly.
///
/// Usage:
/// 1. Attach to a GameObject with a TextMeshProUGUI (UI) or TextMeshPro (3D) component.
/// 2. Set the full text on the TMP component as normal in the Inspector.
/// 3. To trigger it from a Timeline Signal: drop a Signal Track, create a Signal Receiver,
///    and bind it to the Play() method on this component.
///    Or just call Play() from any other script.
///
/// Tap/click skips to the end (toggleable).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TMPTypewriter : MonoBehaviour
{
    [Header("Speed")]
    [Tooltip("Characters revealed per second.")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("Extra pause (seconds) after these punctuation characters. Set to 0 to disable.")]
    [SerializeField] private float punctuationPause = 0.15f;
    [SerializeField] private string punctuationCharacters = ".!?,;:";

    [Header("Behaviour")]
    [Tooltip("Hide all text on Awake so the reveal starts from empty.")]
    [SerializeField] private bool hideOnAwake = true;

    [Tooltip("If true, automatically calls Play() on Start (one frame after Awake). " +
             "Use this for text that should type itself out as soon as it appears.")]
    [SerializeField] private bool playOnStart = true;

    [Tooltip("If true, tapping/clicking anywhere skips to the end.")]
    [SerializeField] private bool tapToSkip = true;

    [Header("Events")]
    [Tooltip("Fires once the full text is visible (whether revealed or skipped).")]
    public UnityEvent onComplete;

    private TMP_Text _text;
    private Coroutine _routine;

    public bool IsPlaying => _routine != null;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        if (hideOnAwake)
            _text.maxVisibleCharacters = 0;
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    private void Update()
    {
        if (tapToSkip && IsPlaying && Input.GetMouseButtonDown(0))
            Skip();
    }

    /// <summary>
    /// Start (or restart) the typewriter reveal from the first character.
    /// Hook this up to a Timeline Signal Receiver to trigger from a cutscene.
    /// </summary>
    public void Play()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Reveal());
    }

    /// <summary>
    /// Skip to the end — show all characters immediately and fire onComplete.
    /// </summary>
    public void Skip()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        _text.maxVisibleCharacters = _text.textInfo.characterCount;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Reset back to empty without playing. Useful if you want to re-trigger later.
    /// </summary>
    public void Hide()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        _text.maxVisibleCharacters = 0;
    }

    private IEnumerator Reveal()
    {
        // Force TMP to update its mesh so textInfo.characterCount is accurate
        _text.ForceMeshUpdate();
        int total = _text.textInfo.characterCount;

        _text.maxVisibleCharacters = 0;

        float secondsPerChar = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;

        for (int i = 1; i <= total; i++)
        {
            _text.maxVisibleCharacters = i;

            yield return new WaitForSeconds(secondsPerChar);

            // Extra pause after punctuation
            if (punctuationPause > 0f && i > 0 && i <= total)
            {
                char c = _text.textInfo.characterInfo[i - 1].character;
                if (punctuationCharacters.IndexOf(c) >= 0)
                    yield return new WaitForSeconds(punctuationPause);
            }
        }

        _routine = null;
        onComplete?.Invoke();
    }
}