using Microsoft.Xna.Framework;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PersonalEffects
{

    public class ItemEatenEventArgs : EventArgs
    {
        public StardewValley.Item Item { get; set; }
        public StardewValley.Farmer Farmer { get; set; }
    }

    public class Events
    {
        //ItemEaten - called when a player starts eating an item. Not cancellable (because of how it's detected)
        public event ItemEatenHandler ItemEaten;
        public delegate void ItemEatenHandler(object sender, ItemEatenEventArgs args);
        internal ItemEatenEventArgs ItemEatenEvent(Farmer who, StardewValley.Item item)
        {
            var args = new ItemEatenEventArgs
            {
                Farmer = who,
                Item = item
            };
            ItemEaten?.Invoke(this, args);
            return args;
        }
    }
}
