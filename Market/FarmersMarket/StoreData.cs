using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FarmersMarket
{
    public class StoresListData
    {
        public List<Vector2> StoreLocations { get; set; }
        public List<StoreData> Stores { get; set; }
    }

    public class StoreData
    {
        public string NpcName { get; set; }
        public int SignObject { get; set; }
        public Color Color { get; set; }
        public Dictionary<string, int> Stock { get; set; }
    }
}