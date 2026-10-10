using FiresCore.Logging;

namespace FiresMonsterOverhaul
{
    // Tagged logger on Core's FiresLog engine. Per-action evidence lines go through Debug (shown only at
    // [Logging] Log Level = Debug or Verbose), so a default install stays quiet.
    internal static class MonsterLog
    {
        private static readonly FiresLog Log = new FiresLog("FiresMonsterOverhaul", () => false);

        public static bool DebugEnabled => FiresLogLevel.DebugOn;
        public static void Info(string message) => Log.Info(message);
        public static void Debug(string message) => Log.Debug(message);
        public static void Warning(string message) => Log.Warning(message);
        public static void Error(string message) => Log.Error(message);
    }
}
