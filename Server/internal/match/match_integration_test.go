package match

import (
	"context"
	"testing"

	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/tienlen"
	"github.com/yourusername/tienlen-server/pb"
	"google.golang.org/protobuf/proto"
)

type stubPresence struct{ id string }

func (p stubPresence) GetHidden() bool                   { return false }
func (p stubPresence) GetPersistence() bool              { return true }
func (p stubPresence) GetUsername() string               { return p.id }
func (p stubPresence) GetStatus() string                 { return "" }
func (p stubPresence) GetReason() runtime.PresenceReason { return runtime.PresenceReasonJoin }
func (p stubPresence) GetUserId() string                 { return p.id }
func (p stubPresence) GetSessionId() string              { return p.id + "-sid" }
func (p stubPresence) GetNodeId() string                 { return "node1" }

type stubMatchData struct {
	op       int64
	userID   string
	data     []byte
	reliable bool
}

func (m stubMatchData) GetUserId() string                 { return m.userID }
func (m stubMatchData) GetHidden() bool                   { return false }
func (m stubMatchData) GetPersistence() bool              { return true }
func (m stubMatchData) GetUsername() string               { return m.userID }
func (m stubMatchData) GetStatus() string                 { return "" }
func (m stubMatchData) GetReason() runtime.PresenceReason { return runtime.PresenceReasonJoin }
func (m stubMatchData) GetOpCode() int64                  { return m.op }
func (m stubMatchData) GetData() []byte                   { return m.data }
func (m stubMatchData) GetReliable() bool                 { return m.reliable }
func (m stubMatchData) GetReceiveTime() int64             { return 0 }
func (m stubMatchData) GetSessionId() string              { return m.userID + "-sid" }
func (m stubMatchData) GetNodeId() string                 { return "node1" }

type recordedMessage struct {
	op        int64
	data      []byte
	presences []runtime.Presence
	reliable  bool
}

type recordingDispatcher struct {
	msgs []recordedMessage
}

func (d *recordingDispatcher) BroadcastMessage(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	d.msgs = append(d.msgs, recordedMessage{op: opCode, data: data, presences: presences, reliable: reliable})
	return nil
}

func (d *recordingDispatcher) BroadcastMessageDeferred(opCode int64, data []byte, presences []runtime.Presence, sender runtime.Presence, reliable bool) error {
	return d.BroadcastMessage(opCode, data, presences, sender, reliable)
}

func (d *recordingDispatcher) MatchKick(presences []runtime.Presence) error { return nil }
func (d *recordingDispatcher) MatchLabelUpdate(label string) error          { return nil }

func (d *recordingDispatcher) reset() { d.msgs = nil }

type testLogger struct{ t *testing.T }

func (l testLogger) Debug(format string, v ...interface{}) {}
func (l testLogger) Info(format string, v ...interface{})  {}
func (l testLogger) Warn(format string, v ...interface{})  {}
func (l testLogger) Error(format string, v ...interface{}) {}
func (l testLogger) WithField(key string, v interface{}) runtime.Logger {
	return l
}
func (l testLogger) WithFields(fields map[string]interface{}) runtime.Logger {
	return l
}
func (l testLogger) Fields() map[string]interface{} { return nil }

