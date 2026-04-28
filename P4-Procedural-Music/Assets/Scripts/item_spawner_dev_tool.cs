using UnityEngine;
using UnityEngine.InputSystem;
using InventorySystem;
using InventorySystem.Data;
using System.Collections.Generic;

/// <summary>
/// Runtime dev tool for testing items and inventory.
///
/// Press F3 during play mode to show/hide. A small "Items [F3]" button
/// also appears in the top-right corner.
///
/// Features:
///   - Browse all items from the ItemDatabase
///   - Spawn items directly into player inventory
///   - Adjust spawn quantity (1-99)
///   - Filter by category (All, Tools, Consumables, Resources, etc.)
///   - Quick-add buttons for rapid testing
///   - Search functionality
///
/// SETUP:
///   1. Create an empty GameObject in your scene (e.g. "ItemSpawner") and attach this script.
///   2. Assign your ItemDatabase in the inspector.
///   3. Press F3 in play mode, or click the "Items [F3]" button in the top-right.
///   4. Remove or disable the GameObject before shipping a build.
///
/// Requires:
///   - ItemDatabase (assigned in inspector)
///   - InventoryBootstrap.PlayerInventory
/// </summary>
public class ItemSpawnerDevTool : MonoBehaviour
{
    // ───────────────────────── Config ─────────────────────────

    [Header("References")]
    [Tooltip("The ItemDatabase containing all items")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Toggle")]
    [Tooltip("Key to show/hide the item spawner")]
    [SerializeField] private Key toggleKey = Key.F3;
    [SerializeField] private bool openOnStart = false;
    [SerializeField] private bool showCornerButton = true;

    [Header("Layout (pixels)")]
    [SerializeField] private float panelX = 10f;
    [SerializeField] private float panelY = 10f;
    [SerializeField] private float panelWidth = 400f;
    [SerializeField] private float maxPanelHeight = 700f;

    // ───────────────────────── State ─────────────────────────

    private bool isOpen;
    private Vector2 scroll;

    // Filter and search
    private ItemCategory filterCategory = (ItemCategory)(-1); // -1 = All
    private string searchQuery = "";
    private int spawnQuantity = 1;

    // Cached inventory ref
    private Inventory playerInventory;

    // Lazy GUI styles
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;
    private GUIStyle itemButtonStyle;
    private GUIStyle categoryButtonStyle;
    private bool stylesInitialized;

    // ───────────────────────── Lifecycle ─────────────────────────

    private void Start()
    {
        isOpen = openOnStart;

        if (itemDatabase == null)
        {
            Debug.LogError("[ItemSpawner] No ItemDatabase assigned!");
            enabled = false;
            return;
        }

        playerInventory = InventoryBootstrap.PlayerInventory;
        if (playerInventory == null)
        {
            Debug.LogWarning("[ItemSpawner] No PlayerInventory found - spawning won't work!");
        }
    }

    private void Update()
    {
        // Hotkey toggle
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            isOpen = !isOpen;

        // Re-cache inventory if it was destroyed
        if (isOpen && playerInventory == null)
            playerInventory = InventoryBootstrap.PlayerInventory;
    }

    // ───────────────────────── GUI ─────────────────────────

    private void OnGUI()
    {
        InitStyles();

        // Corner button (shown even when panel is closed)
        if (!isOpen)
        {
            if (showCornerButton)
            {
                if (GUI.Button(new Rect(Screen.width - 180, 10, 90, 24), $"Items [{toggleKey}]"))
                    isOpen = true;
            }
            return;
        }

        float h = Mathf.Min(Screen.height - 20, maxPanelHeight);
        GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, h));
        GUILayout.BeginVertical(boxStyle);

