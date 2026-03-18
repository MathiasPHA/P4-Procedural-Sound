using UnityEngine;
using ProceduralMusic.Core;
using ProceduralMusic.Bridge;

namespace ProceduralMusic.Examples
{
    /// <summary>
    /// Example script demonstrating how to control the procedural music system
    /// from gameplay code. Attach this to any GameObject in your scene.
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
                MusicController = FindObjectOfType<ProceduralMusicController>();

            if (MusicController == null)
            {
                Debug.LogError("MusicTestController: No ProceduralMusicController found!");
                return;
            }

            Debug.Log("=== Procedural Music System - Test Controls ===");
            Debug.Log("Arrow Up/Down: Increase/Decrease tension");
            Debug.Log("1-8: Switch states (Exploring, Exploring2, Pressure, Combat, Spooky, Horror, Night, Cozy)");
            Debug.Log("Q/W: Change key root (down/up)");
            Debug.Log("M/N: Major/Minor mode");
            Debug.Log("Space: Panic (silence all)");
            Debug.Log("D: Toggle debug display");
            Debug.Log("=============================================");
        }

        void Update()
        {
            if (MusicController == null) return;

            if (Input.GetKey(KeyCode.UpArrow))
                _testTension += TensionChangeSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.DownArrow))
                _testTension -= TensionChangeSpeed * Time.deltaTime;
            _testTension = Mathf.Clamp01(_testTension);
            MusicController.SetTension(_testTension);

            if (Input.GetKeyDown(KeyCode.Alpha1)) MusicController.SetGameState(GameMusicState.Exploring);
            if (Input.GetKeyDown(KeyCode.Alpha2)) MusicController.SetGameState(GameMusicState.Exploring2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) MusicController.SetGameState(GameMusicState.Pressure);
            if (Input.GetKeyDown(KeyCode.Alpha4)) MusicController.SetGameState(GameMusicState.Combat);
            if (Input.GetKeyDown(KeyCode.Alpha5)) MusicController.SetGameState(GameMusicState.Spooky);
            if (Input.GetKeyDown(KeyCode.Alpha6)) MusicController.SetGameState(GameMusicState.Horror);
            if (Input.GetKeyDown(KeyCode.Alpha7)) MusicController.SetGameState(GameMusicState.Night);
            if (Input.GetKeyDown(KeyCode.Alpha8)) MusicController.SetGameState(GameMusicState.Cozy);

            if (Input.GetKeyDown(KeyCode.Q))
            {
                int newRoot = (((int)MusicController.StartingKey - 1) + 12) % 12;
                MusicController.ForceModulation((PitchClass)newRoot, MusicalMode.Major);
            }
            if (Input.GetKeyDown(KeyCode.W))
            {
                int newRoot = ((int)MusicController.StartingKey + 1) % 12;
                MusicController.ForceModulation((PitchClass)newRoot, MusicalMode.Major);
            }

            if (Input.GetKeyDown(KeyCode.M))
                MusicController.ForceModulation(MusicController.StartingKey, MusicalMode.Major);
            if (Input.GetKeyDown(KeyCode.N))
                MusicController.ForceModulation(MusicController.StartingKey, MusicalMode.NaturalMinor);

            if (Input.GetKeyDown(KeyCode.Space))
                MusicController.Panic();

            if (Input.GetKeyDown(KeyCode.D))
                MusicController.ShowDebugInfo = !MusicController.ShowDebugInfo;
        }

        // ── Game Integration Examples ──

        public void OnEnemyProximityChanged(float closestEnemyDistance, float maxDistance)
        {
            float tension = 1f - Mathf.Clamp01(closestEnemyDistance / maxDistance);
            MusicController.SetTension(tension);
        }

        public void OnCombatStarted()
        {
            MusicController.SetGameState(GameMusicState.Combat);
            MusicController.SetTension(0.7f);
        }

        public void OnCombatEnded(bool playerWon)
        {
            MusicController.SetGameState(playerWon ? GameMusicState.Exploring2 : GameMusicState.Exploring);
            MusicController.SetTension(playerWon ? 0.3f : 0.1f);
        }

        public void OnNightFall()
        {
            MusicController.SetGameState(GameMusicState.Night);
            MusicController.SetTension(0.1f);
        }

        public void OnBeastNearby(float proximity01)
        {
            MusicController.SetGameState(GameMusicState.Horror);
            MusicController.SetTension(proximity01);
        }
    }
}