func TestMatchStartFlowDispatchesMessages(t *testing.T) {
	m := &Match{}
	logger := testLogger{t}
	dispatcher := &recordingDispatcher{}

	state, _, _ := m.MatchInit(context.Background(), logger, nil, nil, nil)
	s := state.(*MatchState)

	p1 := stubPresence{id: "p1"}
	p2 := stubPresence{id: "p2"}

	m.MatchJoin(context.Background(), logger, nil, nil, dispatcher, 0, s, []runtime.Presence{p1, p2})
	dispatcher.reset()

	startMsg := stubMatchData{op: int64(pb.OpCode_OP_MATCH_START_REQUEST), userID: "p1"}
	m.handleMessage(s, dispatcher, logger, startMsg)

	if len(dispatcher.msgs) == 0 {
		t.Fatalf("expected messages after match start")
	}

	var startCount, turnCount int
	for _, msg := range dispatcher.msgs {
		switch pb.OpCode(msg.op) {
		case pb.OpCode_OP_MATCH_START:
			startCount++
			packet := &pb.MatchStartPacket{}
			if err := proto.Unmarshal(msg.data, packet); err != nil {
				t.Fatalf("failed to unmarshal MatchStartPacket: %v", err)
			}
			if len(packet.Hand) != 13 {
				t.Fatalf("expected 13 cards in start packet, got %d", len(packet.Hand))
			}
		case pb.OpCode_OP_TURN_UPDATE:
			turnCount++
		}
	}
	if startCount != 2 {
		t.Fatalf("expected 2 MatchStart packets (one per player), got %d", startCount)
	}
	if turnCount == 0 {
		t.Fatalf("expected TurnUpdate after start")
	}
}

func TestMatchPlayAndPassFlow(t *testing.T) {
	m := &Match{}
	logger := testLogger{t}
	dispatcher := &recordingDispatcher{}

	state, _, _ := m.MatchInit(context.Background(), logger, nil, nil, nil)
	s := state.(*MatchState)

	p1 := stubPresence{id: "p1"}
	p2 := stubPresence{id: "p2"}
	m.MatchJoin(context.Background(), logger, nil, nil, dispatcher, 0, s, []runtime.Presence{p1, p2})

	// Prepare deterministic game state.
	s.Game = tienlen.NewGame()
	_, _ = s.Game.Start([]string{"p1", "p2"}, "p1", "")
	s.Game.Hands = map[string][]tienlen.Card{
		"p1": {{Rank: 1, Suit: 0}, {Rank: 3, Suit: 0}},
		"p2": {{Rank: 2, Suit: 0}, {Rank: 4, Suit: 0}},
	}
	s.Game.TurnOrder = []string{"p1", "p2"}
	s.Game.CurrentIdx = 0
	s.Game.Board = nil
	s.Game.LastActor = ""
	s.Game.RoundSkippers = make(map[string]bool)
	if got := len(s.Game.Hands["p1"]); got != 2 {
		t.Fatalf("expected p1 hand of 2 cards, got %d", got)
	}
	dispatcher.reset()

	playReq := &pb.PlayCardRequest{CardIndices: []int32{0}}
	data, _ := proto.Marshal(playReq)
	playMsg := stubMatchData{op: int64(pb.OpCode_OP_PLAY_CARD), userID: "p1", data: data}
	m.handleMessage(s, dispatcher, logger, playMsg)

	var hasHandUpdate, hasTurnUpdate bool
	var opsAfterPlay []pb.OpCode
	for _, msg := range dispatcher.msgs {
		switch pb.OpCode(msg.op) {
		case pb.OpCode_OP_HAND_UPDATE:
			hasHandUpdate = true
		case pb.OpCode_OP_TURN_UPDATE:
			hasTurnUpdate = true
		}
		opsAfterPlay = append(opsAfterPlay, pb.OpCode(msg.op))
	}
	if !hasHandUpdate || !hasTurnUpdate {
		t.Fatalf("expected hand and turn updates after play, got hand=%v turn=%v, ops=%v", hasHandUpdate, hasTurnUpdate, opsAfterPlay)
	}

	dispatcher.reset()
	passMsg := stubMatchData{op: int64(pb.OpCode_OP_PASS), userID: "p2"}
	m.handleMessage(s, dispatcher, logger, passMsg)

	var roundEnd, turnUpdate int
	for _, msg := range dispatcher.msgs {
		switch pb.OpCode(msg.op) {
		case pb.OpCode_OP_ROUND_END:
			roundEnd++
		case pb.OpCode_OP_TURN_UPDATE:
			turnUpdate++
		}
	}
	if roundEnd != 1 {
		t.Fatalf("expected 1 round end message, got %d", roundEnd)
	}
	if turnUpdate == 0 {
		t.Fatalf("expected turn update after round end")
	}
}

