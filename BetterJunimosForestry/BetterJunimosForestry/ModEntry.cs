using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BetterJunimos;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace BetterJunimosForestry {
    public static class Modes {
        public static readonly string Normal = "normal";
        public static readonly string Crops = "crops";
        public static readonly string Orchard = "orchard";
        public static readonly string Forest = "forest";
        public static readonly string Grains = "grains";
        public static readonly string Maze = "maze";
    }
    
    public class HutState {
        public bool ShowHUD = false;
        public string Mode = Modes.Normal;
    }

    public class ModeChange {
        public Guid guid;
        public string mode;

        public ModeChange(Guid guid, string mode) {
            this.guid = guid;
            this.mode = mode;
        }
    }
    
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod {
        private Dictionary<Rectangle, ModeChange> Rectangles = new Dictionary<Rectangle, ModeChange>();
        
        private Rectangle TreeIcon = new Rectangle(0, 656, 14, 14);
        private Rectangle JunimoIcon = new Rectangle(109, 492, 14, 14);
        private Rectangle CropIcon = new Rectangle(178, 129, 14, 14);
        private Rectangle FruitTreeIcon = new Rectangle(16, 624, 14, 14);
        private Rectangle ScrollIcon = new Rectangle(673, 81, 14, 14);
        private Rectangle BundleIcon = new Rectangle(331, 374, 15, 14);
        private Rectangle LetterIcon = new Rectangle(190, 422, 14, 14);
        private Rectangle QuestionIcon = new Rectangle(174, 424, 14, 14);
        private Rectangle MapIcon = new Rectangle(426, 492, 14, 14);
        
        internal static ModConfig Config;
        internal static IMonitor SMonitor;
        internal static Dictionary<Vector2, HutState> HutStates;
        internal static Dictionary<Vector2, Maze> HutMazes;

        internal static IBetterJunimosApi BJApi;
        
        internal static Abilities.PlantTreesAbility PlantTrees;
        internal static Abilities.PlantFruitTreesAbility PlantFruitTrees;

        private void RenderedWorld(object sender, RenderedWorldEventArgs e) {
            if (Game1.player.currentLocation is not Farm) return;
            Rectangles.Clear();
            foreach (KeyValuePair<Vector2, HutState> kvp in HutStates) {
                var state = kvp.Value;
                if (!state.ShowHUD) continue;
                JunimoHut hut = Util.GetHutFromPosition(kvp.Key);
                Guid guid = Util.GetHutIdFromHut(hut);
                if (hut == null) {
                    // Monitor.Log($"RenderedWorld {kvp.Key} is not a valid building", LogLevel.Debug);
                    continue;
                }

                const int padding = 3;
                const int offset = 14 * Game1.pixelZoom;

                int scrollWidth = offset * 7 + padding * 2;
                int hutXvp = hut.tileX.Value * Game1.tileSize - Game1.viewport.X + 1;  // hut x co-ord in viewport pixels
                int scrollXvp = (int)(hutXvp + Game1.tileSize * 1.5 - scrollWidth / 2);

                Vector2 origin = new Vector2(scrollXvp,(int) hut.tileY.Value * Game1.tileSize - Game1.viewport.Y + 1 + Game1.tileSize*2 + 16 );

                int n = 0;
                Rectangle normal =  new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle crops =   new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle orchard = new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle forest =  new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle maze =    new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle quests =  new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);
                Rectangle actions = new Rectangle((int) origin.X + padding + offset * n++, (int) origin.Y - 4, 14 * Game1.pixelZoom, 14 * Game1.pixelZoom);

                Rectangle scroll =  new Rectangle((int)origin.X, (int)origin.Y, scrollWidth, 18);

                // Monitor.Log($"RenderedWorld scroll_width {scroll_width} hut_xvp {hut_xvp} scroll_xvp {scroll_xvp}");
                //Utility.PointToVector2(Game1.viewport.ToXna().Location) + (new Vector2(hut.tileX, hut.tileY + hut.tilesHigh) * Game1.tileSize)
                
                Rectangles[scroll] = new ModeChange(guid, "_menu");
                Rectangles[normal] = new ModeChange(guid, "normal");
                Rectangles[crops] = new ModeChange(guid, "crops");
                Rectangles[orchard] = new ModeChange(guid, "orchard");
                Rectangles[forest] = new ModeChange(guid, "forest");
                Rectangles[maze] = new ModeChange(guid, "maze");
                Rectangles[quests] = new ModeChange(guid, "_quests");
                Rectangles[actions] = new ModeChange(guid, "_actions");
                    
                Util.DrawScroll(e.SpriteBatch, origin, scrollWidth);
                e.SpriteBatch.Draw(Game1.mouseCursors, normal, JunimoIcon, Color.White * (state.Mode=="normal"?1.0f:0.25f));
                e.SpriteBatch.Draw(Game1.mouseCursors, crops, CropIcon, Color.White * (state.Mode=="crops"?1.0f:0.25f));
                e.SpriteBatch.Draw(Game1.mouseCursors, orchard, FruitTreeIcon, Color.White * (state.Mode=="orchard"?1.0f:0.25f));
                e.SpriteBatch.Draw(Game1.mouseCursors, forest, TreeIcon,Color.White * (state.Mode=="forest"?1.0f:0.25f));
                e.SpriteBatch.Draw(Game1.mouseCursors, maze, MapIcon,Color.White * (state.Mode=="maze"?1.0f:0.25f));
                e.SpriteBatch.Draw(Game1.mouseCursors, quests, LetterIcon, Color.White);
                e.SpriteBatch.Draw(Game1.mouseCursors, actions, QuestionIcon, Color.White);
            }
        }

        void OnButtonPressed(object sender, ButtonPressedEventArgs e) {
            if (!Context.IsWorldReady) { return; }
            
            if (e.Button == SButton.MouseLeft) {
                if (Game1.player.currentLocation is not Farm) return;
                if (Game1.activeClickableMenu != null) return;
                
                JunimoHut hut = Util.HutOnTile(e.Cursor.Tile);
                if (hut is not null) {
                    Vector2 hut_pos = Util.GetHutPositionFromHut(hut);
                    if (!HutStates.ContainsKey(hut_pos)) HutStates[hut_pos] = new HutState();
                    HutStates[hut_pos].ShowHUD = !HutStates[hut_pos].ShowHUD;
                    // Monitor.Log($"Hut {hut_pos} HUD state {HutStates[hut_pos].ShowHUD} mode {HutStates[hut_pos].Mode}", LogLevel.Debug);
                    Helper.Input.Suppress(SButton.MouseLeft);
                    return;
                }
                
                foreach (KeyValuePair<Rectangle, ModeChange> kvp in Rectangles) {
                    Rectangle r = kvp.Key;
                    ModeChange mc = kvp.Value;
                    bool contains = r.Contains((int)e.Cursor.ScreenPixels.X, (int)e.Cursor.ScreenPixels.Y);
                    if (contains) {
                        Helper.Input.Suppress(SButton.MouseLeft);
                        hut = Util.GetHutFromId(mc.guid);
                        Vector2 hut_pos = Util.GetHutPositionFromId(mc.guid);
                        // Monitor.Log($"Rectangle {r} {mc.mode} {r.X} {r.Y} contains: {contains}");
                        if (mc.mode == "_quests") {
                            // Monitor.Log($"quests triggered", LogLevel.Debug);
                            BJApi.ShowPerfectionTracker();
                        }
                        if (mc.mode == "_actions") {
                            // Monitor.Log($"actions triggered", LogLevel.Debug);
                            BJApi.ListAvailableActions(mc.guid);
                        }

                        if (!mc.mode.StartsWith("_"))
                        {
                            HutStates[hut_pos].Mode = mc.mode;
                            if (mc.mode == "maze")
                            {
                                Maze.MakeMazeForHut(hut);
                            }
                            else
                            {
                                Maze.ClearMazeForHut(hut);
                            }
                        }
                    }
                }
            }
        }
        
        private void OnLaunched(object sender, GameLaunchedEventArgs e) {
            HutStates = new Dictionary<Vector2, HutState>();
            HutMazes = new Dictionary<Vector2, Maze>();
            
            Config = Helper.ReadConfig<ModConfig>();
            Util.Config = Config;

            var gmcm_api = Helper.ModRegistry.GetApi<GenericModConfigMenuAPI>("spacechase0.GenericModConfigMenu");
            if (gmcm_api is not null) {
                gmcm_api.RegisterModConfig(ModManifest, () => Config = new ModConfig(), () => Helper.WriteConfig(Config));
                gmcm_api.SetDefaultIngameOptinValue(ModManifest, false);

                gmcm_api.RegisterSimpleOption(ModManifest, "Sustainable tree harvesting", "Only harvest wild trees when they've grown a seed", () => Config.SustainableWildTreeHarvesting, (val) => Config.SustainableWildTreeHarvesting = val);

                gmcm_api.RegisterChoiceOption(ModManifest, "Wild tree pattern", "", () => Config.WildTreePattern, (string val) => Config.WildTreePattern = val, Config.WildTreePatternChoices);
                gmcm_api.RegisterChoiceOption(ModManifest, "Fruit tree pattern", "", () => Config.FruitTreePattern, (string val) => Config.FruitTreePattern = val, Config.FruitTreePatternChoices);
                
                gmcm_api.RegisterClampedOption(ModManifest, "Wild tree growth boost", "", () => Config.PlantWildTreesSize, (float val) => Config.PlantWildTreesSize = (int) val, 0, 5, 1);
                gmcm_api.RegisterClampedOption(ModManifest, "Fruit tree growth boost", "", () => Config.PlantFruitTreesSize, (float val) => Config.PlantFruitTreesSize = (int) val, 0, 5, 1);
                
                gmcm_api.RegisterSimpleOption(ModManifest, "Harvest Grass", "", () => Config.HarvestGrassEnabled, (val) => Config.HarvestGrassEnabled = val);
            }

            BJApi = Helper.ModRegistry.GetApi<BetterJunimos.IBetterJunimosApi>("hawkfalcon.BetterJunimos");
            if (BJApi is null) {
                Monitor.Log($"Could not load Better Junimos API", LogLevel.Error);
                return;
            }

            PlantTrees = new Abilities.PlantTreesAbility(Monitor);
            PlantFruitTrees = new Abilities.PlantFruitTreesAbility(Monitor);

            BJApi.RegisterJunimoAbility(new Abilities.HarvestGrassAbility());
            BJApi.RegisterJunimoAbility(new Abilities.HarvestDebrisAbility(Monitor));
            BJApi.RegisterJunimoAbility(new Abilities.CollectDroppedObjectsAbility(Monitor));
            BJApi.RegisterJunimoAbility(new Abilities.ChopTreesAbility(Monitor));
            BJApi.RegisterJunimoAbility(new Abilities.CollectSeedsAbility(Monitor));
            BJApi.RegisterJunimoAbility(new Abilities.FertilizeTreesAbility());
            BJApi.RegisterJunimoAbility(PlantTrees);
            BJApi.RegisterJunimoAbility(PlantFruitTrees);
            BJApi.RegisterJunimoAbility(new Abilities.HarvestFruitTreesAbility(Monitor));
            BJApi.RegisterJunimoAbility(new Abilities.HoeAroundTreesAbility(Monitor));
            // BJApi.RegisterJunimoAbility(new Abilities.LayPathsAbility(Monitor));
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaveLoaded(object sender, EventArgs e) {
            // reload the config to pick up any changes made in GMCM on the title screen
            Config = Helper.ReadConfig<ModConfig>();

            // load hut mode settings from the save file
            HutStates = this.Helper.Data.ReadSaveData<Dictionary<Vector2, HutState>>("ceruleandeep.BetterJunimosForestry.HutStates");
            if (HutStates is null) HutStates = new Dictionary<Vector2, HutState>();

            // load hut maze settings from the save file
            HutMazes = this.Helper.Data.ReadSaveData<Dictionary<Vector2, Maze>>("ceruleandeep.BetterJunimosForestry.HutMazes");
            if (HutMazes is null) HutMazes = new Dictionary<Vector2, Maze>();
        }
        
        /// <summary>Raised after a the game is saved</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnSaving(object sender, SavingEventArgs e) {
            Helper.Data.WriteSaveData("ceruleandeep.BetterJunimosForestry.HutStates", HutStates);
            Helper.Data.WriteSaveData("ceruleandeep.BetterJunimosForestry.HutMazes", HutMazes);
            Helper.WriteConfig(Config);
        }
        
        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnDayStarted(object sender, DayStartedEventArgs e) {
            // Monitor.Log($"Huts in HutStates", LogLevel.Debug);
            foreach (Vector2 hut_pos in HutStates.Keys) {
                HutState state = HutStates[hut_pos];
                // Monitor.Log($"    {hut_pos} {state.Mode}", LogLevel.Debug);
            }
            
            Monitor.Log($"Huts in farm", LogLevel.Debug);
            foreach (JunimoHut hut in Game1.getFarm().buildings.OfType<JunimoHut>()) {
                Guid guid = Game1.getFarm().buildings.GuidOf(hut);
                Monitor.Log($"    [{hut.tileX} {hut.tileY}] {Util.GetModeForHut(hut)}", LogLevel.Debug);
                if (Util.GetModeForHut(hut) == Modes.Maze)
                {
                    Maze.MakeMazeForHut(hut);
                }
            }

            // reset for rainy days, winter, or GMCM options change
            Helper.Content.InvalidateCache(@"Characters\Junimo");
        }
        
        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper) {
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Display.RenderedWorld += RenderedWorld;
            Helper.Events.GameLoop.GameLaunched += OnLaunched;
            Helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            Helper.Events.GameLoop.Saving += OnSaving;
            Helper.Events.GameLoop.DayStarted += OnDayStarted;

            SMonitor = Monitor;
        }
    }
}
