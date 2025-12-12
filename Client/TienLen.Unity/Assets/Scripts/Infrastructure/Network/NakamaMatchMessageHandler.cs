using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Nakama;
using TienLen.Gen;
using TienLen.Unity.Domain.Aggregates;
using DomainClasses = TienLen.Unity.Domain.ValueObjects;
using Serilog;
using System.Diagnostics;

namespace TienLen.Unity.Infrastructure.Network
{
    public interface IMatchMessageHandler
    {
        event Action<string> OnError;
        void Handle(IMatchState state);
    }

    /// <summary>
    /// Translates Nakama match states (protobuf) into domain updates on the GameModel.
    /// Keeps networking concerns (opcodes/payloads) separate from UI/presentation.
    /// </summary>
    public class NakamaMatchMessageHandler : IMatchMessageHandler
    {
        public event Action<string> OnError;

        private readonly GameModel _gameModel;

        public NakamaMatchMessageHandler(GameModel gameModel)
        {
            _gameModel = gameModel;
        }

        public void Handle(IMatchState state)
        {
            Log.Information("Handling [MatchState]: {@MatchState} ", state);
            if (state?.State == null || state.State.Length == 0)
            {
                return;
            }
            var op = (OpCode)state.OpCode;
            var payload = state.State;

            MainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    switch (op)
                    {
                        case OpCode.OpGameStart:
                            HandleGameStart(payload);
                            break;
                        case OpCode.OpTurnUpdate:
                            HandleTurnUpdate(payload);
                            break;
                        case OpCode.OpError:
                            RaiseError(System.Text.Encoding.UTF8.GetString(payload));
                            break;
                        case OpCode.OpOwnerUpdate:
                            HandleOwnerUpdate(payload);
                            break;
                        case OpCode.OpGameOver:
                            HandleGameOver(payload);
                            break;
                        case OpCode.OpMatchState:
                            HandleMatchState(payload);
                            break;
                        case OpCode.OpHandUpdate:
                            HandleHandUpdate(payload);
                            break;
                        default:
                            Log.Warning("[NakamaMatchMessageHandler] Unknown OpCode: {OpCode}", op);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[NakamaMatchMessageHandler] Fatal error handling state");
                    RaiseError($"Fatal protocol error: {ex.Message}");
                }
            });
        }

        private void HandleGameStart(byte[] payload)
        {
            var startPacket = MatchStartPacket.Parser.ParseFrom(payload);
            var domainHand = new Hand();
            foreach (var protoCard in startPacket.Hand)
            {
                domainHand.AddCard(new DomainClasses.Card((Domain.Enums.Rank)protoCard.Rank, (Domain.Enums.Suit)protoCard.Suit));
            }
            _gameModel.SetPlayerHand(domainHand);
            _gameModel.SetMatchOwner(startPacket.OwnerId);
            _gameModel.SetPlayerIds(startPacket.PlayerIds.ToList());
            _gameModel.SetIsPlaying(true);
        }

        private void HandleTurnUpdate(byte[] payload)
        {
            var turnPacket = TurnUpdatePacket.Parser.ParseFrom(payload);
            var domainCards = new List<DomainClasses.Card>();
            foreach (var protoCard in turnPacket.LastPlayedCards)
            {
                domainCards.Add(new DomainClasses.Card((Domain.Enums.Rank)protoCard.Rank, (Domain.Enums.Suit)protoCard.Suit));
            }
            _gameModel.UpdateBoard(domainCards);
            _gameModel.SetActivePlayer(turnPacket.ActivePlayerId);
            _gameModel.SetSecondsRemaining(turnPacket.SecondsRemaining);
        }

        private void HandleOwnerUpdate(byte[] payload)
        {
            var ownerId = System.Text.Encoding.UTF8.GetString(payload);
            _gameModel.SetMatchOwner(ownerId);
        }

        private void HandleGameOver(byte[] payload)
        {
            var gameOverPacket = GameOverPacket.Parser.ParseFrom(payload);
            _gameModel.SetGameOver(gameOverPacket.WinnerId);
        }

        private void HandleMatchState(byte[] payload)
        {
            var matchStatePacket = MatchStatePacket.Parser.ParseFrom(payload);
            Log.Information("Updated [MatchState]: {@MatchStatePacket}", matchStatePacket);
            _gameModel.SetIsPlaying(matchStatePacket.IsPlaying);
            _gameModel.SetMatchOwner(matchStatePacket.OwnerId);
            _gameModel.UpdateBoard(matchStatePacket.Board.Select(c => new DomainClasses.Card((Domain.Enums.Rank)c.Rank, (Domain.Enums.Suit)c.Suit)).ToList());
            _gameModel.SetActivePlayer(matchStatePacket.ActivePlayerId);
            _gameModel.SetPlayerIds(matchStatePacket.PlayerIds.ToList());
            _gameModel.SetSeats(matchStatePacket.PlayerIds.ToList()); // seats align to index
        }

        private void HandleHandUpdate(byte[] payload)
        {
            var handPacket = HandUpdatePacket.Parser.ParseFrom(payload);
            var domainHand = new Hand();
            foreach (var protoCard in handPacket.Hand)
            {
                domainHand.AddCard(new DomainClasses.Card((Domain.Enums.Rank)protoCard.Rank, (Domain.Enums.Suit)protoCard.Suit));
            }
            _gameModel.SetPlayerHand(domainHand);
        }

        private void RaiseError(string message)
        {
            OnError?.Invoke(message);
        }

    }
}
