using System.Diagnostics;
using Serilog;

namespace TienLen.Unity.Infrastructure.Logging
{
    public static class FastLog
    {
        [Conditional("ENABLE_LOGS")]
        public static void Info(string message) => Log.Information(message);

        [Conditional("ENABLE_LOGS")]
        public static void Info(string messageTemplate, params object[] args) => Log.Information(messageTemplate, args);

        [Conditional("ENABLE_LOGS")]
        public static void Warn(string message) => Log.Warning(message);

        [Conditional("ENABLE_LOGS")]
        public static void Error(string message) => Log.Error(message);
    }
}
