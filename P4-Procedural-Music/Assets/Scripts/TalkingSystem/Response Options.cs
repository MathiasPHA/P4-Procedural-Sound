using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using InventorySystem.Tools;

namespace InteractionSystem
{
    /// <summary>
    /// Displays a contextual feedback message when the player uses the wrong tool
    /// on a resource. Keyed on (equippedToolType, requiredToolType) from ToolType enum.
    ///
    /// SETUP:
    ///   1. Attach to a screen-space Canvas GameObject
    ///   2. Assign responseText (a TextMeshProUGUI on that Canvas)
    ///   3. Drag this component into ToolUseSystem.wrongToolFeedback in the Inspector
    /// </summary>
    public class ResponseOptions : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private TextMeshProUGUI responseText;
        [SerializeField] private float displayDuration = 3f;

        private Coroutine _hideCoroutine;

        // Key: (equippedToolType, requiredToolType) → message
        // These strings must match your ToolType enum values exactly
        private readonly Dictionary<(string equipped, string required), string> _responses = new()
        {
            { ("Axe",     "Pickaxe"), "You'll need a pickaxe for that."        },
            { ("Axe",     "Scythe"),  "You'll need a scythe for that."         },
            { ("Axe",     "Shovel"),  "You'll need a shovel for that."         },
            { ("Pickaxe", "Axe"),     "You'll need an axe for that."           },
            { ("Pickaxe", "Scythe"),  "You'll need a scythe for that."         },
            { ("Pickaxe", "Shovel"),  "You'll need a shovel for that."         },
            { ("Scythe",  "Axe"),     "You'll need an axe for that."           },
            { ("Scythe",  "Pickaxe"), "You'll need a pickaxe for that."        },
            { ("Shovel",  "Axe"),     "You'll need an axe for that."           },
            { ("Shovel",  "Pickaxe"), "You'll need a pickaxe for that."        },
        };

        /// <summary>
        /// Called by ToolUseSystem when the equipped tool doesn't match the resource requirement.
        /// equippedToolType and requiredToolType should be ToolType enum values as strings.
        /// Returns true if a matching message was found and shown.
        /// </summary>
        public bool TryShowWrongToolMessage(string equippedToolType, string requiredToolType)
        {
            if (!_responses.TryGetValue((equippedToolType, requiredToolType), out string message))
            {
                // Fallback for any unmapped combination
                ShowMessage($"You need a {requiredToolType.ToLower()} for that.");
                return true;
            }

            ShowMessage(message);
            return true;
        }

        /// <summary>Shows a message directly, bypassing the dictionary lookup.</summary>
        public void ShowMessage(string message)
        {
            if (responseText == null) return;

            responseText.text = message;
            responseText.gameObject.SetActive(true);

            // Reset the timer if a message is already on screen
            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);

            _hideCoroutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displayDuration);
            responseText.gameObject.SetActive(false);
            _hideCoroutine = null;
        }
    }
}