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
            
            // Unsubscribe from Network Events (NakamaGameNetwork now updates GameModel directly)
            // _network.OnMatchStart -= HandleMatchStart; // Removed
            // _network.OnTurnUpdate -= HandleTurnUpdate; // Removed
            if (_network != null)
                _network.OnError += HandleError; // Keep error handling at presenter level
            
            // GameRoom Initialization Logic
            InitializeGameRoom();
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

            _logger.LogInformation($"This client is {( _gameSession.IsHost ? "the HOST" : "a CLIENT")}.");

            // Setup Seat Manager
            if (GameSeatManager != null && _gameSession.CurrentRoom != null)
            {
                string localUserId = _gameSession.CurrentRoom.Self.UserId;
                List<IUserPresence> otherPlayers = _gameSession.ConnectedPlayers;
                GameSeatManager.SetupPlayerSeats(localUserId, otherPlayers);
            }

            // Host logic: Show Start Button if Host
            if (StartGameButton != null)
            {
                StartGameButton.gameObject.SetActive(_gameSession.IsHost);
            }

            if (!_gameSession.IsHost)
            {
                _logger.LogInformation("Waiting for host to start the game...");
            }
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

        private async void OnStartGameClicked() 
        {
            _logger.LogInformation("Start Game clicked. Sending request to server...");
            try
            {
                await _network.SendStartMatchAsync();
                if (StartGameButton) StartGameButton.gameObject.SetActive(false);
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

        private void OnPlayClicked()
        {
            // TODO: Get selected cards from HandView
            // await _network.SendPlayCardAsync(selectedIndices);
             _logger?.LogInformation("Play clicked (not yet implemented)");
        }

        private void OnSkipClicked()
        {
            _logger?.LogInformation("Skip Turn clicked (not yet implemented)");
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
            }

            if (_network != null)
            {
                _network.OnError -= HandleError;
            }
        }
    }
}
