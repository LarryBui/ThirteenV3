using UnityEngine;
using Serilog;
using Serilog.Formatting.Json;
using Serilog.Sinks.Unity3D; // Requires Unity3DSink.cs
using System.IO;

namespace TienLen.Unity.Infrastructure.Logging
{
    public static class GameLogger
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext();

            // 1. Unity Editor Sink
            if (Debug.isDebugBuild || Application.isEditor)
            {
                // Using the extension method from Unity3DSink.cs
                logConfig.WriteTo.Unity3D(outputTemplate: "[{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            }

            // 2. File Sink (also in Editor so we can inspect logs easily)
            string logPath = Path.Combine(Application.persistentDataPath, "Logs", "game_log.json");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? Application.persistentDataPath);
            logConfig.WriteTo.File(
                formatter: new JsonFormatter(),
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true
            );

            Log.Logger = logConfig.CreateLogger();
            _isInitialized = true;

            Log.Information("GameLogger Initialized. Persistent logs at {LogPath}", logPath);
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
