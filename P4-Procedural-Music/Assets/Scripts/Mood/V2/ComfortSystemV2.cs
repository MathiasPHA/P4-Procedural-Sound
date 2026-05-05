using UnityEngine;

public class ComfortSystemV2 : MonoBehaviour
{
    public float comfort;

    [Header("Tier Thresholds")]
    [Tooltip("Mood >= this = Elated")]
    [SerializeField] private float elatedThreshold = 0.80f;
    [Tooltip("Mood >= this = Content")]
    [SerializeField] private float contentThreshold = 0.60f;
    [Tooltip("Mood >= this = Neutral")]
    [SerializeField] private float neutralThreshold = 0.40f;
    [Tooltip("Mood >= this = Uneasy")]
    [SerializeField] private float uneasyThreshold = 0.20f;
    // Below uneasy = Horrified (implicit)

    public ComfortTier CurrentTier => GetTier(comfort);

    public enum ComfortTier
    {
        Miserable,  // < 0.20 — Happiness drains fast
        Uneasy,     // < 0.40 — Happiness drains slowly
        Neutral,    // < 0.60 — Happiness neither drains nor fills
        Content,    // < 0.80 — Happiness fills slowly
        Elated      // >= 0.80 — Happiness fills fast
    }      
    private ComfortTier GetTier(float value)
    {
        if (value >= elatedThreshold) return ComfortTier.Elated;
        if (value >= contentThreshold) return ComfortTier.Content;
        if (value >= neutralThreshold) return ComfortTier.Neutral;
        if (value >= uneasyThreshold) return ComfortTier.Uneasy;
        return ComfortTier.Miserable;
    }


}
