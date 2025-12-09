using System;
using System.Collections.Generic;
using TienLen.Unity.Domain.ValueObjects;
using TienLen.Unity.Domain.Aggregates;

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

        public GameModel()
        {
            _playerHand = new Hand();
            _currentBoard = new List<Card>();
            _activePlayerId = string.Empty;
            _matchId = string.Empty;
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

        public void Reset()
        {
            _playerHand = new Hand();
            _currentBoard = new List<Card>();
            _activePlayerId = string.Empty;
            _matchId = string.Empty;
            _secondsRemaining = 0;

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
        }
    }
}
