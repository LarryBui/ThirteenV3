using System;
using System.Collections.Generic;
using TienLen.Unity.Domain.ValueObjects;
using TienLen.Unity.Domain.Aggregates;
using TienLen.Unity.Domain.Enums;

namespace TienLen.Unity.Domain.Aggregates
{
    public class GameModel
    {
        // Public Events for State Changes
        public event Action<Hand> OnHandUpdated;
        public event Action<List<Card>> OnBoardUpdated;
        public event Action<string> OnActivePlayerChanged;
        public event Action<int> OnSecondsRemainingUpdated;
        public event Action<string> OnMatchIdUpdated;
        public event Action<string> OnMatchOwnerChanged; // Existing event
        public event Action<bool> OnIsPlayingChanged; // New event
        public event Action<string> OnGameOver; // New event (winnerId)
        public event Action<IReadOnlyList<string>> OnPlayerIdsUpdated; // New event for player list
        public event Action<IReadOnlyList<string>> OnSeatsUpdated; // New event for seats (seat index => userId)
        public event Action OnGameStarted;

        // Internal State
        private Hand _playerHand;
        public IReadOnlyList<Card> PlayerHand => _playerHand.Cards;

        private List<Card> _currentBoard;
        public IReadOnlyList<Card> CurrentBoard => _currentBoard;

        private string _activePlayerId;
        public string ActivePlayerId => _activePlayerId;

        private int _secondsRemaining;
        public int SecondsRemaining => _secondsRemaining;

        private string _matchId;
        public string MatchId => _matchId;

        private string _matchOwnerId; // Existing field
        public string MatchOwnerId => _matchOwnerId; // Existing property

        private bool _isPlaying; // New field
        public bool IsPlaying => _isPlaying; // New property

        private string _winnerId; // New field
        public string WinnerId => _winnerId; // New property

        private List<string> _playerIds; // New field
        public IReadOnlyList<string> PlayerIds => _playerIds; // New property

        private List<string> _seats = new List<string>(new string[4]); // seat index -> userId
        public IReadOnlyList<string> Seats => _seats;

        public GameModel()
        {
            _playerHand = new Hand();
            _currentBoard = new List<Card>();
            _activePlayerId = string.Empty;
            _matchId = string.Empty;
            _matchOwnerId = string.Empty;
            _isPlaying = false; // Initialize new field
            _winnerId = string.Empty; // Initialize new field
            _playerIds = new List<string>(); // Initialize new field
        }

        public void SetPlayerHand(Hand newHand)
        {
            _playerHand = newHand;
            OnHandUpdated?.Invoke(_playerHand);
        }

        public void UpdateBoard(List<Card> newBoard)
        {
            _currentBoard = newBoard;
            OnBoardUpdated?.Invoke(_currentBoard);
        }

        public void SetActivePlayer(string playerId)
        {
            if (_activePlayerId != playerId)
            {
                _activePlayerId = playerId;
                OnActivePlayerChanged?.Invoke(_activePlayerId);
            }
        }

        public void SetSecondsRemaining(int seconds)
        {
            if (_secondsRemaining != seconds)
            {
                _secondsRemaining = seconds;
                OnSecondsRemainingUpdated?.Invoke(_secondsRemaining);
            }
        }

        public void SetMatchId(string id)
        {
            if (_matchId != id)
            {
                _matchId = id;
                OnMatchIdUpdated?.Invoke(_matchId);
            }
        }

        public void SetMatchOwner(string ownerId) // Existing method
        {
            if (_matchOwnerId != ownerId)
            {
                _matchOwnerId = ownerId;
                OnMatchOwnerChanged?.Invoke(_matchOwnerId);
            }
        }

        public void SetIsPlaying(bool isPlaying) // New method
        {
            if (_isPlaying != isPlaying)
            {
                _isPlaying = isPlaying;
                OnIsPlayingChanged?.Invoke(_isPlaying);
            }
        }

        public void ApplyGameStart(IEnumerable<(Rank Rank, Suit Suit)> cards, string ownerId, IReadOnlyList<string> playerIds)
        {
            var hand = new Hand();
            foreach (var (rank, suit) in cards)
            {
                hand.AddCard(new Card(rank, suit));
            }

            SetMatchOwner(ownerId);
            SetPlayerIds(playerIds);
            SetIsPlaying(true);
            OnGameStarted?.Invoke();
            SetPlayerHand(hand);
        }

        public void SetGameOver(string winnerId) // New method
        {
            SetIsPlaying(false); // Game is no longer playing
            if (_winnerId != winnerId)
            {
                _winnerId = winnerId;
            }
            OnGameOver?.Invoke(_winnerId);
        }

        public void SetPlayerIds(IReadOnlyList<string> playerIds) // New method
        {
            if (!ReferenceEquals(_playerIds, playerIds)) // Simple reference check
            {
                _playerIds = new List<string>(playerIds); // Create new list to avoid direct modification
                OnPlayerIdsUpdated?.Invoke(_playerIds);
            }
        }

        public void Reset()
        {
            _playerHand = new Hand();
            _currentBoard = new List<Card>();
            _activePlayerId = string.Empty;
            _matchId = string.Empty;
            _secondsRemaining = 0;
            _matchOwnerId = string.Empty;
            _isPlaying = false; // Reset new field
            _winnerId = string.Empty; // Reset new field
            _playerIds = new List<string>(); // Reset new field
            _seats = new List<string>(new string[4]);

            // Invoke events to clear UI (null-safe invocations)
            if (OnHandUpdated != null)
                OnHandUpdated(_playerHand);
            if (OnBoardUpdated != null)
                OnBoardUpdated(_currentBoard);
            if (OnActivePlayerChanged != null)
                OnActivePlayerChanged(_activePlayerId);
            if (OnSecondsRemainingUpdated != null)
                OnSecondsRemainingUpdated(_secondsRemaining);
            if (OnMatchIdUpdated != null)
                OnMatchIdUpdated(_matchId);
            if (OnIsPlayingChanged != null)
                OnIsPlayingChanged(_isPlaying);
            if (OnGameOver != null)
                OnGameOver(_winnerId);
            if (OnPlayerIdsUpdated != null)
                OnPlayerIdsUpdated(_playerIds);
            if (OnSeatsUpdated != null)
                OnSeatsUpdated(_seats);
        }

        public void SetSeats(IReadOnlyList<string> seats)
        {
            if (seats == null) return;
            _seats = new List<string>(seats);
            OnSeatsUpdated?.Invoke(_seats);
        }
    }
}
