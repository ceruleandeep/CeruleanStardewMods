# Changes

* 1.0.8 RELEASE
  * Add shops for Wizard, Haley, Elliott, Sam, Jas+Vincent, Maru
  * Catch exceptions caused by other lazy mods not doing gift preference
  * Move all market-opening settings to C# out of CP
  * Make GMM compat optional and smarter
  * Option to disable shops in config.json
  * Dump mod state to log each day
  * Optionally open when raining or snowing
  * Fix alternative currency/item-payment support
  * Display total sales value in sign message
  * ShopTiles is now a property so get set for mayhem
  
* 1.0.7 RELEASE
  * fix exploit where shop could be opened after hours via sign
  * remove click handler so we don't react to STF tile props
  * remove SVE's picnic set more thoroughly
  
* 1.0.5 RELEASE: New shops, new art, item quality
    * New shops: Caroline, Gunther, Pierre, Demetrius
    * Support item quality in NPC shops
    * Custom open and closed signs for shops
    * Load random items from a category
    * Fix annoying log spam when a not-ours chest is clicked on
\