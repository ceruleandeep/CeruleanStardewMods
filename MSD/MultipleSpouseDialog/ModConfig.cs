using System;
namespace MultipleSpouseDialog
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public int MinHeartsForChat { get; set; } = 9;
        public bool AllowSpousesToChat { get; set; } = true;
        public bool ChatWithPlayer { get; set; } = true;
        public float SpouseChatChance { get; set; } = 0.05f;
        public float MinDistanceToChat { get; set; } = 100f;
        public float MaxDistanceToChat { get; set; } = 350f;
        public float MinSpouseChatInterval { get; set; } = 10f;
        public bool PreventRelativesFromChatting { get; set; } = false;
        public bool ExtraDebugOutput { get; set; } = false;
    }
}
