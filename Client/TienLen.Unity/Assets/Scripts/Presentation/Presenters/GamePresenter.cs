using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TienLen.Unity.Domain.Aggregates;
using TienLen.Unity.Domain.ValueObjects;
using TienLen.Unity.Domain.Enums;
using TienLen.Unity.Infrastructure;
using TienLen.Unity.Infrastructure.Network;
using TienLen.Unity.Infrastructure.Services;
using TienLen.Unity.Presentation.Views;
using TMPro;
using Microsoft.Extensions.Logging;
using VContainer;
using Nakama;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Presentation.Presenters
{
    public class GamePresenter : MonoBehaviour
    {
        [Header("UI References")]
        public HandView PlayerHandView;
        public Button PlayButton;
        public Button SkipButton;
        public Button StartGameButton; 
        public TMP_Text StatusText;
        public BoardView GameBoardView; 
        public SeatManager GameSeatManager; 

        private ILogger<GamePresenter> _logger;
        private IGameNetwork _network;
        private GameSession _gameSession;
        private GameModel _gameModel; // New

        [Inject]
        public void Construct(ILogger<GamePresenter> logger, IGameNetwork network, GameSession gameSession, GameModel gameModel)
        {
            _logger = logger;
            _network = network;
            _gameSession = gameSession;
            _gameModel = gameModel; // New
            _logger.LogInformation("GamePresenter constructed.");
        }

        private void Start()
        {
            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlayClicked);
            if (SkipButton != null) SkipButton.onClick.AddListener(OnSkipClicked);
            if (StartGameButton != null) StartGameButton.onClick.AddListener(OnStartGameClicked); 
            
            // Subscribe to GameModel Events
            _gameModel.OnHandUpdated += OnGameModelHandUpdated;
            _gameModel.OnBoardUpdated += OnGameModelBoardUpdated;
            _gameModel.OnActivePlayerChanged += OnGameModelActivePlayerChanged;
            _gameModel.OnMatchIdUpdated += OnGameModelMatchIdUpdated;
            _gameModel.OnMatchOwnerChanged += OnGameModelMatchOwnerChanged;
            _gameModel.OnIsPlayingChanged += OnGameModelIsPlayingChanged; // New subscription
            _gameModel.OnGameOver += OnGameModelGameOver;             // New subscription
            _gameModel.OnPlayerIdsUpdated += OnGameModelPlayerIdsUpdated; // New subscription
            
            // Unsubscribe from Network Events (NakamaGameNetwork now updates GameModel directly)
            // _network.OnMatchStart -= HandleMatchStart; // Removed
            // _network.OnTurnUpdate -= HandleTurnUpdate; // Removed
            if (_network != null)
                _network.OnError += HandleError; // Keep error handling at presenter level
            
            // GameRoom Initialization Logic
            InitializeGameRoom();
            UpdateStartGameButtonVisibility(); // Initial call to set button state
        }

        private void InitializeGameRoom()
        {
            if (_gameSession.CurrentRoom == null)
            {
                _logger.LogError("GameRoom loaded without a valid match in GameSession!");
                if (StatusText) StatusText.text = "Error: No active room!";
                return;
            }

            // Update GameModel's match ID
            _gameModel.SetMatchId(_gameSession.CurrentRoom.Id);
            if (StatusText) StatusText.text = $"Room: {_gameModel.MatchId}";
            _logger.LogInformation($"Game Room initialized. Room ID: {_gameModel.MatchId}");


            // Setup Seat Manager
            if (GameSeatManager != null && _gameSession.CurrentRoom != null)
            {
                string localUserId = _gameSession.CurrentRoom.Self.UserId;
                List<IUserPresence> otherPlayers = _gameSession.ConnectedPlayers;
                GameSeatManager.SetupPlayerSeats(localUserId, otherPlayers);
            }

            // The button visibility is now handled by UpdateStartGameButtonVisibility
            // which subscribes to GameModel.OnMatchOwnerChanged.
            // if (!_gameSession.IsHost) // This check is no longer needed here
            // {
            //     _logger.LogInformation("Waiting for host to start the game...");
            // }
        }

        // GameModel Event Handlers
        private void OnGameModelHandUpdated(Hand hand)
        {
            _logger.LogInformation($"GameModel Hand Updated! Received Hand with {hand.Cards.Count} cards.");
            PlayerHandView.RenderHand(hand.Cards);
            if (_gameModel.ActivePlayerId == _gameSession.CurrentRoom.Self.UserId)
            {
                if (StatusText) StatusText.text = "Game Started! Your Turn!";
            }
        }

        private void OnGameModelBoardUpdated(List<Card> board)
        {
            _logger.LogInformation($"GameModel Board Updated! Cards on board: {board.Count}");
            if (GameBoardView != null)
            {
                GameBoardView.ClearBoard();
                if (board.Any())
                {
                    _logger.LogInformation($"Cards on Board: {board.Count}");
                    // TODO: Implement RenderPlayedCards on BoardView
                    // Example: GameBoardView.RenderPlayedCards(board);
                }
            }
        }

        private void OnGameModelActivePlayerChanged(string activePlayerId)
        {
            _logger.LogInformation($"GameModel Active Player Changed: {activePlayerId}");
            if (StatusText)
            {
                if (activePlayerId == _gameSession.CurrentRoom.Self.UserId)
                {
                    StatusText.text = "Your Turn!";
                }
                else
                {
                    StatusText.text = $"It's {GameSeatManager.GetPlayerName(activePlayerId)}'s Turn.";
                }
            }
        }

        private void OnGameModelMatchIdUpdated(string matchId)
        {
            _logger.LogInformation($"GameModel Match ID Updated: {matchId}");
            if (StatusText) StatusText.text = $"Room: {matchId}";
        }

        private void OnGameModelMatchOwnerChanged(string newOwnerId)
        {
            _logger.LogInformation($"GameModel Match Owner Changed: {newOwnerId}");
            UpdateStartGameButtonVisibility();
        }
        
        private void OnGameModelIsPlayingChanged(bool isPlaying)
        {
            _logger.LogInformation($"GameModel IsPlaying Changed: {isPlaying}");
            // Based on isPlaying, we might want to enable/disable Play/Skip buttons or show/hide hand
            PlayerHandView.gameObject.SetActive(isPlaying);
            PlayButton.gameObject.SetActive(isPlaying);
            SkipButton.gameObject.SetActive(isPlaying);

            if (!isPlaying) // Game over or not started
            {
                if (StartGameButton != null)
                {
                    UpdateStartGameButtonVisibility(); // Re-evaluate if owner should see Start Game button
                }
            }
        }

        private void OnGameModelGameOver(string winnerId)
        {
            _logger.LogInformation($"Game Over! Winner: {winnerId}");
            string localUserId = _gameSession.CurrentRoom?.Self?.UserId;
            if (StatusText != null)
            {
                if (!string.IsNullOrEmpty(winnerId) && winnerId == localUserId)
                {
                    StatusText.text = "You Win!";
                }
                else if (!string.IsNullOrEmpty(winnerId))
                {
                    StatusText.text = $"{GameSeatManager.GetPlayerName(winnerId)} Wins!";
                }
                else
                {
                    StatusText.text = "Game Over!"; // No specific winner, e.g., if match terminated early
                }
            }
            // Trigger UI for game over, maybe show a replay button
            // StartGameButton visibility is handled by OnIsPlayingChanged -> UpdateStartGameButtonVisibility
        }

        private void OnGameModelPlayerIdsUpdated(IReadOnlyList<string> playerIds)
        {
            _logger.LogInformation($"GameModel Player IDs Updated: {string.Join(", ", playerIds)}");
            // This is primarily for the SeatManager to update player positions/avatars/etc.
            // When a late-joiner connects, their GameSeatManager needs to be set up.
            // We need to fetch UserPresences from CurrentNakamaMatch (if available) to reconstruct
            // I'll update the `InitializeGameRoom` logic to take into account `_gameModel.PlayerIds`.
            // For now, this just logs, as `GameSeatManager.SetupPlayerSeats` needs IUserPresence.
            // I'll need to pass the full list of presences to GameSeatManager.SetupPlayerSeats
            // The `GameSeatManager.SetupPlayerSeats` will be refactored later to use the presences directly from the network or a combined list.
        }

        private void UpdateStartGameButtonVisibility()
        {
            if (StartGameButton == null) return;

            string localUserId = _gameSession.CurrentRoom?.Self?.UserId;
            bool isLocalPlayerOwner = !string.IsNullOrEmpty(localUserId) && localUserId == _gameModel.MatchOwnerId;
            
            // Only show StartGameButton if local player is owner AND game is NOT playing
            StartGameButton.gameObject.SetActive(isLocalPlayerOwner && !_gameModel.IsPlaying);

            if (!isLocalPlayerOwner)
            {
                _logger.LogInformation("Waiting for owner to start the game...");
            }
        }

        private async void OnStartGameClicked() 
        {
            _logger.LogInformation("Start Game clicked. Sending request to server...");
            try
            {
                await _network.SendStartMatchAsync();
                // Button will be hidden by OnGameModelMatchOwnerChanged event
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to send start match request.");
                if (StatusText) StatusText.text = "Error starting game!";
            }
        }
        
        private void HandleError(string error)
        {
             _logger.LogError("Network Error: {Error}", error);
             if (StatusText) StatusText.text = $"Error: {error}";
        }

        private async void OnPlayClicked()
        {
            if (PlayerHandView == null) return;

            var selectedCards = PlayerHandView.GetSelectedCards();
            if (selectedCards.Count == 0)
            {
                _logger.LogWarning("No cards selected to play.");
                if (StatusText) StatusText.text = "Select cards to play!";
                return;
            }

            // Map selected cards to their indices in the GameModel's hand
            var currentHand = _gameModel.PlayerHand;
            var selectedIndices = new List<int>();

            foreach (var card in selectedCards)
            {
                // Find index of this card in the source hand
                // Note: This relies on card equality (Suit/Rank). 
                // Since duplicates aren't allowed in a standard deck, this is safe.
                int index = -1;
                for (int i = 0; i < currentHand.Count; i++)
                {
                    if (currentHand[i].Suit == card.Suit && currentHand[i].Rank == card.Rank)
                    {
                        index = i;
                        break;
                    }
                }

                if (index != -1)
                {
                    selectedIndices.Add(index);
                }
                else
                {
                    _logger.LogError($"Selected card {card.Rank}-{card.Suit} not found in GameModel hand!");
                }
            }

            if (selectedIndices.Count > 0)
            {
                _logger.LogInformation($"Playing {selectedIndices.Count} cards: indices [{string.Join(",", selectedIndices)}]");
                try
                {
                    await _network.SendPlayCardAsync(selectedIndices);
                }
                catch (System.Exception ex)
                {
                     _logger.LogError(ex, "Failed to send play card request.");
                     HandleError("Failed to play cards.");
                }
            }
        }

        private async void OnSkipClicked()
        {
            _logger.LogInformation("Skip Turn clicked. Sending empty play request.");
            try
            {
                // Sending empty list implies "Skip"
                await _network.SendPlayCardAsync(new List<int>());
            }
            catch (System.Exception ex)
            {
                 _logger.LogError(ex, "Failed to send skip request.");
                 HandleError("Failed to skip turn.");
            }
        }
        
        private void OnDestroy()
        {
            if (PlayButton != null) PlayButton.onClick.RemoveAllListeners();
            if (SkipButton != null) SkipButton.onClick.RemoveAllListeners();
            if (StartGameButton != null) StartGameButton.onClick.RemoveAllListeners();
            
            if (_gameModel != null) // Unsubscribe from GameModel events
            {
                _gameModel.OnHandUpdated -= OnGameModelHandUpdated;
                _gameModel.OnBoardUpdated -= OnGameModelBoardUpdated;
                _gameModel.OnActivePlayerChanged -= OnGameModelActivePlayerChanged;
                _gameModel.OnMatchIdUpdated -= OnGameModelMatchIdUpdated;
                _gameModel.OnMatchOwnerChanged -= OnGameModelMatchOwnerChanged;
                _gameModel.OnIsPlayingChanged -= OnGameModelIsPlayingChanged; // New unsubscription
                _gameModel.OnGameOver -= OnGameModelGameOver;             // New unsubscription
                _gameModel.OnPlayerIdsUpdated -= OnGameModelPlayerIdsUpdated; // New unsubscription
            }

            if (_network != null)
            {
                _network.OnError -= HandleError;
            }
        }
    }
}
