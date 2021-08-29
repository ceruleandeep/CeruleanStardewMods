using System;
using System.Collections.Generic;
using System.Linq;
using BetterJunimos.Abilities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;

namespace BetterJunimosForestry
{
    public static class RoomTypes
    {
        public const int WALL = 0;
        public const int PATH = 1;
        public const int HUT = 2;
        public const int DOOR = 3;
        public const int FIXED_WALL = 4;
    }
    
    public class Maze
    {
        public int[,] Rooms;

        public Maze(int radius) : this(radius, null) { }

        public Maze(int radius, int[,] maze)
        {
            if (maze is null)
            {
                int size = 2 * radius + 1;
                maze = new int[size, size];
            }
            if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
            Rooms = GetMaze(radius, maze);
        }

        public int Radius()
        {
            return Rooms.GetLength(0) / 2;
        }
        
        private static List<Vector2> Directions = new List<Vector2> {new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1)};
        
        
        // utility methods

        protected bool IsTrellisCrop(Item item) {
            Crop crop = new Crop(item.ParentSheetIndex, 0, 0);
            return crop.raisedSeeds.Value;
        }

        /// <summary>Identify tiles that already contain something impassable</summary>
        /// so that they can be treated as non-removable walls during maze building
        private static int[,] FixedWallsForHut(JunimoHut hut)
        {
            int radius = ModEntry.BJApi.GetJunimoHutMaxRadius();
            int sx = hut.tileX.Value - radius + 1;
            int sy = hut.tileY.Value - radius + 1;
            int size = 2 * radius + 1;
            int[,] maze = new int[size, size];
            Farm farm = Game1.getFarm();
            
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var pos = new Vector2(x+sx, y+sy);
                    
                    // trellis crop check
                    if (farm.terrainFeatures.ContainsKey(pos) && farm.terrainFeatures[pos] is HoeDirt hd && hd.crop != null)
                    {
                        if (!hd.crop.dead.Value && hd.crop.raisedSeeds.Value) 
                        {
                            maze[x, y] = RoomTypes.FIXED_WALL;
                        }
                        
                        // skip the crop check in Util.IsOccupied()
                        continue;
                    }
                    
                    // general obstruction check
                    if (Util.IsOccupied(farm, pos))
                    {
                        maze[x, y] = RoomTypes.FIXED_WALL;
                    }
                }
            }
            
            // ModEntry.SMonitor.Log($"FixedWallsForHut: sx {sx} sy {sy} size {size} radius {radius}", LogLevel.Debug);
            // print(maze);
            return maze;
        }
        
        internal static void MakeMazeForHut(JunimoHut hut) {
            if (hut is null) {
                ModEntry.SMonitor.Log($"SetMazeForHut: hut is null", LogLevel.Warn);
                return;
            }
            
            Maze m = new Maze(ModEntry.BJApi.GetJunimoHutMaxRadius(), FixedWallsForHut(hut));
            string[,] ct = new string[m.Rooms.GetLength(0), m.Rooms.GetLength(1)];
            
            for (int r = 0; r < m.Rooms.GetLength(0); r++)
            {
                for (int c = 0; c < m.Rooms.GetLength(1); c++)
                {
                    ct[r, c] = CropTypeForRoomType(m.Rooms[r, c]);
                }
            }

            BetterJunimos.CropMap ctm = new BetterJunimos.CropMap();
            ctm.Map = ct;
            ModEntry.BJApi.SetCropMapForHut(Util.GetHutIdFromHut(hut), ctm);
        }

        internal static void ClearMazeForHut(JunimoHut hut)
        {
            ModEntry.BJApi.ClearCropMapForHut(Util.GetHutIdFromHut(hut));
        }

        internal static string CropTypeForRoomType(int roomType)
        {
            if (roomType == RoomTypes.WALL) return BetterJunimos.CropTypes.Trellis;
            return BetterJunimos.CropTypes.Ground;
        }
        
        public static int getRoomAtTile(JunimoHut hut, Vector2 pos)
        {
            Maze m = getMazeForHut(hut);

            int dx = (int)pos.X - (int)hut.tileX.Value;
            int dy = (int)pos.Y - (int)hut.tileY.Value;

            int mx = m.Radius() - 1 + dx;
            int my = m.Radius() - 1 + dy;
            
            // ModEntry.SMonitor.Log($"getRoomAtTile: pos [{pos.X} {pos.Y}] radius {m.Radius()} d [{dx} {dy}] m [{mx} {my}]", LogLevel.Debug);

            return m.Rooms[mx, my];
        }

        public static Maze getMazeForHut(JunimoHut hut)
        {
            Vector2 pos = new Vector2(hut.tileX.Value, hut.tileY.Value);
            if (!ModEntry.HutMazes.ContainsKey(pos))
            {
                ModEntry.HutMazes[pos] = new Maze(ModEntry.BJApi.GetJunimoHutMaxRadius());
            }

            return ModEntry.HutMazes[pos];
        }
        
        public static int[,] GetMaze(int radius, int[,] maze)
        {
            // ModEntry.SMonitor.Log($"GetMaze: radius {radius}", LogLevel.Debug);
            int size = 2 * radius + 1;
            
            placeHut(maze);
            
            // start at the hut door and visit all rooms in the maze
            int bx = radius + 1;
            int by = radius + 1;
            visit(maze, bx, by);
            
            // pick a random spot at the top of the grid as the maze entry
            int offset = Game1.random.Next(size);
            for (int x = 0; x < size; x++)
            {
                int checkX = (x + offset) % size;
                if (maze[checkX, 0] != RoomTypes.FIXED_WALL && maze[checkX, 1] == RoomTypes.PATH)
                {
                    setPath(maze, checkX, 0);
                    break;
                }
            }

            // print(maze);
            return maze;
        }

        
        // internals, may not need to be static if we stop passing maze around
        static void placeHut(int[,] maze)
        {
            int hx = maze.GetLength(0) / 2 - 1;
            int hy = maze.GetLength(1) / 2 - 1;
            for (int x = hx; x < hx + 3; x++)
            {
                for (int y = hy; y < hy + 2; y++)
                {
                    maze[x, y] = RoomTypes.HUT;
                }
            }

            maze[hx + 1, hy + 2] = RoomTypes.DOOR;
        }
        
        static void print(int[,] maze)
        {
            string c;
            string line;
            for (int y = 0; y < maze.GetLength(1); y++)
            {
                line = "";
                for (int x = 0; x < maze.GetLength(0); x++)
                {
                    switch (maze[x, y])
                    {
                        case RoomTypes.PATH: 
                            c = "  ";
                            break;
                        case RoomTypes.WALL:
                            c = "##";
                            break;
                        case RoomTypes.HUT:
                            c = "HH";
                            break;
                        case RoomTypes.DOOR:
                            c = "[]";
                            break;
                        case RoomTypes.FIXED_WALL:
                            c = "FW";
                            break;
                        default:
                            c = "??";
                            break;
                    }
                    line += c;
                }
                ModEntry.SMonitor.Log($"{line}", LogLevel.Debug);
            }
        }
        
        static void visit(int[,] maze, int cx, int cy)
        {
            // Console.WriteLine($"visit({cx}, {cy})");
            setPath(maze, cx, cy);
            // print(maze);

            var directions = Directions.OrderBy(a => Guid.NewGuid()).ToList();
            foreach (var direction in directions)
            {
                var wx = cx + (int)direction.X;
                var wy = cy + (int)direction.Y;
                var nx = cx + (int)direction.X * 2;
                var ny = cy + (int)direction.Y * 2;

                if (isUnvisitedRoom(maze, nx, ny) && maze[wx, wy] != RoomTypes.FIXED_WALL)
                {
                    // success
                    setPath(maze, wx, wy);
                    visit(maze, nx, ny);
                }
            }
        }

        static bool isRoom(int[,] maze, int x, int y)
        {
            // Console.WriteLine($"isRoom({x}, {y}): {(x % 2 == 1 && y % 2 == 1)}");
            if (maze[x, y] == RoomTypes.DOOR) return true;
            return (x % 2 == 1 && y % 2 == 1);
        }
        static bool isUnvisitedRoom(int[,] maze, int x, int y)
        {
            var wall = false;
            try
            {
                if (isRoom(maze, x, y) && maze[x, y] == RoomTypes.WALL ) wall = true;
                if (isRoom(maze, x, y) && maze[x, y] == RoomTypes.DOOR ) wall = true;
            }
            catch (IndexOutOfRangeException)
            {
                wall = false;
            }

            // Console.WriteLine($"wall({x}, {y}) = {wall}");
            return wall;
        }

        static void setPath(int[,] maze, int x, int y)
        {
            // Console.WriteLine($"setPath({x}, {y})");
            try
            {
                maze[x, y] = RoomTypes.PATH;
            }
            catch (IndexOutOfRangeException) { }
        }
    }
}