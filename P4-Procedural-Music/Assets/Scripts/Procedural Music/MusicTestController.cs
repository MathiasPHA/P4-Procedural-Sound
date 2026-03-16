using UnityEngine;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;

namespace ProceduralMusic.Examples
{
    /// <summary>
    /// Example script demonstrating how to control the procedural music system
    /// from gameplay code. Attach this to any GameObject in your scene.
    /// 
    /// This example uses keyboard input for testing. In your actual game,
    /// replace the input handling with your game logic.
    /// </summary>
    public class MusicTestController : MonoBehaviour
    {
        [Tooltip("Reference to the ProceduralMusicController in your scene")]
        public ProceduralMusicController MusicController;

        [Header("Test Settings")]
        [Tooltip("Speed at which tension rises/falls during test")]
        public float TensionChangeSpeed = 0.3f;

        private float _testTension = 0.3f;

        void Start()
        {
            if (MusicController == null)
            {
                MusicController = FindObjectOfType<ProceduralMusicController>();
            }

            if (MusicController == null)
            {
                Debug.LogError("MusicTestController: No ProceduralMusicController found! " +
                    "Create one by adding ProceduralMusicController to a GameObject with an AudioSource.");
                return;
            }

            Debug.Log("=== Procedural Music System - Test Controls ===");
            Debug.Log("Arrow Up/Down: Increase/Decrease tension");
            Debug.Log("1-8: Switch game states (Explore, Dialogue, Tension, Combat, Victory, Mystery, Ambient, Spooky)");
            Debug.Log("Q/W: Change key root (down/up by semitone)");
            Debug.Log("M/N: Switch to Major/Minor mode");
            Debug.Log("Space: Panic (silence all)");
            Debug.Log("D: Toggle debug display");
            Debug.Log("=============================================");
        }

        void Update()
        {
            if (MusicController == null) return;

            // Tension control
            if (Input.GetKey(KeyCode.UpArrow))
            {
                _testTension += TensionChangeSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                _testTension -= TensionChangeSpeed * Time.deltaTime;
            }
            _testTension = Mathf.Clamp01(_testTension);
            MusicController.SetTension(_testTension);

            // State switching
            if (Input.GetKeyDown(KeyCode.Alpha1)) MusicController.SetGameState(GameMusicState.Explore);
            if (Input.GetKeyDown(KeyCode.Alpha2)) MusicController.SetGameState(GameMusicState.Dialogue);
            if (Input.GetKeyDown(KeyCode.Alpha3)) MusicController.SetGameState(GameMusicState.Tension);
            if (Input.GetKeyDown(KeyCode.Alpha4)) MusicController.SetGameState(GameMusicState.Combat);
            if (Input.GetKeyDown(KeyCode.Alpha5)) MusicController.SetGameState(GameMusicState.Victory);
            if (Input.GetKeyDown(KeyCode.Alpha6)) MusicController.SetGameState(GameMusicState.Mystery);
            if (Input.GetKeyDown(KeyCode.Alpha7)) MusicController.SetGameState(GameMusicState.Ambient);
            if (Input.GetKeyDown(KeyCode.Alpha8)) MusicController.SetGameState(GameMusicState.Spooky);

            // Key changes
            if (Input.GetKeyDown(KeyCode.Q))
            {
                var key = MusicController.GetCurrentChord();
                int newRoot = (((int)MusicController.StartingKey - 1) + 12) % 12;
                MusicController.ForceModulation((PitchClass)newRoot, MusicalMode.Major);
                Debug.Log($"Modulated to: {(PitchClass)newRoot}");
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                int newRoot = ((int)MusicController.StartingKey + 1) % 12;
                MusicController.ForceModulation((PitchClass)newRoot, MusicalMode.Major);
                Debug.Log($"Modulated to: {(PitchClass)newRoot}");
            }

            // Mode switches
            if (Input.GetKeyDown(KeyCode.M))
                MusicController.ForceModulation(MusicController.StartingKey, MusicalMode.Major);
            if (Input.GetKeyDown(KeyCode.N))
                MusicController.ForceModulation(MusicController.StartingKey, MusicalMode.NaturalMinor);

            // Panic
            if (Input.GetKeyDown(KeyCode.Space))
                MusicController.Panic();

            // Debug toggle
            if (Input.GetKeyDown(KeyCode.D))
                MusicController.ShowDebugInfo = !MusicController.ShowDebugInfo;
        }

        /// <summary>
        /// Example: Call this from your enemy proximity system to drive tension.
        /// </summary>
        public void OnEnemyProximityChanged(float closestEnemyDistance, float maxDistance)
        {
            // Invert: closer enemy = higher tension
            float tension = 1f - Mathf.Clamp01(closestEnemyDistance / maxDistance);
            MusicController.SetTension(tension);
        }

        /// <summary>
        /// Example: Call this when entering combat.
        /// </summary>
        public void OnCombatStarted()
        {
            MusicController.SetGameState(GameMusicState.Combat);
            MusicController.SetTension(0.7f);
        }

        /// <summary>
        /// Example: Call this when combat ends.
        /// </summary>
        public void OnCombatEnded(bool playerWon)
        {
            if (playerWon)
            {
                MusicController.SetGameState(GameMusicState.Victory);
                MusicController.SetTension(0.5f);
            }
            else
            {
                MusicController.SetGameState(GameMusicState.Explore);
                MusicController.SetTension(0.1f);
            }
        }

        /// <summary>
        /// Example: Call this from a dialogue system.
        /// </summary>
        public void OnDialogueStarted()
        {
            MusicController.SetGameState(GameMusicState.Dialogue);
            MusicController.SetTension(0.1f);
        }

        /// <summary>
        /// Example: Gradually build tension during a chase sequence.
        /// </summary>
        public void OnChaseProgress(float progress01)
        {
            MusicController.SetGameState(GameMusicState.Tension);
            MusicController.SetTension(Mathf.Lerp(0.3f, 0.9f, progress01));
        }
    }
}
