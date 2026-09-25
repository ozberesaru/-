using System;

namespace Srpg.Core.Combat
{
    public enum TerrainType
    {
        Plain,
        Forest,
        Mountain,
        Fort,
        Water,
        Wall,
    }

    public readonly struct TerrainInfo
    {
        public const int Impassable = -1;

        public readonly int MoveCost;
        public readonly int Avoid;
        public readonly int Defense;

        public TerrainInfo(int moveCost, int avoid, int defense)
        {
            MoveCost = moveCost;
            Avoid = avoid;
            Defense = defense;
        }

        public bool Passable => MoveCost != Impassable;
    }

    public sealed class BattleMap
    {
        public readonly int Width;
        public readonly int Height;
        private readonly TerrainType[] tiles;

        public BattleMap(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TerrainType[width * height];
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public int Index(GridPos p) => p.Y * Width + p.X;

        public GridPos PosOf(int index) => new GridPos(index % Width, index / Width);

        public TerrainType Get(GridPos p) => tiles[Index(p)];

        public void Set(GridPos p, TerrainType t) => tiles[Index(p)] = t;

        public int EdgeDistance(GridPos p) =>
            Math.Min(Math.Min(p.X, p.Y), Math.Min(Width - 1 - p.X, Height - 1 - p.Y));

        public static char ToChar(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Forest: return 'T';
                case TerrainType.Mountain: return '^';
                case TerrainType.Fort: return 'F';
                case TerrainType.Water: return '~';
                case TerrainType.Wall: return '#';
                default: return '.';
            }
        }

        public static TerrainType FromChar(char c)
        {
            switch (c)
            {
                case '.': return TerrainType.Plain;
                case 'T': return TerrainType.Forest;
                case '^': return TerrainType.Mountain;
                case 'F': return TerrainType.Fort;
                case '~': return TerrainType.Water;
                case '#': return TerrainType.Wall;
                default: throw new ArgumentException($"未知地形字符 '{c}'");
            }
        }

        // Row 0 is y = 0.
        public static BattleMap FromRows(params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var map = new BattleMap(width, height);
            for (int y = 0; y < height; y++)
            {
                if (rows[y].Length != width) throw new ArgumentException($"第 {y} 行长度不一致");
                for (int x = 0; x < width; x++)
                    map.Set(new GridPos(x, y), FromChar(rows[y][x]));
            }
            return map;
        }
    }
}
