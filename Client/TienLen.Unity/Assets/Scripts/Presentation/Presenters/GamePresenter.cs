using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TienLen.Core.Domain.Aggregates;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Core.Rules;
using TienLen.Unity.Infrastructure;
using TienLen.Unity.Infrastructure.Network; // Changed to IGameNetwork
using TienLen.Unity.Infrastructure.Services;
using TienLen.Unity.Presentation.Views;
using TMPro;
using Microsoft.Extensions.Logging;
using VContainer;
using Nakama;
using System.Linq;
using Cysharp.Threading.Tasks; // NEW

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
        private IGameNetwork _network; // Changed to IGameNetwork
        private GameSession _gameSession;
        
        // Local State
        private Hand _myHand;
        private List<Card> _currentBoard;
        private string _currentMatchId;

        [Inject]
        public void Construct(ILogger<GamePresenter> logger, IGameNetwork network, GameSession gameSession) // Changed IGameNetworkClient to IGameNetwork
        {
            _logger = logger;
            _network = network;
            _gameSession = gameSession;
            _logger.LogInformation("GamePresenter constructed.");
        }

        private void Start()
        {
            _myHand = new Hand();
            _currentBoard = new List<Card>();

            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlayClicked);
            if (SkipButton != null) SkipButton.onClick.AddListener(OnSkipClicked);
            if (StartGameButton != null) StartGameButton.onClick.AddListener(OnStartGameClicked); 
            
            // Subscribe to Network Events
            _network.OnMatchStart += HandleMatchStart; // Subscribed to new event
            _network.OnTurnUpdate += HandleTurnUpdate; // Subscribed to new event
            _network.OnError += HandleError;
            
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

            _currentMatchId = _gameSession.CurrentRoom.Id;
            if (StatusText) StatusText.text = $"Room: {_currentMatchId}";
            _logger.LogInformation($"Game Room initialized. Room ID: {_currentMatchId}");

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

        // NEW: Button Handler
        private async void OnStartGameClicked() 
        {
            _logger.LogInformation("Start Game clicked. Sending request to server...");
            try
            {
                await _network.SendStartMatchAsync(); // Send request to server
                if (StartGameButton) StartGameButton.gameObject.SetActive(false); // Hide button immediately
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to send start match request.");
                if (StatusText) StatusText.text = "Error starting game!";
            }
        }
        
        // Removed DealCardsAsHost() - this is now handled by the server.

        private void HandleMatchStart(TienLen.Gen.MatchStartPacket packet) // New event handler
        {
            _logger.LogInformation($"Match Started! Received Hand with {packet.Hand.Count} cards.");
            _myHand = new Hand();
            foreach (var protoCard in packet.Hand)
            {
                // Convert Protobuf Card to Domain Card
                _myHand.AddCard(new Card((TienLen.Core.Domain.Enums.Suit)protoCard.Suit, (TienLen.Core.Domain.Enums.Rank)protoCard.Rank));
            }
            _myHand.Sort(); // Client-side sort for display
            PlayerHandView.RenderHand(_myHand.Cards);
            if (StatusText) StatusText.text = "Game Started! Your Turn!"; // This will be updated by turn updates
        }

        private void HandleTurnUpdate(TienLen.Gen.TurnUpdatePacket packet) // New event handler
        {
            _logger.LogInformation($"Turn Update: Active Player {packet.ActivePlayerId}, Cards on board: {packet.LastPlayedCards.Count}");
            
            // Convert Proto Cards to Domain Cards
            _currentBoard.Clear();
            foreach (var protoCard in packet.LastPlayedCards)
            {
                _currentBoard.Add(new Card((TienLen.Core.Domain.Enums.Suit)protoCard.Suit, (TienLen.Core.Domain.Enums.Rank)protoCard.Rank));
            }
            
            // Render Board
            if (GameBoardView != null && GameSeatManager != null)
            {
                // This logic is simplified, would ideally use GameBoardView.RenderPlayedCards(currentBoard)
                // For now, let's just make sure we update the board state visually.
                // Re-rendering everything might be expensive, optimize later.
                GameBoardView.ClearBoard();
                if (_currentBoard.Any())
                {
                    // Assuming GameBoardView has a method to display cards
                    // For now, just logging
                    _logger.LogInformation($"Cards on Board: {_currentBoard.Select(c => c.ToString()).Aggregate((a, b) => a + ", " + b)}");
                }
            }
            
            if (StatusText)
            {
                if (packet.ActivePlayerId == _gameSession.CurrentRoom.Self.UserId)
                {
                    StatusText.text = "Your Turn!";
                }
                else
                {
                    StatusText.text = $"It's {GameSeatManager.GetPlayerName(packet.ActivePlayerId)}'s Turn.";
                }
            }
        }
        
        private void HandleError(string error)
        {
             _logger.LogError("Network Error: {Error}", error);
             if (StatusText) StatusText.text = $"Error: Failed to edit, 0 occurrences found for old_string (using UnityEngine;\nusing UnityEngine.UI;\nusing System.Collections.Generic;\nusing TienLen.Core.Domain.Aggregates;\nusing TienLen.Core.Domain.ValueObjects;\nusing TienLen.Core.Rules;\nusing TienLen.Unity.Infrastructure;\nusing TienLen.Unity.Infrastructure.Network;\nusing TienLen.Unity.Infrastructure.Services;\nusing TienLen.Unity.Presentation.Views;\nusing TMPro;\nusing Microsoft.Extensions.Logging;\nusing VContainer;\nusing Nakama;\nusing System.Linq;\nusing Cysharp.Threading.Tasks; // NEW\n\nnamespace TienLen.Unity.Presentation.Presenters\n{\n    public class GamePresenter : MonoBehaviour\n    {\n        [Header(\"UI References\")]\n        public HandView PlayerHandView;\n        public Button PlayButton;\n        public Button SkipButton;\n        public Button StartGameButton; \n        public TMP_Text StatusText;\n        public BoardView GameBoardView; \n        public SeatManager GameSeatManager; \n\n        private ILogger<GamePresenter> _logger;\n        private IGameNetworkClient _network;\n        private GameSession _gameSession;\n        \n        // Local State\n        private Hand _myHand;\n        private List<Card> _currentBoard;\n        private string _currentMatchId;\n\n        [Inject]\n        public void Construct(ILogger<GamePresenter> logger, IGameNetworkClient network, GameSession gameSession)\n        {\n            _logger = logger;\n            _network = network;\n            _gameSession = gameSession;\n            _logger.LogInformation(\"GamePresenter constructed.\");\n        }\n\n        private void Start()\n        {\n            _myHand = new Hand();\n            _currentBoard = new List<Card>();\n\n            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlayClicked);\n            if (SkipButton != null) SkipButton.onClick.AddListener(OnSkipClicked);\n            if (StartGameButton != null) StartGameButton.onClick.AddListener(OnStartGameClicked); \n            \n            // Subscribe to Network Events\n            _network.OnHandReceived += HandleReceiveHand;\n            _network.OnPlayerPlayedCard += HandleCardPlayed;\n            _network.OnError += HandleError;\n            \n            // GameRoom Initialization Logic\n            InitializeGameRoom();\n        }\n\n        private void InitializeGameRoom()\n        {\n            if (_gameSession.CurrentRoom == null)\n            {\n                _logger.LogError(\"GameRoom loaded without a valid match in GameSession!\");\n                if (StatusText) StatusText.text = \"Error: No active room!\";\n                return;\n            }\n\n            _currentMatchId = _gameSession.CurrentRoom.Id;\n            if (StatusText) StatusText.text = $\"Room: {_currentMatchId}\";\n            _logger.LogInformation($\"Game Room initialized. Room ID: {_currentMatchId}\");\n\n            _logger.LogInformation($\"This client is {( _gameSession.IsHost ? \"the HOST\" : \"a CLIENT\")}.\");\n\n            // Setup Seat Manager\n            if (GameSeatManager != null && _gameSession.CurrentRoom != null)\n            {\n                string localUserId = _gameSession.CurrentRoom.Self.UserId;\n                List<IUserPresence> otherPlayers = _gameSession.ConnectedPlayers;\n                GameSeatManager.SetupPlayerSeats(localUserId, otherPlayers);\n            }\n\n            // Host logic: Show Start Button if Host\n            if (StartGameButton != null)\n            {\n                StartGameButton.gameObject.SetActive(_gameSession.IsHost);\n            }\n\n            if (!_gameSession.IsHost)\n            {\n                _logger.LogInformation(\"Waiting for host to start the game...\");\n            }\n        }\n\n        // NEW: Button Handler\n        private async UniTask DealCardsAsHost() \n        {\n            _logger.LogInformation(\"Host dealing cards...\");\n            // 1. Create and Shuffle Deck (from Shared Kernel)\n            var deck = new TienLen.Core.Domain.Services.Deck();\n            \n            // 2. Deal to Self (Local)\n            var myCards = deck.Draw(13);\n            myCards.Sort();\n            HandleReceiveHand(myCards);\n            \n            // 3. Deal to Connected Players (Network)\n            foreach (var presence in _gameSession.ConnectedPlayers)\n            {\n                var theirCards = deck.Draw(13);\n                theirCards.Sort();\n                _logger.LogInformation($\"Sending {theirCards.Count} cards to {presence.UserId}\");\n                await _network.SendHandAsync(presence.UserId, theirCards); // Await the call\n            }\n        }\n        private async void OnStartGameClicked() \n        {\n            _logger.LogInformation(\"Start Game clicked. Dealing cards...\");\n            await DealCardsAsHost(); \n            if (StartGameButton != null) StartGameButton.gameObject.SetActive(false); // Hide button after start\n        }\n        \n        private void HandleReceiveHand(List<Card> cards)\n        {\n             _myHand = new Hand(); \n             _myHand.AddCards(cards);\n             PlayerHandView.RenderHand(_myHand.Cards);\n             _logger.LogInformation(\"Hand received and rendered.\");\n        }\n\n        private void HandleCardPlayed(string playerId, List<Card> cards)\n        {\n            _logger.LogInformation($\"Player {playerId} played {cards.Count} cards.\");\n            _currentBoard = cards; // Update board state\n\n            if (GameBoardView != null && GameSeatManager != null)\n            {\n                Vector3 startPos = GameSeatManager.GetHandPosition(playerId);\n                _logger.LogInformation($\"Animating from {startPos} for player {playerId}\");\n                \n                if (startPos == Vector3.zero)\n                {\n                     _logger.LogWarning($\"StartPos is Zero! SeatManager might not have mapped user {playerId}.\");\n                }\n\n                GameBoardView.AnimateAndAddPlayedCards(cards, startPos).Forget();\n            }\n            else\n            {\n                _logger.LogWarning($\"BoardView: {GameBoardView}, SeatManager: {GameSeatManager}. Animation skipped.\");\n            }\n        }\n        \n        private void HandleError(string error)\n        {\n             _logger.LogError(\"Network Error: {Error}\", error);\n        }\n\n        private async void OnPlayClicked()\n        {\n            var selectedCards = PlayerHandView.GetSelectedCards();\n            _logger.LogInformation(\"Player attempting to play cards: {@SelectedCards}\", selectedCards);\n\n            // Shared Kernel Validation\n            if (!TienLen.Core.Rules.TienLenRuleEngine.IsValidMove(selectedCards, _currentBoard))\n            {\n                if (StatusText) StatusText.text = \"Invalid Move!\";\n                _logger.LogWarning(\"Invalid move attempt: Shared Kernel validation failed.\");\n                return;\n            }\n\n            // Send via Network Interface\n            _logger.LogInformation(\"Valid move. Sending to server...\");\n            await _network.SendPlayCardsAsync(selectedCards);\n\n            // Optimistic Update\n            _myHand.RemoveCards(selectedCards);\n            PlayerHandView.RenderHand(_myHand.Cards);\n        }\n\n        private async void OnSkipClicked()\n        {\n            await _network.SendSkipTurnAsync();\n        }\n        \n        private void OnDestroy()\n        {\n            if (PlayButton != null) PlayButton.onClick.RemoveAllListeners();\n            if (SkipButton != null) SkipButton.onClick.RemoveAllListeners();\n            if (StartGameButton != null) StartGameButton.onClick.RemoveAllListeners();\n            \n            if (_network != null)\n            {\n                _network.OnHandReceived -= HandleReceiveHand;\n                _network.OnPlayerPlayedCard -= HandleCardPlayed;\n                _network.OnError -= HandleError;\n            }\n        }\n    }\n}). Original old_string was (using UnityEngine;\nusing UnityEngine.UI;\nusing System.Collections.Generic;\nusing TienLen.Core.Domain.Aggregates;\nusing TienLen.Core.Domain.ValueObjects;\nusing TienLen.Core.Rules;\nusing TienLen.Unity.Infrastructure;\nusing TienLen.Unity.Infrastructure.Network;\nusing TienLen.Unity.Infrastructure.Services;\nusing TienLen.Unity.Presentation.Views;\nusing TMPro;\nusing Microsoft.Extensions.Logging;\nusing VContainer;\nusing Nakama;\nusing System.Linq;\nusing Cysharp.Threading.Tasks; // NEW\n\nnamespace TienLen.Unity.Presentation.Presenters\n{\n    public class GamePresenter : MonoBehaviour\n    {\n        [Header(\"UI References\")]\n        public HandView PlayerHandView;\n        public Button PlayButton;\n        public Button SkipButton;\n        public Button StartGameButton; \n        public TMP_Text StatusText;\n        public BoardView GameBoardView; \n        public SeatManager GameSeatManager; \n\n        private ILogger<GamePresenter> _logger;\n        private IGameNetworkClient _network;\n        private GameSession _gameSession;\n        \n        // Local State\n        private Hand _myHand;\n        private List<Card> _currentBoard;\n        private string _currentMatchId;\n\n        [Inject]\n        public void Construct(ILogger<GamePresenter> logger, IGameNetworkClient network, GameSession gameSession)\n        {\n            _logger = logger;\n            _network = network;\n            _gameSession = gameSession;\n            _logger.LogInformation(\"GamePresenter constructed.\");\n        }\n\n        private void Start()\n        {\n            _myHand = new Hand();\n            _currentBoard = new List<Card>();\n\n            if (PlayButton != null) PlayButton.onClick.AddListener(OnPlayClicked);\n            if (SkipButton != null) SkipButton.onClick.AddListener(OnSkipClicked);\n            if (StartGameButton != null) StartGameButton.onClick.AddListener(OnStartGameClicked); \n            \n            // Subscribe to Network Events\n            _network.OnHandReceived += HandleReceiveHand;\n            _network.OnPlayerPlayedCard += HandleCardPlayed;\n            _network.OnError += HandleError;\n            \n            // GameRoom Initialization Logic\n            InitializeGameRoom();\n        }\n\n        private void InitializeGameRoom()\n        {\n            if (_gameSession.CurrentRoom == null)\n            {\n                _logger.LogError(\"GameRoom loaded without a valid match in GameSession!\");\n                if (StatusText) StatusText.text = \"Error: No active room!\";\n                return;\n            }\n\n            _currentMatchId = _gameSession.CurrentRoom.Id;\n            if (StatusText) StatusText.text = $\"Room: {_currentMatchId}\";\n            _logger.LogInformation($\"Game Room initialized. Room ID: {_currentMatchId}\");\n\n            _logger.LogInformation($\"This client is {( _gameSession.IsHost ? \"the HOST\" : \"a CLIENT\")}.\");\n\n            // Setup Seat Manager\n            if (GameSeatManager != null && _gameSession.CurrentRoom != null)\n            {\n                string localUserId = _gameSession.CurrentRoom.Self.UserId;\n                List<IUserPresence> otherPlayers = _gameSession.ConnectedPlayers;\n                GameSeatManager.SetupPlayerSeats(localUserId, otherPlayers);\n            }\n\n            // Host logic: Show Start Button if Host\n            if (StartGameButton != null)\n            {\n                StartGameButton.gameObject.SetActive(_gameSession.IsHost);\n            }\n\n            if (!_gameSession.IsHost)\n            {\n                _logger.LogInformation(\"Waiting for host to start the game...\");\n            }\n        }\n\n        // NEW: Button Handler\n        private async UniTask DealCardsAsHost() \n        {\n            _logger.LogInformation(\"Host dealing cards...\");\n            // 1. Create and Shuffle Deck (from Shared Kernel)\n            var deck = new TienLen.Core.Domain.Services.Deck();\n            \n            // 2. Deal to Self (Local)\n            var myCards = deck.Draw(13);\n            myCards.Sort();\n            HandleReceiveHand(myCards);\n            \n            // 3. Deal to Connected Players (Network)\n            foreach (var presence in _gameSession.ConnectedPlayers)\n            {\n                var theirCards = deck.Draw(13);\n                theirCards.Sort();\n                _logger.LogInformation($\"Sending {theirCards.Count} cards to {presence.UserId}\");\n                await _network.SendHandAsync(presence.UserId, theirCards); // Await the call\n            }\n        }\n        private async void OnStartGameClicked() \n        {\n            _logger.LogInformation(\"Start Game clicked. Dealing cards...\");\n            await DealCardsAsHost(); \n            if (StartGameButton != null) StartGameButton.gameObject.SetActive(false); // Hide button after start\n        }\n        \n        private void HandleReceiveHand(List<Card> cards)\n        {\n             _myHand = new Hand(); \n             _myHand.AddCards(cards);\n             PlayerHandView.RenderHand(_myHand.Cards);\n             _logger.LogInformation(\"Hand received and rendered.\");\n        }\n\n        private void HandleCardPlayed(string playerId, List<Card> cards)\n        {\n            _logger.LogInformation($\"Player {playerId} played {cards.Count} cards.\");\n            _currentBoard = cards; // Update board state\n\n            if (GameBoardView != null && GameSeatManager != null)\n            {\n                Vector3 startPos = GameSeatManager.GetHandPosition(playerId);\n                _logger.LogInformation($\"Animating from {startPos} for player {playerId}\");\n                \n                if (startPos == Vector3.zero)\n                {\n                     _logger.LogWarning($\"StartPos is Zero! SeatManager might not have mapped user {playerId}.\");\n                }\n\n                GameBoardView.AnimateAndAddPlayedCards(cards, startPos).Forget();\n            }\n            else\n            {\n                _logger.LogWarning($\"BoardView: {GameBoardView}, SeatManager: {GameSeatManager}. Animation skipped.\");\n            }\n        }\n        \n        private void HandleError(string error)\n        {\n             _logger.LogError(\"Network Error: {Error}\", error);\n        }\n\n        private async void OnPlayClicked()\n        {\n            var selectedCards = PlayerHandView.GetSelectedCards();\n            _logger.LogInformation(\"Player attempting to play cards: {@SelectedCards}\", selectedCards);\n\n            // Convert selectedCards (Domain.Card) to List<int> (Protobuf indices)\n            // This requires mapping to player's current hand.\n            List<int> cardIndicesToPlay = new List<int>();\n            foreach (var domainCard in selectedCards)\n            {\n                // Find the index of this card in the player's _myHand\n                int index = _myHand.Cards.FindIndex(c => c.Rank == domainCard.Rank && c.Suit == domainCard.Suit);\n                if (index != -1)\n                {\n                    cardIndicesToPlay.Add(index);\n                }\n                else\n                {\n                    _logger.LogWarning($\"Selected card {domainCard} not found in player's hand for indexing.\");\n                }\n            }\n\n            if (cardIndicesToPlay.Count == 0 && selectedCards.Count > 0)\n            {\n                if (StatusText) StatusText.text = \"Error: Selected cards not in hand!\";\n                return;\n            }\n            \n            // Shared Kernel Validation (Client-side prediction/validation before sending)\n            if (!TienLen.Core.Rules.TienLenRuleEngine.IsValidMove(selectedCards, _currentBoard))\n            {\n                if (StatusText) StatusText.text = \"Invalid Move!\";\n                _logger.LogWarning(\"Invalid move attempt: Client-side validation failed.\");\n                return;\n            }\n\n            // Send via Network Interface\n            _logger.LogInformation(\"Valid move. Sending to server...\");\n            await _network.SendPlayCardAsync(cardIndicesToPlay);\n\n            // Optimistic Update (Will be corrected by server's OnTurnUpdate)\n            _myHand.RemoveCards(selectedCards);\n            PlayerHandView.RenderHand(_myHand.Cards);\n            PlayerHandView.ClearSelectedCards(); // Clear selection after playing\n        }\n
        private async void OnSkipClicked()
        {
            // TODO: Implement SendSkipTurnAsync in IGameNetwork and server logic
            _logger?.LogInformation("Skip Turn clicked (not yet implemented)");
        }
        
        private void OnDestroy()
        {
            if (PlayButton != null) PlayButton.onClick.RemoveAllListeners();
            if (SkipButton != null) SkipButton.onClick.RemoveAllListeners();
            if (StartGameButton != null) StartGameButton.onClick.RemoveAllListeners();
            
            if (_network != null)
            {
                _network.OnMatchStart -= HandleMatchStart;
                _network.OnTurnUpdate -= HandleTurnUpdate;
                _network.OnError -= HandleError;
            }
        }
    }
}

