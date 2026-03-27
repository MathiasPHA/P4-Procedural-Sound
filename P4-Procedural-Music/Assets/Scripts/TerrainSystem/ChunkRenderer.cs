using UnityEngine;
using UnityEngine.Tilemaps;

namespace ProceduralTerrain
{
    /// <summary>
    /// Renders a ChunkData into a Unity Tilemap using the bitmask resolver
    /// and SuperTiled2Unity tile references.
    /// </summary>
    public static class ChunkRenderer
    {
        /// <summary>
        /// Render a chunk's terrain onto two tilemaps (ground + water).
        /// Uses the TerrainGenerator to sample neighbors across chunk boundaries.
        /// </summary>
        public static void RenderChunk(
            ChunkData chunk,
            Tilemap groundTilemap,
            Tilemap waterTilemap,
            TilesetReference tileset,
            TerrainGenerationConfig genConfig,
            int chunkSize)
        {
            int worldOffsetX = chunk.ChunkCoord.x * chunkSize;
            int worldOffsetY = chunk.ChunkCoord.y * chunkSize;

            var groundPositions = new Vector3Int[chunkSize * chunkSize];
            var waterPositions  = new Vector3Int[chunkSize * chunkSize];
            var groundTiles     = new TileBase[chunkSize * chunkSize];
            var waterTiles      = new TileBase[chunkSize * chunkSize];

            TileBase grassTile = tileset.GetTile(WaterBitmaskResolver.GRASS_TILE_ID);

            for (int ly = 0; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int idx = ly * chunkSize + lx;
                    int wx = worldOffsetX + lx;
                    int wy = worldOffsetY + ly;
                    var cellPos = new Vector3Int(wx, wy, 0);

                    groundPositions[idx] = cellPos;
                    waterPositions[idx] = cellPos;

                    TerrainType terrain = chunk.GetTerrain(lx, ly);

                    if (terrain == TerrainType.Grass)
                    {
                        groundTiles[idx] = grassTile;
                        waterTiles[idx] = null;
                    }
                    else if (terrain == TerrainType.Water)
                    {
                        // Sample 8 neighbors to build bitmask
                        // Use chunk data for in-bounds, generator for cross-boundary
                        int bitmask = BuildBitmaskAt(lx, ly, chunk, chunkSize, genConfig);
                        int tsxId = WaterBitmaskResolver.Resolve(bitmask);

                        groundTiles[idx] = null;
                        waterTiles[idx] = tileset.GetTile(tsxId);
                    }
                }
            }

            groundTilemap.SetTiles(groundPositions, groundTiles);
            waterTilemap.SetTiles(waterPositions, waterTiles);
        }

        /// <summary>
        /// Clear a chunk's area from both tilemaps.
        /// </summary>
        public static void ClearChunk(
            Vector2Int chunkCoord,
            Tilemap groundTilemap,
            Tilemap waterTilemap,
            int chunkSize)
        {
            int worldOffsetX = chunkCoord.x * chunkSize;
            int worldOffsetY = chunkCoord.y * chunkSize;

            var positions = new Vector3Int[chunkSize * chunkSize];
            var nullTiles = new TileBase[chunkSize * chunkSize];

            for (int ly = 0; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int idx = ly * chunkSize + lx;
                    positions[idx] = new Vector3Int(worldOffsetX + lx, worldOffsetY + ly, 0);
                    nullTiles[idx] = null;
                }
            }

            groundTilemap.SetTiles(positions, nullTiles);
            waterTilemap.SetTiles(positions, nullTiles);
        }

        private static int BuildBitmaskAt(int lx, int ly, ChunkData chunk, int chunkSize, TerrainGenerationConfig genConfig)
        {
            int wx = chunk.ChunkCoord.x * chunkSize + lx;
            int wy = chunk.ChunkCoord.y * chunkSize + ly;

            bool IsWater(int offsetX, int offsetY)
            {
                int nlx = lx + offsetX;
                int nly = ly + offsetY;

                // If within this chunk, use chunk data (respects modifications)
                if (nlx >= 0 && nlx < chunkSize && nly >= 0 && nly < chunkSize)
                    return chunk.GetTerrain(nlx, nly) == TerrainType.Water;

                // Otherwise, sample the generator (base terrain only for cross-boundary)
                return TerrainGenerator.SampleAt(wx + offsetX, wy + offsetY, genConfig) == TerrainType.Water;
            }

            return WaterBitmaskResolver.BuildBitmask(
                nw: IsWater(-1,  1),
                n:  IsWater( 0,  1),
                ne: IsWater( 1,  1),
                w:  IsWater(-1,  0),
                e:  IsWater( 1,  0),
                sw: IsWater(-1, -1),
                s:  IsWater( 0, -1),
                se: IsWater( 1, -1)
            );
        }
    }
}
