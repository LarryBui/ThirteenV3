using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Nakama;
using Newtonsoft.Json;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Core.Networking;
using UnityEngine;

namespace TienLen.Unity.Infrastructure.Network
{
    public class NakamaGameClient : IGameNetworkClient, IDisposable
    {
        private readonly NakamaSocketService _socketService;
        private readonly GameSession _session;
        private string _currentMatchId;

        public event Action<List<Card>> OnHandReceived;
        public event Action<string, List<Card>> OnPlayerPlayedCard;
        public event Action<string> OnTurnChanged;
        public event Action<string> OnError;

        public NakamaGameClient(NakamaSocketService socketService, GameSession session)
        {
            _socketService = socketService;
            _session = session;
            
            // If socket is already ready, subscribe
            if (_socketService.Socket != null)
            {
                SubscribeToSocket();
            }
            
            // Listen for future connection
            _socketService.OnConnected += SubscribeToSocket;
        }

        private void SubscribeToSocket()
        {
            if (_socketService.Socket != null)
            {
                _socketService.Socket.ReceivedMatchState -= HandleMatchState;
                _socketService.Socket.ReceivedMatchState += HandleMatchState;
                
                _socketService.Socket.ReceivedMatchPresence -= HandleMatchPresence;
                _socketService.Socket.ReceivedMatchPresence += HandleMatchPresence;
            }
        }

        public void Initialize()
        {
            SubscribeToSocket();
        }

        public async UniTask<string> CreateMatchAsync(int players)
        {
            try 
            {
                // Client-side relayed match creation
                var match = await _socketService.Socket.CreateMatchAsync();
                _currentMatchId = match.Id;
                return _currentMatchId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create match: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        public async UniTask JoinMatchAsync(string matchId)
        {
            try
            {
                await _socketService.Socket.JoinMatchAsync(matchId);
                _currentMatchId = matchId;
            }
            catch (Exception ex)
            {
                 Debug.LogError($"Failed to join match: {ex.Message}");
                 OnError?.Invoke(ex.Message);
            }
        }

        public async UniTask SendPlayCardsAsync(List<Card> cards)
        {
            var payload = new { cards = cards };
            await SendStateAsync(OpCodes.PlayCard, payload);
        }

        public async UniTask SendSkipTurnAsync()
        {
            await SendStateAsync(OpCodes.SkipTurn, new object());
        }

        public async UniTask SendHandAsync(string userId, List<Card> cards)
        {
            if (_session.CurrentRoom == null) return;

            var target = _session.ConnectedPlayers.FirstOrDefault(p => p.UserId == userId);
            if (target == null)
            {
                Debug.LogWarning($"Cannot send hand to {userId}: User not found in room.");
                return;
            }

            var json = JsonConvert.SerializeObject(cards);
            await _socketService.Socket.SendMatchStateAsync(_session.CurrentRoom.Id, OpCodes.HandReceived, json, new [] { target });
        }

        // --- Internal Helpers ---

        private async UniTask SendStateAsync(long opCode, object data)
        {
            if (_session.CurrentRoom == null) 
            {
                Debug.LogWarning("Cannot send state: No active match.");
                return;
            }
            
            var json = JsonConvert.SerializeObject(data);
            await _socketService.Socket.SendMatchStateAsync(_session.CurrentRoom.Id, opCode, json);
        }

        private void HandleMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            if (_session.CurrentRoom == null || presenceEvent.MatchId != _session.CurrentRoom.Id) return;

            Debug.Log($"[NakamaGameClient] Presence Event Received. Joins: {presenceEvent.Joins.Count()}, Leaves: {presenceEvent.Leaves.Count()}");

            // Add new players
            foreach (var p in presenceEvent.Joins)
            {
                // Avoid duplicates (though Nakama shouldn't send dupe joins for same session)
                if (!_session.ConnectedPlayers.Any(x => x.SessionId == p.SessionId))
                {
                    _session.ConnectedPlayers.Add(p);
                    Debug.Log($"[NakamaGameClient] Player joined: {p.UserId}. Total Connected: {_session.ConnectedPlayers.Count}");
                }
            }

            // Remove leaving players
            foreach (var p in presenceEvent.Leaves)
            {
                _session.ConnectedPlayers.RemoveAll(x => x.SessionId == p.SessionId);
                Debug.Log($"[NakamaGameClient] Player left: {p.UserId}. Total Connected: {_session.ConnectedPlayers.Count}");
            }
        }

        private async void HandleMatchState(IMatchState matchState)
        {
            if (_session.CurrentRoom == null || matchState.MatchId != _session.CurrentRoom.Id) return;
            
            // Switch to Main Thread to ensure UI updates (Instantiate) work safely
            await UniTask.SwitchToMainThread();
            
            Debug.Log($"[NakamaGameClient] Received MatchState OpCode: {matchState.OpCode}");

            try
            {
                var json = Encoding.UTF8.GetString(matchState.State);

                switch (matchState.OpCode)
                {
                    case OpCodes.HandReceived:
                        var handData = JsonConvert.DeserializeObject<List<Card>>(json);
                        OnHandReceived?.Invoke(handData);
                        break;

                    case OpCodes.PlayCard: // Relayed from another client (Client-Side Logic)
                    case OpCodes.PlayerPlayed: // Server-Side Logic
                        var playData = JsonConvert.DeserializeObject<PlayerMoveDto>(json);
                        OnPlayerPlayedCard?.Invoke(matchState.UserPresence.UserId, playData.Cards);
                        break;

                    case OpCodes.TurnSkipped:
                        // Handle skip logic if needed
                        break; 
                        
                    case OpCodes.NewTurn:
                         // Assuming payload is playerId, adapt as needed
                         OnTurnChanged?.Invoke(json); 
                         break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing match state: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_socketService?.Socket != null)
            {
                _socketService.Socket.ReceivedMatchState -= HandleMatchState;
                _socketService.Socket.ReceivedMatchPresence -= HandleMatchPresence;
            }
        }
        
        private class PlayerMoveDto { public List<Card> Cards { get; set; } }
        public UniTask LeaveMatchAsync() => UniTask.CompletedTask;
    }
}
