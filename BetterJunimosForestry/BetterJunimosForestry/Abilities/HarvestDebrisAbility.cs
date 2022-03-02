using System;
using System.Collections.Generic;
using BetterJunimos.Abilities;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Tools;
using StardewModdingAPI;
using SObject = StardewValley.Object;

// bits of this are from Tractor Mod; https://github.com/Pathoschild/StardewMods/blob/68628a40f992288278b724984c0ade200e6e4296/TractorMod/Framework/BaseAttachment.cs#L132

namespace BetterJunimosForestry.Abilities {
    public class HarvestDebrisAbility : IJunimoAbility {

        private readonly IMonitor Monitor;
        private Pickaxe FakePickaxe = new();
        private Axe FakeAxe = new();
        private MeleeWeapon Scythe = new(47);
        private FakeFarmer FakeFarmer = new();

        internal HarvestDebrisAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
            FakeAxe.IsEfficient = true;
            FakePickaxe.IsEfficient = true;
            Scythe.IsEfficient = true;
        }

        public string AbilityName() {
            return "HarvestDebris";
        }

        private bool IsDebris(SObject so) {
            bool debris = IsTwig(so) || IsWeed(so) || IsStone(so);
            return debris;
        }

        protected bool IsTwig(SObject obj) {
            return obj?.ParentSheetIndex == 294 || obj?.ParentSheetIndex == 295;
        }

        protected bool IsWeed(SObject obj) {
            return !(obj is Chest) && obj?.Name == "Weeds";
        }

        protected bool IsStone(SObject obj) {
            return !(obj is Chest) && obj?.Name == "Stone";
        }

        public bool IsActionAvailable(Farm farm, Vector2 pos, Guid guid) {
            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (farm.objects.ContainsKey(nextPos) && IsDebris(farm.objects[nextPos])) {
                    return true;
                }
            }
            return false;
        }

        public bool PerformAction(Farm farm, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            int direction = 0;
            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (!Util.IsWithinRadius(Util.GetHutFromId(guid), nextPos)) continue;
                if (farm.objects.ContainsKey(nextPos) && IsDebris(farm.objects[nextPos])) {

                    junimo.faceDirection(direction);
                    // SetForageQuality(farm, nextPos);

                    SObject item = farm.objects[nextPos];
                    GameLocation location = Game1.currentLocation;

                    if (IsStone(item)) {
                        UseToolOnTile(FakePickaxe, nextPos, Game1.currentLocation);
                    }

                    if (IsTwig(item)) {
                        UseToolOnTile(FakeAxe, nextPos, Game1.currentLocation);
                    }

                    if (IsWeed(item)) {
                        UseToolOnTile(Scythe, nextPos, location);
                        item.performToolAction(Scythe, Game1.currentLocation);
                        location.removeObject(nextPos, false);
                    }
                    return true;
                }
                direction++;
            }

            return false;
        }

        protected bool UseToolOnTile(Tool t, Vector2 tile, GameLocation location) {
            FakeFarmer.currentLocation = location;

            // use tool on center of tile
            Vector2 lc = GetToolPixelPosition(tile);
            
            // just before we get going
            if (t is null) Monitor.Log($"t is null", LogLevel.Warn);
            if (FakeFarmer is null) Monitor.Log($"FakeFarmer is null", LogLevel.Warn);
            if (FakeFarmer.currentLocation is null) Monitor.Log($"FakeFarmer.currentLocation is null", LogLevel.Warn);
            if (FakeFarmer.currentLocation.debris is null) Monitor.Log($"FakeFarmer.currentLocation.debris is null", LogLevel.Warn);

            t.DoFunction(location, (int) lc.X, (int) lc.Y, 0, FakeFarmer);
            return true;
        }

        protected bool FarmerUseToolOnTile(Tool tool, Vector2 tile, GameLocation location) {
            // use tool on center of tile
            Vector2 lc = GetToolPixelPosition(tile);
            tool.DoFunction(location, (int)lc.X, (int)lc.Y, 0, Game1.player);
            return true;
        }

        protected Vector2 GetToolPixelPosition(Vector2 tile) {
            return (tile * Game1.tileSize) + new Vector2(Game1.tileSize / 2f);
        }

        public List<int> RequiredItems() {
            return new List<int>();
        }
    }
}