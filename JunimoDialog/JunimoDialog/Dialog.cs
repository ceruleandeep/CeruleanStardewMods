using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContentPatcher;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;

namespace JunimoDialog
{
    public class ConfigDialog
    {
        public string Dialog;
        public string Emotion;
        public string Situation;
        public Dictionary<string, string> When;
        internal IManagedConditions Conditions;
    }

    static class Dialog
    {
        private static List<ConfigDialog> Data;
        private static ConfigDialog NoData;
        private static Random rand;
        private static IManifest manifest;
        private static IModHelper Helper;
        public static IContentPatcherAPI cp_api;

        public static void Initialize(IManifest mf, IModHelper helper)
        {
            manifest = mf;
            Helper = helper;
            rand = new Random();
            
            NoData = new ConfigDialog
            {
                Dialog = "Hi, farmer!",
                When = new Dictionary<string, string>()
            };

            Data = new List<ConfigDialog>();
        }

        public static void Load(string directory)
        {
            if (cp_api == null)
            {
                JunimoDialog.SMonitor.Log($"Content patcher API not available; this won't end well", LogLevel.Error);
            }

            var files = Directory.GetFiles(directory, "*.json").ToList();
            foreach (var fullFilename in files.Where(fullFilename => !fullFilename.EndsWith("manifest.json")))
            {
                LoadFile(fullFilename);
            }
        }
        
        private static void LoadFile(string filename)
        {
            JunimoDialog.SMonitor.Log($"Reading dialog from {filename}");

            try
            {
                var fileContents = File.ReadAllText(filename);
                var entries = JsonConvert.DeserializeObject<List<ConfigDialog>>(fileContents);
                if (entries == null) return;
                foreach (var dialog in entries)
                {
                    dialog.Dialog ??= "";
                    dialog.Emotion ??= "";
                    dialog.Situation ??= "";
                    Data.Add(dialog);

                    if (JunimoDialog.Config.ExtraDebugOutput)
                        JunimoDialog.SMonitor.Log($"Dialog loaded: {dialog.Dialog}: {dialog.Emotion}");
                }
            }
            catch (Exception e)
            {
                JunimoDialog.SMonitor.Log($"Error reading from config file {filename}: {e.Message}", LogLevel.Debug);
            }
        }

        public static string GetDialog(int harvestTimer)
        {
            double roll = JunimoDialog.jdRandom.NextDouble();
            if (roll > JunimoDialog.Config.DialogChance) return null;

            List<string> emotions = new List<string>();
            if (JunimoDialog.Config.Happy) emotions.Add("Happy");
            if (JunimoDialog.Config.Grumpy) emotions.Add("Grumpy");
            if (!emotions.Any()) return null;
            string emotion = emotions[JunimoDialog.jdRandom.Next(emotions.Count)];

            // 300, 998: other activity succeeded
            // 2000: harvest succeeded
            // 0, 5, 200: nothing to do
            string situation = harvestTimer < 300 ? "Idle" : "Working";
            
            string dialog = RandomDialog(situation, emotion).Dialog;
            dialog = GetTranslation(dialog).Replace("@", Game1.player.displayName);
            return dialog;
        }
        
        private static string GetTranslation(string text_key) {
            string prompt = Helper.Translation.Get(text_key).UsePlaceholder(false);
            if (prompt == null)
            {
                JunimoDialog.SMonitor.Log($"No translation for {text_key}", LogLevel.Debug);
                return text_key;
            }
            return prompt;
        }
        
        private static ConfigDialog RandomDialog(string situation="Working", string emotion="Grumpy")
        {
            var candidates = Data
                .Where(dialog => dialog.Emotion.Split(' ').Contains(emotion) || dialog.Emotion == "")
                .Where(dialog => dialog.Situation.Split(' ').Contains(situation) || dialog.Situation == "")
                .ToList();
            
            foreach (ConfigDialog dialog in candidates)
            {
                if (dialog.Conditions == null)
                {
                    dialog.Conditions = cp_api.ParseConditions(
                        manifest: manifest,
                        rawConditions: dialog.When,
                        formatVersion: new SemanticVersion("1.20.0")
                    );
                }
                else
                {
                    dialog.Conditions.UpdateContext();
                }
            }

            List<ConfigDialog> matched = candidates.Where(Dialog => Dialog.Conditions.IsMatch).ToList();

            if (!matched.Any())
            {
                if (JunimoDialog.Config.ExtraDebugOutput)
                    JunimoDialog.SMonitor.Log($"Could not find any candidate dialog for {emotion} {situation}", LogLevel.Error);
                return NoData;
            }

            if (JunimoDialog.Config.ExtraDebugOutput)
                JunimoDialog.SMonitor.Log($"{matched.Count} candidate dialogs for {emotion} {situation}", LogLevel.Debug);
            int r = rand.Next(matched.Count);
            return matched[r];
        }
    }
}