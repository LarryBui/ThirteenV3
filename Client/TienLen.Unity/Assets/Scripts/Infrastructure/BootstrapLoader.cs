using UnityEngine;
using VContainer;
using Cysharp.Threading.Tasks;
using TienLen.Unity.Infrastructure.Network;
using TienLen.Unity.Infrastructure.Services;

namespace TienLen.Unity.Infrastructure
{
    public class BootstrapLoader : MonoBehaviour
    {
        private NakamaAuthService _authService;
        private NakamaSocketService _socketService;
        private ISceneService _sceneService;

        [Inject]
        public void Construct(NakamaAuthService auth, NakamaSocketService socket, ISceneService sceneService)
        {
            _authService = auth;
            _socketService = socket;
            _sceneService = sceneService;
            
            // Start the logic immediately after injection is complete
            StartApp();
        }

        // Removed standard Unity Start() to avoid race conditions
        private async void StartApp()
        {
            try
            {
                // 1. Load Master Shell (Camera, Loading Screen)
                await _sceneService.LoadMasterShellAsync();

                // 2. Authenticate with a unique custom user per run (test-friendly)
                var session = await _authService.AuthenticateCustomUniqueAsync();

                // 3. Connect Socket
                await _socketService.ConnectAsync(session);

                // 4. Load Lobby Feature
                await _sceneService.LoadFeatureAsync(FeatureScene.Lobby);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Bootstrap] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
