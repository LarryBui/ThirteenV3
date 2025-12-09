using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using TienLen.Unity.Domain.ValueObjects;
// using TienLen.Core.DTOs; 

namespace TienLen.Unity.Infrastructure
{
    public class SignalRClient : MonoBehaviour
    {
        private HubConnection _connection;
        private string _serverUrl = "http://localhost:5218/gamehub"; 

        public event Action<string> OnMatchCreated;
        public event Action<string> OnPlayerJoined;
        public event Action<string, List<Card>> OnCardPlayed;
        public event Action<List<Card>> OnReceiveHand;

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public async Task ConnectAsync()
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_serverUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string>("MatchCreated", (matchId) => 
            {
                MainThreadDispatcher.Enqueue(() => OnMatchCreated?.Invoke(matchId));
            });
            
            _connection.On<List<Card>>("ReceiveHand", (cards) => 
            {
                MainThreadDispatcher.Enqueue(() => OnReceiveHand?.Invoke(cards));
            });

            _connection.On<string>("PlayerJoined", (connId) => 
            {
                MainThreadDispatcher.Enqueue(() => OnPlayerJoined?.Invoke(connId));
            });

            // Note: Server sends object { PlayerId, Cards }, need to match signature
            // The Hub sends: await Clients.Group(dto.MatchId).SendAsync("OnCardPlayed", new { PlayerId = ..., Cards = ... });
            // We need a DTO or handle the object.
            // Simpler approach: The commented out code expected (playerId, cards). 
            // Let's adjust the server or client. 
            // If Server sends `new { PlayerId, Cards }`, SignalR Client in C# expects a single object.
            // If Server sent `SendAsync("OnCardPlayed", playerId, cards)`, then Client expects (p, c).
            
            // Looking at Hub: await Clients.Group(dto.MatchId).SendAsync("OnCardPlayed", new { PlayerId = ..., Cards = ... });
            // This is sending ONE argument (an object).
            // So client should expect ONE argument.
            
            _connection.On<CardPlayedEvent>("OnCardPlayed", (evt) => 
            {
                 MainThreadDispatcher.Enqueue(() => OnCardPlayed?.Invoke(evt.PlayerId, evt.Cards));
            });

            await _connection.StartAsync();
        }

        public async Task CreateMatch(int playerCount)
        {
            if (_connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("CreateMatch", new { PlayerCount = playerCount });
        }

        public async Task PlayCard(string matchId, List<Card> cards)
        {
             if (_connection.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("PlayCard", new { MatchId = matchId, Cards = cards });
        }
        
        // Helper class to deserialize the event from server
        private class CardPlayedEvent {
            public string PlayerId { get; set; }
            public List<Card> Cards { get; set; }
        }
    }
}