        // Title bar
        GUILayout.BeginHorizontal();
        GUILayout.Label("ITEM SPAWNER", headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(24))) isOpen = false;
        GUILayout.EndHorizontal();

        if (itemDatabase == null)
        {
            GUILayout.Label("ERROR: No ItemDatabase assigned!", headerStyle);
            GUILayout.EndVertical();
            GUILayout.EndArea();
            return;
        }

        if (playerInventory == null)
        {
            GUI.color = new Color(1f, 0.5f, 0.5f);
            GUILayout.Label("⚠ No PlayerInventory found!", headerStyle);
            GUI.color = Color.white;
        }

        DrawControls();
        DrawItemList();

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    // ───────────────────────── Sections ─────────────────────────

    private void DrawControls()
    {
        GUILayout.BeginVertical(boxStyle);

        // Quantity slider
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Quantity: {spawnQuantity}", GUILayout.Width(100));
        spawnQuantity = Mathf.RoundToInt(GUILayout.HorizontalSlider(spawnQuantity, 1, 99));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Category filter
        GUILayout.Label("Filter by Category:", headerStyle);
        GUILayout.BeginHorizontal();

        if (CategoryButton("All", filterCategory == (ItemCategory)(-1)))
            filterCategory = (ItemCategory)(-1);

        GUILayout.BeginHorizontal();
        if (CategoryButton("Tool", filterCategory == ItemCategory.Tool))
            filterCategory = ItemCategory.Tool;

        if (CategoryButton("Consumable", filterCategory == ItemCategory.Consumable))
            filterCategory = ItemCategory.Consumable;

        if (CategoryButton("Material", filterCategory == ItemCategory.Material))
            filterCategory = ItemCategory.Material;

        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Search box
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(60));
        searchQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Clear", GUILayout.Width(60)))
            searchQuery = "";
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawItemList()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("— Available Items —", headerStyle);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));

        var allItems = itemDatabase.AllItems;
        int displayedCount = 0;

        foreach (var item in allItems)
        {
            if (item == null) continue;

            // Apply filters
            if (filterCategory != (ItemCategory)(-1) && item.category != filterCategory)
                continue;

            if (!string.IsNullOrEmpty(searchQuery))
            {
                string query = searchQuery.ToLower();
                if (!item.displayName.ToLower().Contains(query) &&
                    !item.id.ToLower().Contains(query))
                    continue;
            }

            displayedCount++;
            DrawItemEntry(item);
        }

        if (displayedCount == 0)
        {
            GUI.color = Color.gray;
            GUILayout.Label("  No items match filters", GUILayout.Height(30));
            GUI.color = Color.white;
        }

        GUILayout.EndScrollView();

        // Footer info
        GUI.color = Color.gray;
        GUILayout.Label($"Showing {displayedCount} / {allItems.Count} items");
        GUI.color = Color.white;

        GUILayout.EndVertical();
    }

    private void DrawItemEntry(ItemData item)
    {
        GUILayout.BeginHorizontal(boxStyle);

        // Item icon (if you want to display sprites)
        // Rect iconRect = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
        // if (item.icon != null)
        // {
        //     GUI.DrawTexture(iconRect, item.icon.texture);
        // }

        // Item info
        GUILayout.BeginVertical();

        GUILayout.Label(item.displayName, itemButtonStyle);

        GUI.color = Color.gray;
        string categoryText = $"[{item.category}]";
        if (item.IsStackable)
            categoryText += $" (Stack: {item.maxStackSize})";
        GUILayout.Label(categoryText, new GUIStyle(GUI.skin.label) { fontSize = 10 });
        GUI.color = Color.white;

        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // Spawn buttons
        if (playerInventory != null)
        {
            // Quick add (1)
            if (GUILayout.Button("+1", GUILayout.Width(40)))
            {
                SpawnItem(item, 1);
            }

            // Add quantity
            if (spawnQuantity > 1)
            {
                if (GUILayout.Button($"+{spawnQuantity}", GUILayout.Width(50)))
                {
                    SpawnItem(item, spawnQuantity);
                }
            }
        }
        else
        {
            GUI.enabled = false;
            GUILayout.Button("N/A", GUILayout.Width(50));
            GUI.enabled = true;
        }

        GUILayout.EndHorizontal();
    }

    // ───────────────────────── Actions ─────────────────────────

    private void SpawnItem(ItemData item, int quantity)
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("[ItemSpawner] Cannot spawn - no player inventory!");
            return;
        }

        // AddItem returns the number that could NOT fit (overflow)
        int overflow = playerInventory.AddItem(item, quantity);
        int added = quantity - overflow;

        if (added > 0)
        {
            Debug.Log($"[ItemSpawner] Spawned {added}x {item.displayName}" +
                      (overflow > 0 ? $" ({overflow} didn't fit)" : ""));
        }
        else
        {
            Debug.LogWarning($"[ItemSpawner] Failed to add {item.displayName} - inventory full?");
        }
    }

    private bool CategoryButton(string label, bool isActive)
    {
        var originalColor = GUI.backgroundColor;
        if (isActive)
            GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);

        bool clicked = GUILayout.Button(label, categoryButtonStyle, GUILayout.ExpandWidth(true));

        GUI.backgroundColor = originalColor;
        return clicked;
    }

    // ───────────────────────── Styling ─────────────────────────

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            normal = { textColor = new Color(1f, 0.9f, 0.4f) }
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6)
        };

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11
        };

        itemButtonStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 11,
            normal = { textColor = Color.white }
        };

        categoryButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            padding = new RectOffset(4, 4, 4, 4)
        };

        stylesInitialized = true;
    }
}