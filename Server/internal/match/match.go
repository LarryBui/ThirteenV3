package match

import (
	"context"
	"database/sql"
	"errors"
	"math/rand"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/match/adapter"
	"github.com/yourusername/tienlen-server/internal/tienlen"
	"github.com/yourusername/tienlen-server/pb"
	"google.golang.org/protobuf/proto"
)

// MatchState keeps Nakama-specific session data and the domain game engine.
type MatchState struct {
	Presences  map[string]runtime.Presence `json:"presences"`
	Spectators map[string]bool             `json:"spectators"`
	OwnerID    string                      `json:"owner_id"`
	Game       *tienlen.Game               `json:"-"`
	Seats      [4]string                   `json:"seats"`           // seat index -> userID (empty string means free)
	SeatByUser map[string]int              `json:"seat_by_user_id"` // userID -> seat index
}

type Match struct{}

func (m *Match) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Info("Match initialized")
	state := &MatchState{
		Presences:  make(map[string]runtime.Presence),
		Spectators: make(map[string]bool),
		Game:       tienlen.NewGame(),
		SeatByUser: make(map[string]int),
	}
	return state, 10, "TienLen"
}

func (m *Match) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*MatchState)
	if m.findOpenSeat(s) == -1 {
		return s, false, "Match is full"
	}
	return s, true, ""
}

func (m *Match) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	for _, p := range presences {
		userID := p.GetUserId()
		s.Presences[userID] = p
		m.assignSeat(logger, s, dispatcher, userID)

		if s.OwnerID == "" {
			s.OwnerID = userID
			logger.Info("Player %s set as match owner", userID)
			adapter.BroadcastOwnerUpdate(dispatcher, s.OwnerID)
		}

		logger.Info("Player %s joined match", userID)

		if s.Game.IsPlaying() {
			if s.Game.HasPlayer(userID) {
				adapter.SendMatchState(dispatcher, s.Game.Snapshot(), s.Seats[:], p)
				adapter.SendHand(dispatcher, userID, s.Game.HandOf(userID), []runtime.Presence{p})
			} else {
				s.Spectators[userID] = true
				adapter.SendMatchState(dispatcher, s.Game.Snapshot(), s.Seats[:], p)
			}
		}
	}
	return s
}

func (m *Match) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	ownerLeft := false

	for _, p := range presences {
		userID := p.GetUserId()
		if userID == s.OwnerID {
			ownerLeft = true
		}
		m.freeSeat(s, dispatcher, userID)
		delete(s.Presences, userID)
		delete(s.Spectators, userID)
		logger.Info("Player %s left match", userID)
	}

	if len(s.Presences) == 0 {
		logger.Info("No players remain, destroying match")
		return nil
	}

	if ownerLeft {
		for uid := range s.Presences {
			s.OwnerID = uid
			break
		}
		logger.Info("New match owner: %s", s.OwnerID)
		adapter.BroadcastOwnerUpdate(dispatcher, s.OwnerID)
	}

	adapter.BroadcastPlayerLeft(dispatcher, seatsAsSlice(s.Seats), s.OwnerID, s.Game)

	return s
}

func (m *Match) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*MatchState)

	select {
	case <-ctx.Done():
		logger.Info("Context cancelled, terminating match loop")
		return s
	default:
	}

	for _, msg := range messages {
		m.handleMessage(s, dispatcher, logger, msg)
	}

	return s
}

func (m *Match) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, graceSeconds int) interface{} {
	return state
}

func (m *Match) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, "Signal received: " + data
}

// --- Message Handling ---

func (m *Match) handleMessage(s *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, msg runtime.MatchData) {
	if msg == nil {
		return
	}

	opCode := pb.OpCode(msg.GetOpCode())
	senderID := msg.GetUserId()
	senderPresence, ok := s.Presences[senderID]
	if !ok {
		logger.Warn("Unknown sender %s", senderID)
		return
	}

	switch opCode {
	case pb.OpCode_OP_MATCH_START_REQUEST:
		if s.Game.IsPlaying() {
			sendError(dispatcher, senderPresence, "Match already started")
			return
		}
		if err := m.startMatch(s, dispatcher); err != nil {
			sendError(dispatcher, senderPresence, err.Error())
			return
		}
	case pb.OpCode_OP_PLAY_CARD:
		req := &pb.PlayCardRequest{}
		if err := proto.Unmarshal(msg.GetData(), req); err != nil {
			sendError(dispatcher, senderPresence, "Invalid play request")
			return
		}
		indices := make([]int, 0, len(req.CardIndices))
		for _, idx := range req.CardIndices {
			indices = append(indices, int(idx))
		}
		events, err := s.Game.PlayCards(senderID, indices)
		if err != nil {
			sendError(dispatcher, senderPresence, err.Error())
			return
		}
		adapter.DispatchEvents(dispatcher, s.Presences, events)
	case pb.OpCode_OP_PASS:
		events, err := s.Game.Pass(senderID)
		if err != nil {
			sendError(dispatcher, senderPresence, err.Error())
			return
		}
		adapter.DispatchEvents(dispatcher, s.Presences, events)
	default:
		logger.Warn("Unhandled opcode: %d", opCode)
	}
}

func (m *Match) startMatch(s *MatchState, dispatcher runtime.MatchDispatcher) error {
	activePlayers := m.orderedSeatedPlayers(s)
	if len(activePlayers) == 0 {
		return errors.New("no active players to start")
	}

	rand.Seed(time.Now().UnixNano())
	events, err := s.Game.Start(activePlayers, s.OwnerID)
	if err != nil {
		return err
	}
	adapter.DispatchEvents(dispatcher, s.Presences, events)
	return nil
}

// --- Helpers ---

func sendError(dispatcher runtime.MatchDispatcher, p runtime.Presence, msg string) {
	data := []byte(msg)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_ERROR), data, []runtime.Presence{p}, nil, true)
}

func (m *Match) findOpenSeat(s *MatchState) int {
	for i := 0; i < len(s.Seats); i++ {
		if s.Seats[i] == "" {
			return i
		}
	}
	return -1
}

func (m *Match) assignSeat(logger runtime.Logger, s *MatchState, dispatcher runtime.MatchDispatcher, userID string) {
	// If user already seated, keep existing seat (handles reconnects).
	if _, exists := s.SeatByUser[userID]; exists {
		return
	}
	slot := m.findOpenSeat(s)
	if slot == -1 {
		logger.Warn("No free seat for user %s", userID)
		return
	}
	s.Seats[slot] = userID
	s.SeatByUser[userID] = slot
	adapter.BroadcastPlayerJoined(dispatcher, seatsAsSlice(s.Seats), s.OwnerID, s.Game)
}

func (m *Match) freeSeat(s *MatchState, dispatcher runtime.MatchDispatcher, userID string) {
	slot, ok := s.SeatByUser[userID]
	if !ok {
		return
	}
	s.Seats[slot] = ""
	delete(s.SeatByUser, userID)
}

func (m *Match) orderedSeatedPlayers(s *MatchState) []string {
	players := make([]string, 0, len(s.SeatByUser))
	for _, uid := range s.Seats {
		if uid == "" {
			continue
		}
		if s.Spectators[uid] {
			continue
		}
		players = append(players, uid)
	}
	return players
}

func seatsAsSlice(seats [4]string) []string {
	out := make([]string, len(seats))
	copy(out, seats[:])
	return out
}
