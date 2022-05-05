using System.Collections.Generic;
using StardewModdingAPI;

namespace MarketDay.Utility
{
    public class Mail : IAssetEditor
    {
        // This collection holds any letters loaded after the initial load or last cache refresh
        private readonly Dictionary<string, string> dynamicMail = new();
        
        public bool CanEdit<T>(IAssetInfo asset)
        {
            return asset.AssetNameEquals("Data\\mail");
        }

        public void Edit<T>(IAssetData asset)
        {
            var data = asset.AsDictionary<string, string>().Data;

            // This is just an example
            data["StaticMail"] = "If there were any letters with static content they could be placed here.";

            // Inject any mail that was added after the initial load.
            foreach (var item in dynamicMail) data.Add(item);
            dynamicMail.Clear();
        }

        /// <summary>
        /// Add a new mail asset into the collection so it can be injected by the next cache refresh.  The letter will
        /// not be available to send until the cache is invalidated in the code.
        /// </summary>
        /// <param name="mailId">The mail key</param>
        /// <param name="mailText">The mail text</param>
        public void Add(string mailId, string mailText)
        {
            if (string.IsNullOrEmpty(mailId)) return;
            if (dynamicMail.ContainsKey(mailId)) dynamicMail[mailId] = mailText;
            else dynamicMail.Add(mailId, mailText);
        }
    }
}