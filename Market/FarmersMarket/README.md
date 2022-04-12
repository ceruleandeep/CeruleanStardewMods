# Farmers Market

## Credits

* Uses code from ChroniclerCherry's Shop Tile Framework
* Uses code from aedenthorn's Persistent Grange Stand

## Store life cycle

Day started:
* reset the sales and visitor counters
* reset the lists of sales and recent buyers
* find or create the chest and sign objects
* put some stock in the chest, if an NPC store
* restock the shop from the chest
* move the furniture into position

When world is rendered:
* draw the shop contents

When button pressed:
* if a player shop, open the grange menu
* if a NPC shop, open the shop menu

Every hour:
* move some items from the chest to the shop

Every update:
* if an NPC is in front of the shop, stop to look

Every 1s update:
* if an NPC is looking, maybe sell something to them
* if the NPC store owner is nearby, stop to tend the shop

Day ended:
* if a player shop, move the shop stock into the chest and hide the furniture
* if a NPC shop, destroy the furniture



## Developer notes

patch summary ceruleandeep.FarmersMarket.CP
patch summary ceruleandeep.FarmersMarket

patch reload ceruleandeep.FarmersMarket.CP

  "37": "Wood Sign/5/-300/Crafting -9/Use an item on this to change what's displayed. The item won't be consumed./true/true/0/Wood Sign",
  "130": "Chest/0/-300/Crafting -9/A place to store your items./true/true/0/Chest",
  "232": "Stone Chest/0/-300/Crafting -9/A place to store your items./true/true/0/Stone Chest",


Object
    Object::performToolAction
        public virtual bool performToolAction(Tool t, GameLocation location)
        
        this.performRemoveAction((Vector2) (NetFieldBase<Vector2, NetVector2>) this.tileLocation, location);
        if (location.objects.ContainsKey((Vector2) (NetFieldBase<Vector2, NetVector2>) this.tileLocation))
          location.objects.Remove((Vector2) (NetFieldBase<Vector2, NetVector2>) this.tileLocation);
              
    Object::performRemoveAction
    
    Object.fragility
    
    Object.isTemporarilyInvisible
    
    public virtual bool placementAction(GameLocation location, int x, int y, Farmer who = null)
        makes a fresh one of whatever item the player is trying to place
        if returns true, players stack of the item is reduced by 1
        sets the owner
        for a Chest:
            if location.objects.ContainsKey(placementTile) || location is MineShaft || location is VolcanoDungeon:
                unsuitable location, return false
            make a new Chest(true, placementTile, this.parentSheetIndex);
            add to location.objects at x, y
        for a Sign:
            location.objects.Add(placementTile, (Object) new Sign(placementTile, this.ParentSheetIndex));
        


Chest
    type: "Crafting" or "interactive"
    chestType: "Monster"
    playerChest: 
    TileLocation:
    
    interactive chest ctor:
    public Chest()
    public Chest(Vector2 location)
    public Chest(string type, Vector2 location, MineShaft mine)
    public Chest(int coins, List<Item> items, Vector2 location, bool giftbox = false, int giftboxIndex = 0)
    
    crafting chest ctor:
    public Chest(bool playerChest, Vector2 tileLocation, int parentSheetIndex = 130)
    public Chest(bool playerChest, int parentSheedIndex = 130)
     
    public override bool performToolAction(Tool t, GameLocation location)
        if TileLocation is 0,0 try and find the tile from location.Objects or use the tool's target tile
        if empty: 
            performRemoveAction
            location.Objects.Remove(tileLocation)
            something about debris
       
    MoveToSafePosition
    public bool MoveToSafePosition(
          GameLocation location,
          Vector2 tile_position,
          int depth = 0,
          Vector2? prioritize_direction = null)
       
    placementAction
    public override bool placementAction(GameLocation location, int x, int y, Farmer who = null)
        basically calls base
    