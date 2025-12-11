package adapter

import (
	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/tienlen"
	"github.com/yourusername/tienlen-server/pb"
	"google.golang.org/protobuf/proto"
)

// DispatchEvents converts domain events into protobuf messages and broadcasts them.
func DispatchEvents(dispatcher runtime.MatchDispatcher, presences map[string]runtime.Presence, events []tienlen.Event) {
	for _, ev := range events {
		switch e := ev.(type) {
		case tienlen.MatchStarted:
			sendMatchStarted(dispatcher, presences, e)
		case tienlen.HandUpdated:
			sendHandUpdate(dispatcher, presences, e)
		case tienlen.TurnChanged:
			sendTurnUpdate(dispatcher, e)
		case tienlen.RoundEnded:
			sendRoundEnd(dispatcher, e)
		case tienlen.GameOver:
			sendGameOver(dispatcher, e)
		}
	}
}

// BroadcastOwnerUpdate sends an OP_OWNER_UPDATE to all players.
func BroadcastOwnerUpdate(dispatcher runtime.MatchDispatcher, ownerID string) {
	data := []byte(ownerID)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_OWNER_UPDATE), data, nil, nil, true)
}

// SendMatchState synchronizes a late joiner with the current match state.
func SendMatchState(dispatcher runtime.MatchDispatcher, snapshot tienlen.Snapshot, seats []string, receiver runtime.Presence) {
	packet := &pb.MatchStatePacket{
		IsPlaying:      snapshot.IsPlaying,
		OwnerId:        snapshot.OwnerID,
		Board:          toPBCards(snapshot.Board),
		ActivePlayerId: snapshot.ActivePlayerID,
		PlayerIds:      seats,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_STATE), data, []runtime.Presence{receiver}, nil, true)
}

// SendHand sends a targeted hand update to a specific player.
func SendHand(dispatcher runtime.MatchDispatcher, userID string, cards []tienlen.Card, receivers []runtime.Presence) {
	packet := &pb.HandUpdatePacket{
		Hand: toPBCards(cards),
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_HAND_UPDATE), data, receivers, nil, true)
}

func sendMatchStarted(dispatcher runtime.MatchDispatcher, presences map[string]runtime.Presence, ev tienlen.MatchStarted) {
	for playerID, hand := range ev.Hands {
		presence, ok := presences[playerID]
		if !ok {
			continue
		}
		packet := &pb.MatchStartPacket{
			Hand:      toPBCards(hand),
			PlayerIds: ev.TurnOrder, // TurnOrder now matches seat order
			OwnerId:   ev.OwnerID,
		}
		data, err := proto.Marshal(packet)
		if err != nil {
			continue
		}
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_START), data, []runtime.Presence{presence}, nil, true)
	}
}

func sendHandUpdate(dispatcher runtime.MatchDispatcher, presences map[string]runtime.Presence, ev tienlen.HandUpdated) {
	presence, ok := presences[ev.PlayerID]
	if !ok {
		return
	}
	SendHand(dispatcher, ev.PlayerID, ev.Hand, []runtime.Presence{presence})
}

func sendTurnUpdate(dispatcher runtime.MatchDispatcher, ev tienlen.TurnChanged) {
	packet := &pb.TurnUpdatePacket{
		ActivePlayerId:   ev.ActivePlayerID,
		LastPlayedCards:  toPBCards(ev.Board),
		SecondsRemaining: 30,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_TURN_UPDATE), data, nil, nil, true)
}

// BroadcastSeatUpdate sends the current seat map to all players.
func BroadcastPlayerJoined(dispatcher runtime.MatchDispatcher, joinedUserID string, playerOrder []string, ownerID string, game *tienlen.Game) {
	snapshot := tienlen.Snapshot{}
	if game != nil {
		snapshot = game.Snapshot()
	}

	packet := &pb.MatchStatePacket{
		IsPlaying:      snapshot.IsPlaying,
		OwnerId:        ownerID,
		Board:          toPBCards(snapshot.Board),
		ActivePlayerId: snapshot.ActivePlayerID,
		PlayerIds:      playerOrder,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_STATE), data, nil, nil, true)
}

// BroadcastPlayerLeft updates everyone with the current state after one or more players leave.
func BroadcastPlayerLeft(dispatcher runtime.MatchDispatcher, leftUserIDs []string, playerOrder []string, ownerID string, game *tienlen.Game) {
	snapshot := tienlen.Snapshot{}
	if game != nil {
		snapshot = game.Snapshot()
	}

	packet := &pb.MatchStatePacket{
		IsPlaying:      snapshot.IsPlaying,
		OwnerId:        ownerID,
		Board:          toPBCards(snapshot.Board),
		ActivePlayerId: snapshot.ActivePlayerID,
		PlayerIds:      playerOrder,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_STATE), data, nil, nil, true)
}

func sendRoundEnd(dispatcher runtime.MatchDispatcher, ev tienlen.RoundEnded) {
	packet := &pb.RoundEndPacket{WinnerId: ev.WinnerID}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_ROUND_END), data, nil, nil, true)
}

func sendGameOver(dispatcher runtime.MatchDispatcher, ev tienlen.GameOver) {
	packet := &pb.GameOverPacket{WinnerId: ev.WinnerID}
	data, err := proto.Marshal(packet)
	if err != nil {
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_GAME_OVER), data, nil, nil, true)
}

func toPBCards(cards []tienlen.Card) []*pb.Card {
	out := make([]*pb.Card, 0, len(cards))
	for _, c := range cards {
		c := c
		out = append(out, &pb.Card{Suit: c.Suit, Rank: c.Rank})
	}
	return out
}