func TestGameOverManagement(t *testing.T) {
	m := &Match{}
	logger := testLogger{t}
	dispatcher := &recordingDispatcher{}

	state, _, _ := m.MatchInit(context.Background(), logger, nil, nil, nil)
	s := state.(*MatchState)

	p1 := stubPresence{id: "p1"}
	p2 := stubPresence{id: "p2"}
	p3 := stubPresence{id: "p3"}
	p4 := stubPresence{id: "p4"}

	m.MatchJoin(context.Background(), logger, nil, nil, dispatcher, 0, s, []runtime.Presence{p1, p2, p3, p4})
	dispatcher.reset()

	// --- Simulate game start ---
	startMsg := stubMatchData{op: int64(pb.OpCode_OP_MATCH_START_REQUEST), userID: "p1"}
	m.handleMessage(s, dispatcher, logger, startMsg)
	dispatcher.reset()

	if !s.Game.IsPlaying() {
		t.Fatal("expected game to be playing")
	}

	// --- Prepare hands to make players finish (3 winners to trigger game over) ---
	s.Game.Hands = map[string][]tienlen.Card{
		"p1": {{Rank: 0, Suit: 0}}, // 1st finisher
		"p2": {{Rank: 1, Suit: 0}}, // 2nd finisher
		"p3": {{Rank: 2, Suit: 0}}, // 3rd finisher
		"p4": {{Rank: 3, Suit: 0}}, // Loser
	}
	s.Game.TurnOrder = []string{"p1", "p2", "p3", "p4"} // Set explicit turn order for test
	s.Game.CurrentIdx = 0                               // Start with p1

	// p1 plays last card (1st finisher)
	playReq1 := &pb.PlayCardRequest{CardIndices: []int32{0}}
	data1, _ := proto.Marshal(playReq1)
	playMsg1 := stubMatchData{op: int64(pb.OpCode_OP_PLAY_CARD), userID: "p1", data: data1}
	m.handleMessage(s, dispatcher, logger, playMsg1)
	dispatcher.reset()

	if !s.Game.IsPlaying() {
		t.Fatal("expected game to still be playing after 1st player finishes")
	}

	// p2 plays last card (2nd finisher)
	playReq2 := &pb.PlayCardRequest{CardIndices: []int32{0}}
	data2, _ := proto.Marshal(playReq2)
	playMsg2 := stubMatchData{op: int64(pb.OpCode_OP_PLAY_CARD), userID: "p2", data: data2}
	m.handleMessage(s, dispatcher, logger, playMsg2)
	dispatcher.reset()

	if !s.Game.IsPlaying() {
		t.Fatal("expected game to still be playing after 2nd player finishes")
	}

	// p3 plays last card (3rd finisher -> GAME OVER)
	playReq3 := &pb.PlayCardRequest{CardIndices: []int32{0}}
	data3, _ := proto.Marshal(playReq3)
	playMsg3 := stubMatchData{op: int64(pb.OpCode_OP_PLAY_CARD), userID: "p3", data: data3}
	m.handleMessage(s, dispatcher, logger, playMsg3)
	dispatcher.reset()

	if s.Game.IsPlaying() {
		t.Fatal("expected game to not be playing")
	}
	if s.LastGameWinnerID != "p1" {
		t.Fatalf("expected LastGameWinnerID to be p1, got %s", s.LastGameWinnerID)
	}

	// --- Simulate starting a new game ---
	startNewGameMsg := stubMatchData{op: int64(pb.OpCode_OP_MATCH_START_REQUEST), userID: "p1"}
	m.handleMessage(s, dispatcher, logger, startNewGameMsg)
	dispatcher.reset()

	if !s.Game.IsPlaying() {
		t.Fatal("expected game to be playing again")
	}
	if s.Game.Hands["p1"] == nil || len(s.Game.Hands["p1"]) != 13 {
		t.Fatal("expected game to be reset and hands dealt")
	}
	if s.LastGameWinnerID != "p1" { // Last winner should persist until next game over
		t.Fatalf("expected LastGameWinnerID to still be p1, got %s", s.LastGameWinnerID)
	}
}