using UnityEngine;
using VContainer;
using VContainer.Unity;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging; // For SerilogLoggerFactory
using TienLen.Unity.Infrastructure.Logging;
using TienLen.Unity.Infrastructure.Network; // Added
using TienLen.Unity.Infrastructure.Services; // Added
using Nakama; // Added
using System; 

namespace TienLen.Unity.Infrastructure
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameConfig _gameConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            Application.runInBackground = true; // Ensure game runs when window is not focused (essential for MP testing)
            GameLogger.Initialize();

            // Configuration
            if (_gameConfig == null)
            {
                throw new InvalidOperationException("GameConfig is missing in GameLifetimeScope! Please assign it in the Inspector of the GameLifetimeScope Prefab.");
            }
            builder.RegisterInstance(_gameConfig);

            // Bridge Serilog to Microsoft.Extensions.Logging
            builder.RegisterInstance<ILoggerFactory>(new SerilogLoggerFactory());
            builder.Register(typeof(GenericLogger<>), Lifetime.Singleton).As(typeof(ILogger<>));

            // Nakama Client (Singleton)
            builder.Register<IClient>(container => 
            {
                return new Client("http", _gameConfig.NakamaHost, _gameConfig.NakamaPort, _gameConfig.NakamaKey);
            }, Lifetime.Singleton);

            // Nakama Socket (Singleton)
            builder.Register<ISocket>(container =>
            {
                var client = container.Resolve<IClient>();
                return client.NewSocket();
            }, Lifetime.Singleton);

            // Infrastructure Services
            builder.Register<NakamaAuthService>(Lifetime.Singleton);
            builder.Register<NakamaSocketService>(Lifetime.Singleton);
            builder.Register<ISceneService, SceneService>(Lifetime.Singleton);

            // Network Client Adaptor
            builder.Register<IGameNetwork, NakamaGameNetwork>(Lifetime.Singleton);

            // Game Session & Lobby
            builder.Register<GameSession>(Lifetime.Singleton);
            builder.Register<LobbyService>(Lifetime.Singleton);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GameLogger.CloseAndFlush();
        }

    }
}