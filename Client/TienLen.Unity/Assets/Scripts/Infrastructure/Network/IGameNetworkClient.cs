using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TienLen.Core.Domain.ValueObjects;

namespace TienLen.Unity.Infrastructure.Network
{
    public interface IGameNetworkClient
    {
        // 1. Connection / Lifecycle
        UniTask<string> CreateMatchAsync(int players);
        UniTask JoinMatchAsync(string matchId);
        UniTask LeaveMatchAsync();

        // 2. Actions (Client -> Server)
        UniTask SendPlayCardsAsync(List<Card> cards);
        UniTask SendSkipTurnAsync();
        UniTask SendHandAsync(string userId, List<Card> cards); // NEW

        // 3. Events (Server -> Client)
        event Action<List<Card>> OnHandReceived;
        event Action<string, List<Card>> OnPlayerPlayedCard; // playerId, cards
        event Action<string> OnTurnChanged; // playerId
        event Action<string> OnError;
    }
}
