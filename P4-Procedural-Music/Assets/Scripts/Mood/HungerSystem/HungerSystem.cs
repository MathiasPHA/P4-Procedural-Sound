using UnityEngine;
using InventorySystem;
using InventorySystem.Data;

/// <summary>
/// Player hunger bar (0–1). Drains passively over time. Eating food restores it.
/// Registers as an IMoodModifier on MoodSystem:
///
///   Full    (above satiatedThreshold) → positive mood rate
///   Normal  (between thresholds)      → no mood contribution
///   Hungry  (below hungryThreshold)   → negative mood rate (scales with emptiness)
///
/// Hunger does NOT directly affect happiness — it affects mood, which in turn
/// drives the happiness drift rate via MoodTier.
///
/// SETUP:
///   1. Attach to the Player GameObject (same object as MoodSystem).
///   2. MoodSystem must exist for mood registration.
///   3. InventoryBootstrap.PlayerInventory must exist for food consumption events.
/// </summary>
public class HungerSystem : MonoBehaviour, IMoodModifier
{
    public static HungerSystem Instance { get; private set; }

    // ───────────────────────── Tuning ─────────────────────────

    [Header("Hunger Bar")]
    [Tooltip("Starting hunger (0 = starving, 1 = full)")]
    [Range(0f, 1f)]
    [SerializeField] private float startingHunger = 0.8f;

    [Tooltip("Hunger drained per second. At 0.005, a full bar lasts ~200 seconds (3.3 min). " +
             "Tune this relative to your day length.")]
    [SerializeField] private float drainPerSecond = 0.005f;

    [Header("Mood Thresholds")]
    [Tooltip("Above this hunger level, mood gets a positive boost (well-fed)")]
    [Range(0f, 1f)]
    [SerializeField] private float satiatedThreshold = 0.7f;

    [Tooltip("Below this hunger level, mood gets a negative penalty (hungry)")]
    [Range(0f, 1f)]
    [SerializeField] private float hungryThreshold = 0.3f;

    [Header("Mood Rates")]
    [Tooltip("Max positive mood rate when completely full (units/sec)")]
    [SerializeField] private float wellFedMoodRate = 0.03f;

    [Tooltip("Max negative mood rate when completely starving (units/sec)")]
    [SerializeField] private float starvingMoodRate = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    // ───────────────────────── State ─────────────────────────

    private float hunger;
    private MoodSystem moodSystem;

    // ───────────────────────── IMoodModifier ─────────────────────────

    public float MoodRate
    {
        get
        {
            if (hunger >= satiatedThreshold)
            {
                // Well-fed: scale from 0 at threshold to max at 1.0
                float t = Mathf.InverseLerp(satiatedThreshold, 1f, hunger);
                return t * wellFedMoodRate;
            }

            if (hunger <= hungryThreshold)
            {
                // Hungry: scale from 0 at threshold to -max at 0.0
                float t = Mathf.InverseLerp(hungryThreshold, 0f, hunger);
                return -t * starvingMoodRate;
            }

            // Normal range — no mood contribution
            return 0f;
        }
    }

    public string ModifierName => "Hunger";
    public bool IsActive => enabled;

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Current hunger value (0 = starving, 1 = full).</summary>
    public float Hunger => hunger;

    /// <summary>True if hunger is below the hungry threshold.</summary>
    public bool IsHungry => hunger < hungryThreshold;

    /// <summary>True if hunger is above the satiated threshold.</summary>
    public bool IsWellFed => hunger > satiatedThreshold;

    /// <summary>
    /// Restore hunger by a fixed amount (e.g. from a food item).
    /// Clamped to 0–1.
    /// </summary>
    public void RestoreHunger(float amount)
    {
        float before = hunger;
        hunger = Mathf.Clamp01(hunger + amount);
        Debug.Log($"[Hunger] +{amount:F2} hunger ({before:F2} → {hunger:F2})");
    }

