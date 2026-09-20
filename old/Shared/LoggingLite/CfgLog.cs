using MarcoZechner.LoggingLite;

namespace MarcoZechner.ConfigAPI.Scripts.ConfigAPI.Shared
{
    public sealed class CfgLog : LogBase<CfgLog>
    {
        protected override string FileName => "Main.ConfigAPI.log";

        protected override void Configure(LogConfig c)
        {
            c.ChatName = "ConfigAPI";
            c.WarningInChat = true;
            c.ErrorInChat = true;
            c.InfoInChat = false;

            c.DebugEnabled = true;
            c.DebugInChat = false;

            c.MaxLineChars = 400;
        }
    }

    public sealed class CfgLogWorld : LogBase<CfgLogWorld>
    {
        protected override string FileName => "Main.ConfigAPIWorld.log";
        protected override bool SaveInWorld => true;

        protected override void Configure(LogConfig c)
        {
            c.ChatName = "ConfigAPIWorld";
            c.WarningInChat = true;
            c.ErrorInChat = true;
            c.InfoInChat = false;

            c.DebugEnabled = true;
            c.DebugInChat = false;

            c.MaxLineChars = 400;
        }
    }
}