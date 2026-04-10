using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace InteractionSystem
{
    public class ResponseOptions : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] private TextMeshProUGUI responseText;
        [SerializeField] private float displayDuration = 3f;

        [Header("Typewriter")]
        [SerializeField] private float characterDelay = 0.05f;

        private Coroutine _activeCoroutine;

        private readonly Dictionary<(string equipped, string required), string> _responses = new()
        {
            { ("Axe",     "Pickaxe"), "Maybe I should try a pickaxe." },
            { ("Axe",     "Shovel"),  "You'll need a shovel for that."  },
            { ("Pickaxe", "Axe"),     "Maybe I should try an axe."    },
            { ("Pickaxe", "Shovel"),  "You'll need a shovel for that."  },
            { ("Shovel",  "Axe"),     "You'll need an axe for that."    },
            { ("Shovel",  "Pickaxe"), "You'll need a pickaxe for that." },
        };

        public bool TryShowWrongToolMessage(string equippedToolType, string requiredToolType)
        {
            string message = _responses.TryGetValue((equippedToolType, requiredToolType), out string match)
                ? match
                : $"You need a {requiredToolType.ToLower()} for that.";

            ShowMessage(message);
            return true;
        }

        public void ShowMessage(string message)
        {
            if (responseText == null) return;

            // Stop any running typewriter or hide timer
            if (_activeCoroutine != null)
                StopCoroutine(_activeCoroutine);

            responseText.gameObject.SetActive(true);
            _activeCoroutine = StartCoroutine(TypewriterRoutine(message));
        }

        private IEnumerator TypewriterRoutine(string message)
        {
            responseText.text = string.Empty;

            foreach (char c in message)
            {
                responseText.text += c;
                yield return new WaitForSeconds(characterDelay);
            }

            // Full message shown — now wait before hiding
            yield return new WaitForSeconds(displayDuration);
            responseText.gameObject.SetActive(false);
            _activeCoroutine = null;
        }
    }
}