    /// <summary>
    /// Force hunger to an exact value (game start, respawn, debug).
    /// </summary>
    public void SetHunger(float value)
    {
        hunger = Mathf.Clamp01(value);
    }

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        hunger = startingHunger;
    }

    private void Start()
    {
        // Register with MoodSystem
        moodSystem = MoodSystem.Instance;
        if (moodSystem == null)
            moodSystem = FindObjectOfType<MoodSystem>();

        if (moodSystem != null)
            moodSystem.Register(this);
        else
            Debug.LogWarning("[HungerSystem] No MoodSystem found — " +
                             "hunger won't affect mood.");

        // Subscribe to food consumption
        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory != null)
            inventory.OnItemConsumed += OnItemConsumed;
        else
            Debug.LogWarning("[HungerSystem] No PlayerInventory found — " +
                             "eating won't restore hunger.");
    }

    private void OnDestroy()
    {
        if (moodSystem != null)
            moodSystem.Unregister(this);

        var inventory = InventoryBootstrap.PlayerInventory;
        if (inventory != null)
            inventory.OnItemConsumed -= OnItemConsumed;
    }

    private void Update()
    {
        DrainHunger();
    }

    // ───────────────────────── Core ─────────────────────────

    private void DrainHunger()
    {
        if (hunger <= 0f) return;

        hunger = Mathf.Max(0f, hunger - drainPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// Called when the player consumes an item from inventory.
    /// Routes hunger restore and happiness restore to the correct systems.
    /// </summary>
    private void OnItemConsumed(ItemInstance consumed)
    {
        if (consumed == null) return;
        if (consumed.Data.category != ItemCategory.Consumable) return;

        // Food → hunger bar
        if (consumed.Data.hungerRestore > 0f)
            RestoreHunger(consumed.Data.hungerRestore);

        // Potions / special items → happiness directly
        if (consumed.Data.happinessRestore > 0f && HappinessSystem.Instance != null)
        {
            HappinessSystem.Instance.AdjustHappiness(consumed.Data.happinessRestore);
            Debug.Log($"[Hunger] {consumed.Data.displayName} restored " +
                      $"+{consumed.Data.happinessRestore:F2} happiness");
        }
    }

    // ───────────────────────── Debug GUI ─────────────────────────

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        float x = 320f;   // offset right to sit beside ComfortSystem debug
        float y = 220f;
        float barWidth = 200f;
        float barHeight = 20f;

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 300, 20), "=== HUNGER SYSTEM ===");
        y += 24f;

        // Hunger bar
        Color barColor;
        if (hunger > satiatedThreshold)
            barColor = new Color(0.3f, 0.9f, 0.3f); // green - well fed
        else if (hunger > hungryThreshold)
            barColor = new Color(0.9f, 0.7f, 0.2f); // yellow - normal
        else
            barColor = new Color(0.9f, 0.2f, 0.2f); // red - hungry

        GUI.Label(new Rect(x, y, 100, barHeight), $"Hunger: {hunger:F2}");
        DrawBar(new Rect(x + 110, y, barWidth, barHeight), hunger, barColor);
        y += barHeight + 6f;

        // Mood rate contribution
        float rate = MoodRate;
        GUI.color = rate >= 0f ? Color.green : Color.red;
        string status = IsWellFed ? "Well-fed" : IsHungry ? "Hungry!" : "Normal";
        GUI.Label(new Rect(x, y, 300, 20),
            $"→ Mood: {rate:+0.000;-0.000}/s ({status})");
        y += 22f;

        // Drain info
        GUI.color = Color.gray;
        float secsRemaining = hunger > 0f ? hunger / drainPerSecond : 0f;
        GUI.Label(new Rect(x, y, 300, 20),
            $"Drain: {drainPerSecond:F4}/s — empty in {secsRemaining:F0}s");

        GUI.color = Color.white;
    }

    private void DrawBar(Rect rect, float value, Color color)
    {
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        GUI.color = color;
        Rect fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);

        // Threshold markers
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        float hungryX = rect.x + rect.width * hungryThreshold;
        GUI.DrawTexture(new Rect(hungryX - 1, rect.y, 2, rect.height), Texture2D.whiteTexture);
        float satiatedX = rect.x + rect.width * satiatedThreshold;
        GUI.DrawTexture(new Rect(satiatedX - 1, rect.y, 2, rect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
    }
}