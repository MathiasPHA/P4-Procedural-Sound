using UnityEngine;

namespace ProceduralTerrain
{
    public enum TerrainType
    {
        Grass = 0,
        Water = 1,
        // Future: Forest = 2, Path = 3, Rock = 4, etc.
    }

    /// <summary>
    /// Configuration for procedural terrain generation.
    /// Tweak noise, water threshold, etc. in the Inspector.
    /// The seed here is the default — at runtime ChunkManager uses
    /// the seed from GameSettings and passes it as an override,
    /// so this asset is never modified at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainGenConfig", menuName = "Procedural Terrain/Generation Config")]
    public class TerrainGenerationConfig : ScriptableObject
    {
        [Header("Noise Settings")]
        [Tooltip("Default seed. Overridden at runtime by GameSettings.")]
        public int seed = 42;

        [Tooltip("Scale of the primary noise. Smaller = larger features.")]
        [Range(0.005f, 0.1f)]
        public float noiseScale = 0.03f;

        [Tooltip("Number of noise octaves for detail.")]
        [Range(1, 6)]
        public int octaves = 3;

        [Tooltip("How much each octave contributes relative to the last.")]
        [Range(0f, 1f)]
        public float persistence = 0.5f;

        [Tooltip("How much the frequency increases per octave.")]
        [Range(1f, 4f)]
        public float lacunarity = 2f;

        [Header("Water")]
        [Tooltip("Noise values below this become water.")]
        [Range(0f, 1f)]
        public float waterThreshold = 0.38f;
    }
}
