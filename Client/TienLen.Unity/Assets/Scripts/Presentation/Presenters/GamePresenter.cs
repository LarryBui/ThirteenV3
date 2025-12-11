using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TienLen.Unity.Domain.Aggregates;
using TienLen.Unity.Domain.ValueObjects;
using TienLen.Unity.Domain.Enums;
using TienLen.Unity.Domain.Services;
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
        public AvatarView LocalPlayerAvatar; // New Avatar reference
        public Button PlayButton;
        public Button SkipButton;
        public Button StartGameButton; 
        public TMP_Text StatusText;
        public BoardView GameBoardView; 
        public SeatManager GameSeatManager; 

        /// <summary>
        /// Prefab used to render a player's avatar at a seat anchor.
        /// </summary>
        public AvatarView AvatarPrefab; // Prefab to spawn per seat

        /// <summary>
        /// Seat anchor transforms ordered by seat index; used to place avatars.
        /// </summary>
        public Transform[] SeatAvatarAnchors; // Seat index -> anchor transform

        private Dictionary<Card, Vector3> _pendingPlayedCardOrigins;
        private ILogger<GamePresenter> _logger;
        private IGameNetwork _network;
        private GameSession _gameSession;
        private GameModel _gameModel; // New
        private AvatarView[] _seatAvatars;

        [Inject]
        public void Construct(ILogger<GamePresenter> logger, IGameNetwork network, GameSession gameSession, GameModel gameModel)
        {
            _logger = logger;
            _network = network;
            _gameSession = gameSession;
            _gameModel = gameModel; // New
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
            _gameModel.OnSeatsUpdated += OnGameModelSeatsUpdated;
            
            // Unsubscribe from Network Events (NakamaGameNetwork now updates GameModel directly)
            // _network.OnMatchStart -= HandleMatchStart; // Removed
            // _network.OnTurnUpdate -= HandleTurnUpdate; // Removed
            if (_network != null)
                _network.OnError += HandleError; // Keep error handling at presenter level
            
            // GameRoom Initialization Logic
            InitializeGameRoom();
            UpdateStartGameButtonVisibility(); // Initial call to set button state
            OnGameModelSeatsUpdated(_gameModel.Seats); // Render any initial seat state
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
                
                // Initialize Local Avatar
                if (LocalPlayerAvatar != null)
                {
                    LocalPlayerAvatar.SetName(_gameSession.CurrentRoom.Self.Username);
                    // LocalPlayerAvatar.SetAvatar(...); // To be implemented when avatar assets are available
                }
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

        private async void OnGameModelBoardUpdated(List<Card> board)
        {
            _logger.LogInformation($"GameModel Board Updated! Cards on board: {board.Count}");
            if (GameBoardView != null)
            {
                if (board == null || board.Count == 0)
                {
                    GameBoardView.ClearBoard();
                }
                else
                {
                    _logger.LogInformation($"Cards on Board: {board.Count}");

                    var animateFromHand = ShouldAnimateFromHand(board);
                    var fallbackStart = GameBoardView.PlayedCardsContainer != null
                        ? GameBoardView.PlayedCardsContainer.position
                        : Vector3.zero;

                    if (animateFromHand && PlayerHandView != null)
                    {
                        fallbackStart = PlayerHandView.GetHandCenterWorldPosition();
                    }

                    await GameBoardView.AnimateAndAddPlayedCards(
                        board,
                        fallbackStart,
                        animateFromHand ? _pendingPlayedCardOrigins : null);
                }
            }
            _pendingPlayedCardOrigins = null;

            // Check if it's the local player's turn and update Skip button visibility
            string localUserId = _gameSession.CurrentRoom?.Self?.UserId;
            if (localUserId == _gameModel.ActivePlayerId)
            {
                UpdateSkipButtonHighlight();
            }
        }

        private bool ShouldAnimateFromHand(List<Card> board)
        {
            if (_pendingPlayedCardOrigins == null || board == null)
            {
                return false;
            }

            if (board.Count != _pendingPlayedCardOrigins.Count)
            {
                return false;
            }

            foreach (var card in board)
            {
                if (!_pendingPlayedCardOrigins.ContainsKey(card))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnGameModelActivePlayerChanged(string activePlayerId)
        {
            _logger.LogInformation($"GameModel Active Player Changed: {activePlayerId}");
            
            string localUserId = _gameSession.CurrentRoom?.Self?.UserId;
            bool isLocalPlayerTurn = activePlayerId == localUserId;
            
            if (StatusText)
            {
                if (isLocalPlayerTurn)
                {
                    StatusText.text = "Your Turn!";
                }
                else
                {
                    StatusText.text = $"It's {GameSeatManager.GetPlayerName(activePlayerId)}'s Turn.";
                }
            }

            // Check if it's the local player's turn and update Skip button visibility
            if (isLocalPlayerTurn)
            {
                UpdateSkipButtonHighlight();
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

            // Update start game button visibility when playing state changes
            UpdateStartGameButtonVisibility();
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

        /// <summary>
        /// Renders or hides avatars when the authoritative seat list changes.
        /// </summary>
        /// <param name="seats">Seat array where index maps to anchor and value is userId.</param>
        private void OnGameModelSeatsUpdated(IReadOnlyList<string> seats)
        {
            if (seats == null) return;
            if (SeatAvatarAnchors == null || SeatAvatarAnchors.Length == 0)
            {
                _logger.LogWarning("Seat anchors not configured for avatars.");
                return;
            }
            if (AvatarPrefab == null)
            {
                _logger.LogWarning("AvatarPrefab not assigned; cannot render avatars.");
                return;
            }

            if (_seatAvatars == null || _seatAvatars.Length != SeatAvatarAnchors.Length)
            {
                _seatAvatars = new AvatarView[SeatAvatarAnchors.Length];
            }

            var presenceLookup = new Dictionary<string, IUserPresence>();
            if (_gameSession?.CurrentRoom?.Self != null)
            {
                presenceLookup[_gameSession.CurrentRoom.Self.UserId] = _gameSession.CurrentRoom.Self;
            }
            foreach (var presence in _gameSession.ConnectedPlayers)
            {
                if (presence != null)
                {
                    presenceLookup[presence.UserId] = presence;
                }
            }

            int count = Mathf.Min(seats.Count, SeatAvatarAnchors.Length);
            for (int i = 0; i < count; i++)
            {
                var anchor = SeatAvatarAnchors[i];
                if (anchor == null) continue;

                var userId = seats[i];
                if (string.IsNullOrEmpty(userId))
                {
                    if (_seatAvatars[i] != null)
                    {
                        _seatAvatars[i].gameObject.SetActive(false);
                    }
                    continue;
                }

                var avatar = _seatAvatars[i];
                if (avatar == null)
                {
                    avatar = Instantiate(AvatarPrefab, anchor, worldPositionStays: false);
                    _seatAvatars[i] = avatar;
                }
                else
                {
                    avatar.transform.SetParent(anchor, false);
                    avatar.gameObject.SetActive(true);
                }

                if (presenceLookup.TryGetValue(userId, out var presence))
                {
                    avatar.SetName(presence.Username);
                }
                else
                {
                    avatar.SetName("Player");
                }
                // avatar.SetAvatar(sprite); // Hook up when avatars per user are available
            }

            // Hide any unused cached avatars beyond current seat count
            for (int i = count; i < SeatAvatarAnchors.Length; i++)
            {
                if (_seatAvatars[i] != null)
                {
                    _seatAvatars[i].gameObject.SetActive(false);
                }
            }
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
                // Waiting for owner to start the game...
            }
        }

        private void UpdateSkipButtonHighlight()
        {
            if (SkipButton == null) return;

            // Check if player has any valid moves against the current board
            bool hasValidMove = CardValidationHelper.HasValidMove(_gameModel.PlayerHand, _gameModel.CurrentBoard);

            if (!hasValidMove)
            {
                // Player has no valid moves - highlight/enable Skip button
                // Player has no valid moves. Highlighting Skip button.
                
                // Set button to interactable state
                SkipButton.interactable = true;

                // Highlight the button with a color shift (e.g., brighter/saturated color)
                var colors = SkipButton.colors;
                colors.normalColor = new Color(1f, 1f, 0.7f, 1f); // Yellowish highlight
                SkipButton.colors = colors;

                // Update status text to guide player
                if (StatusText)
                {
                    StatusText.text = "No valid moves. Click Skip to pass.";
                }
            }
            else
            {
                // Player has valid moves - reset Skip button to normal state
                // Player has valid moves available.
                
                // Reset button color to default
                var colors = SkipButton.colors;
                colors.normalColor = Color.white; // Default color
                SkipButton.colors = colors;
                
                // StatusText will be set by other methods like OnGameModelActivePlayerChanged
            }
        }

        private async void OnStartGameClicked() 
        {try
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
            {// No cards selected to play.\nif (StatusText) StatusText.text = "Select cards to play!";
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
                if (PlayerHandView != null)
                {
                    _pendingPlayedCardOrigins = PlayerHandView.GetSelectedCardWorldPositions();
                }
                try
                {
                    await _network.SendPlayCardAsync(selectedIndices);
                }
                catch (System.Exception ex)
                {
                     _logger.LogError(ex, "Failed to send play card request.");
                     HandleError("Failed to play cards.");
                     _pendingPlayedCardOrigins = null;
                }
            }
        }

        private async void OnSkipClicked()
        {// Immediately hide action buttons and show "Passed" status locally
            // if (PlayButton != null) PlayButton.gameObject.SetActive(false);
            // if (SkipButton != null) SkipButton.gameObject.SetActive(false);
            if (StatusText != null) StatusText.text = "You Passed!";
            
            try
            {
                await _network.SendPassAsync();
            }
            catch (System.Exception ex)
            {
                 _logger.LogError(ex, "Failed to send skip request.");
                 HandleError("Failed to skip turn.");
                 // Re-enable buttons on error so player can retry
                //  if (PlayButton != null) PlayButton.gameObject.SetActive(true);
                //  if (SkipButton != null) SkipButton.gameObject.SetActive(true);
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
                _gameModel.OnSeatsUpdated -= OnGameModelSeatsUpdated;
            }

            if (_network != null)
            {
                _network.OnError -= HandleError;
            }
        }
    }
}
