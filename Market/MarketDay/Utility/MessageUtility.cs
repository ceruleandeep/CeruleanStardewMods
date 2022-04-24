using StardewValley;

namespace MarketDay.Utility
{
    public class MessageUtility
    {
        public static void SendMessage(string msg) {
            if (!MarketDay.Config.ReceiveMessages) return;

            Game1.addHUDMessage(new HUDMessage(msg, 3) {
                noIcon = true,
                timeLeft = HUDMessage.defaultTime
            });

            // try {
            //     var multiplayer = Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
            //     multiplayer.broadcastGlobalMessage("Strings\\StringsFromCSFiles:"+msg);
            // }
            // catch (InvalidOperationException) {
            //     BetterJunimos.SMonitor.Log($"SendMessage: multiplayer unavailable", LogLevel.Error);
            // }
        }
    }
}