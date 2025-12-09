using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Infrastructure.Services
{
    public class SceneService : ISceneService
    {
        private readonly GameConfig _config;

        public SceneService(GameConfig config) 
        {
            _config = config;
        }

        public async UniTask LoadMasterShellAsync()
        {
            Debug.Log($"[SceneService] Attempting to load Master Shell: {_config.MasterSceneName}");
            
            // Only load if not already loaded
            if (!SceneManager.GetSceneByName(_config.MasterSceneName).isLoaded)
            {
                await SceneManager.LoadSceneAsync(_config.MasterSceneName, LoadSceneMode.Additive).ToUniTask();
                Debug.Log("[SceneService] Master Shell loaded successfully.");
            }
            else
            {
                 Debug.Log("[SceneService] Master Shell was already loaded.");
            }
        }

        public async UniTask LoadFeatureAsync(FeatureScene scene)
        {
            string sceneName = ResolveSceneName(scene);
            
            // Note: We no longer need LifetimeScope.EnqueueParent() because 
            // VContainer's "Root Lifetime Scope" setting automatically parents 
            // any new LifetimeScope in the scene to the Root scope.
            
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive).ToUniTask();
            
            // Optional: Set active scene
            var loadedScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(loadedScene);
        }

        private string ResolveSceneName(FeatureScene scene)
        {
            return scene switch
            {
                FeatureScene.GameRoom => _config.GameRoomSceneName,
                FeatureScene.Lobby => _config.LobbySceneName,
                _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
            };
        }
    }
}