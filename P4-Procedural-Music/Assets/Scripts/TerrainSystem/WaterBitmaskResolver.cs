using System.Collections.Generic;

namespace ProceduralTerrain
{
    /// <summary>
    /// Resolves water tile variants based on 8-bit neighbor bitmask.
    /// Derived from Water_Rules.tmx automapping rules.
    /// 
    /// Bitmask bit layout:
    ///   NW=7  N=6  NE=5
    ///    W=4       E=3
    ///   SW=2  S=1  SE=0
    /// 
    /// Each bit is 1 if that neighbor is water, 0 if land.
    /// Values are Forest_Tileset.tsx tile IDs (local, not GIDs).
    /// </summary>
    public static class WaterBitmaskResolver
    {
        public const int GRASS_TILE_ID = 49; // G_Mid (tile id=49 in Forest_Tileset.tsx)

        // Bit positions for neighbor directions
        public const int NW = 7, N = 6, NE = 5;
        public const int W = 4, E = 3;
        public const int SW = 2, S = 1, SE = 0;

        /// <summary>
        /// Build an 8-bit bitmask from neighbor water states.
        /// Pass true for each direction that contains water.
        /// </summary>
        public static int BuildBitmask(bool nw, bool n, bool ne, bool w, bool e, bool sw, bool s, bool se)
        {
            int mask = 0;
            if (nw) mask |= (1 << NW);
            if (n) mask |= (1 << N);
            if (ne) mask |= (1 << NE);
            if (w) mask |= (1 << W);
            if (e) mask |= (1 << E);
            if (sw) mask |= (1 << SW);
            if (s) mask |= (1 << S);
            if (se) mask |= (1 << SE);
            return mask;
        }

        /// <summary>
        /// Given a bitmask of water neighbors, returns the tsx tile ID for the water tile variant.
        /// </summary>
        public static int Resolve(int bitmask)
        {
            return _lookup[bitmask & 0xFF];
        }

        // Full 256-entry lookup table, generated from Water_Rules.tmx
        // with wildcard expansion and specificity-based conflict resolution.
        private static readonly int[] _lookup = new int[256]
        {
            // 0b00000000 .. 0b00001111
            20, 20, 13, 13, 20, 20, 13, 13, 11, 11,  5, 21, 11, 11,  5, 21,
            // 0b00010000 .. 0b00011111
            12, 12,  6,  6, 12, 12, 23, 23, 25, 25, 17, 69, 25, 25, 71, 22,
            // 0b00100000 .. 0b00101111
            20, 20, 13, 13, 20, 20, 13, 13, 11, 11,  5, 21, 11, 11,  5, 21,
            // 0b00110000 .. 0b00111111
            12, 12,  6,  6, 12, 12, 20, 20, 25, 25, 17, 69, 25, 25, 71, 22,
            // 0b01000000 .. 0b01001111
            10, 10, 26, 26, 10, 10, 26, 26,  3,  3, 19, 64,  3,  3, 19, 64,
            // 0b01010000 .. 0b01011111
             4,  4, 18, 18,  4,  4, 65, 65,  2,  2, 24, 58,  2,  2, 59, 46,
            // 0b01100000 .. 0b01101111
            10, 10, 26, 26, 10, 10, 26, 26,  7,  7, 66, 50, 20, 20, 66, 50,
            // 0b01110000 .. 0b01111111
             4,  4, 18, 18,  4,  4, 65, 65, 68, 68, 56, 44, 68, 68, 47, 52,
            // 0b10000000 .. 0b10001111
            20, 20, 13, 13, 20, 20, 13, 13, 11, 11,  5, 20, 11, 11,  5, 20,
            // 0b10010000 .. 0b10011111
            12, 12,  6,  6, 12, 12, 23, 23, 25, 25, 17, 69, 25, 25, 71, 22,
            // 0b10100000 .. 0b10101111
            20, 20, 13, 13, 20, 20, 13, 13, 11, 11,  5, 20, 11, 11,  5, 20,
            // 0b10110000 .. 0b10111111
            12, 12,  6,  6, 12, 12, 20, 20, 25, 25, 17, 69, 25, 25, 71, 22,
            // 0b11000000 .. 0b11001111
            10, 10, 26, 26, 10, 10, 26, 26,  3,  3, 19, 64,  3,  3, 19, 64,
            // 0b11010000 .. 0b11011111
             9, 20, 67, 67,  9, 20, 51, 51, 70, 70, 57, 48, 70, 70, 45, 53,
            // 0b11100000 .. 0b11101111
            10, 10, 26, 26, 10, 10, 26, 26,  7,  7, 66, 50, 20, 20, 66, 50,
            // 0b11110000 .. 0b11111111
             9, 20, 67, 67,  9, 20, 51, 51,  8,  8, 43, 54,  8,  8, 55, 15,
        };

        // Human-readable tile name lookup for debugging
        private static readonly Dictionary<int, string> _tileNames = new Dictionary<int, string>
        {
            {  2, "W_3T"      }, {  3, "W_BendBL"  }, {  4, "W_BendBR"  }, {  5, "W_BendTL"  },
            {  6, "W_BendTR"  }, {  7, "W_BL"      }, {  8, "W_BM"      }, {  9, "W_BR"      },
            { 10, "W_EB"      }, { 11, "W_EL"      }, { 12, "W_ER"      }, { 13, "W_ET"      },
            { 15, "W_Mid"     }, { 17, "W_3B"      }, { 18, "W_3L"      }, { 19, "W_3R"      },
            { 20, "W_Single"  }, { 21, "W_TL"      }, { 22, "W_TM"      }, { 23, "W_TR"      },
            { 24, "W_X"       }, { 25, "W_SingleLR" },{ 26, "W_SingleUD" },
            { 43, "W_MOD"     }, { 44, "W_MOL"     }, { 45, "W_MOR"     }, { 46, "W_MOT"     },
            { 47, "W_2C1"     }, { 48, "W_2C2"     },
            { 50, "W_LM"      }, { 51, "W_RM"      },
            { 52, "W_ICTL"    }, { 53, "W_ICTR"    }, { 54, "W_ICBL"    }, { 55, "W_ICBR"    },
            { 56, "W_2OBL"    }, { 57, "W_2OBR"    }, { 58, "W_2OTL"    }, { 59, "W_2OTR"    },
            { 64, "W_UCL"     }, { 65, "W_UCR"     }, { 66, "W_DCL"     }, { 67, "W_DCR"     },
            { 68, "W_LCD"     }, { 69, "W_LCU"     }, { 70, "W_RCD"     }, { 71, "W_RCU"     },
        };

        public static string GetTileName(int tsxId)
        {
            return _tileNames.TryGetValue(tsxId, out var name) ? name : $"Unknown({tsxId})";
        }
    }
}