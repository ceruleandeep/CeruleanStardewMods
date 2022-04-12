using System.Collections.Generic;
using FarmersMarket.Shop;
using Microsoft.Xna.Framework;

namespace FarmersMarket.Data
{
    public class ContentPack
    {
        public List<Vector2> ShopLocations { get; set; }
        // public List<ItemShop> Shops { get; set; }
        public ItemShop[] Shops { get; set; }
        public AnimalShop[] AnimalShops { get; set; }
    }
}