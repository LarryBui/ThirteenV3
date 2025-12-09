using UnityEngine;
using UnityEngine.UI;
using VContainer;
using TMPro;
using TienLen.Unity.Infrastructure.Services;

namespace TienLen.Unity.Presentation.Presenters
{
    public class LobbyPresenter : MonoBehaviour
    {
        public Button PlayNowButton;
        public TMP_Text StatusText;

        private LobbyService _lobbyService;
        private ISceneService _sceneService;

        [Inject]
        public void Construct(LobbyService lobbyService, ISceneService sceneService)
        {
            Debug.Log("[LobbyPresenter] Construct called.");
            _lobbyService = lobbyService;
            _sceneService = sceneService;
        }

        private void Start()
        {
            if (PlayNowButton != null) PlayNowButton.onClick.AddListener(OnPlayNowClicked);
        }

        private async void OnPlayNowClicked()
        {
            if (StatusText != null) StatusText.text = "Finding a table...";
            if (PlayNowButton != null) PlayNowButton.interactable = false;

            try
            {
                await _lobbyService.JoinOrCreateTableAsync();
                if (StatusText != null) StatusText.text = "Joined! Loading Game Room...";
                
                // Transition to GameRoom
                await _sceneService.LoadFeatureAsync(FeatureScene.GameRoom);
            }
            catch (System.Exception ex)
            {
                if (StatusText != null) StatusText.text = $"Error: {ex.Message}";
                if (PlayNowButton != null) PlayNowButton.interactable = true;
                Debug.LogError(ex);
            }
        }
        
        private void OnDestroy()
        {
            if (PlayNowButton != null) PlayNowButton.onClick.RemoveAllListeners();
        }
    }
}
