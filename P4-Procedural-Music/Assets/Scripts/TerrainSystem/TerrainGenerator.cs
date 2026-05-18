using UnityEngine;

namespace ProceduralTerrain
{
    /// <summary>
    /// Generates a terrain-type grid for a chunk using layered Perlin noise.
    /// Deterministic: same seed + chunk coord = same terrain every time.
    /// </summary>
    public static class TerrainGenerator
    {
        /// <summary>
        /// Generate a terrain grid for the given chunk.
        /// chunkCoord is in chunk-space (e.g. (0,0), (1,0), (-1,2)).
        /// Returns a chunkSize x chunkSize array of TerrainType.
        /// </summary>
        public static TerrainType[,] GenerateChunk(Vector2Int chunkCoord, int chunkSize, TerrainGenerationConfig config, int? seedOverride = null)
        {
            var grid = new TerrainType[chunkSize, chunkSize];

            int activeSeed = seedOverride ?? config.seed;

            // Deterministic offset from seed so different seeds give different worlds
            float seedOffsetX = activeSeed * 17.3f;
            float seedOffsetY = activeSeed * 31.7f;

            for (int y = 0; y < chunkSize; y++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    // World-space position of this tile
                    float worldX = chunkCoord.x * chunkSize + x;
                    float worldY = chunkCoord.y * chunkSize + y;

                    float noiseValue = SampleNoise(
                        worldX + seedOffsetX,
                        worldY + seedOffsetY,
                        config.noiseScale,
                        config.octaves,
                        config.persistence,
                        config.lacunarity
                    );

                    grid[y, x] = noiseValue < config.waterThreshold
                        ? TerrainType.Water
                        : TerrainType.Grass;
                }
            }

            return grid;
        }

        /// <summary>
        /// Sample a single terrain type at a world-space tile position.
        /// Useful for querying neighbors across chunk boundaries.
        /// </summary>
        public static TerrainType SampleAt(int worldX, int worldY, TerrainGenerationConfig config, int? seedOverride = null)
        {
            int activeSeed = seedOverride ?? config.seed;
            float seedOffsetX = activeSeed * 17.3f;
            float seedOffsetY = activeSeed * 31.7f;

            float noiseValue = SampleNoise(
                worldX + seedOffsetX,
                worldY + seedOffsetY,
                config.noiseScale,
                config.octaves,
                config.persistence,
                config.lacunarity
            );

            return noiseValue < config.waterThreshold
                ? TerrainType.Water
                : TerrainType.Grass;
        }

        private static float SampleNoise(float x, float y, float scale, int octaves, float persistence, float lacunarity)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float noiseValue = 0f;
            float maxAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float sampleX = x * scale * frequency;
                float sampleY = y * scale * frequency;

                // Mathf.PerlinNoise returns 0..1
                float perlin = Mathf.PerlinNoise(sampleX, sampleY);
                noiseValue += perlin * amplitude;
                maxAmplitude += amplitude;

                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return noiseValue / maxAmplitude; // Normalize to 0..1
        }
    }
}