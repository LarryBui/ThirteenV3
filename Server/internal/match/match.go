package match

import (
	"context"
	"database/sql"

	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/logic"
	"github.com/yourusername/tienlen-server/pb"
	"google.golang.org/protobuf/proto"
)

type MatchState struct {
	Presences  map[string]runtime.Presence `json:"presences"`
	Hands      map[string][]*pb.Card       `json:"hands"`
	Board      []*pb.Card                  `json:"board"` // Last played set
	TurnOrder  []string                    `json:"turn_order"`
	CurrentIdx int                         `json:"current_idx"`
	IsPlaying  bool                        `json:"is_playing"`
}

type Match struct{}

func (m *Match) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	state := &MatchState{
		Presences: make(map[string]runtime.Presence),
		Hands:     make(map[string][]*pb.Card),
		IsPlaying: false,
	}
	tickRate := 1 // 1 tick per second is enough for turn-based, or higher for responsiveness
	label := "TienLen"
	return state, tickRate, label
}

func (m *Match) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*MatchState)
	if s.IsPlaying {
		return s, false, "Match already in progress"
	}
	return s, true, ""
}

func (m *Match) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	for _, p := range presences {
		s.Presences[p.GetUserId()] = p
	}
	return s
}

func (m *Match) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	for _, p := range presences {
		delete(s.Presences, p.GetUserId())
		// Handle player leaving mid-game (forfeit?)
	}
	return s
}

func (m *Match) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*MatchState)

	// Removed auto-start logic. Game start is now explicitly requested by host.

	for _, msg := range messages {
		handleMessage(s, msg, dispatcher, logger)
	}

	return s
}

func (m *Match) MatchTerminate(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, graceSeconds int) interface{} {
	return state
}

func (m *Match) MatchSignal(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, data string) (interface{}, string) {
	return state, "Signal received: " + data
}

// --- Helpers ---

func startMatch(s *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	s.IsPlaying = true
	logger.Info("Starting Match with %d players", len(s.Presences))

	// 1. Create Deck
	deck := createDeck()
	// TODO: Shuffle the deck
	// For now, deal from a sorted deck (predictable for testing)

	// 2. Deal Cards
	i := 0
	for userID, _ := range s.Presences {
		hand := deck[i*13 : (i+1)*13]
		logic.SortCards(hand) // Sort for player convenience
		s.Hands[userID] = hand
		s.TurnOrder = append(s.TurnOrder, userID)
		i++

		// Send Hand to Player
		packet := &pb.MatchStartPacket{
			Hand:      hand,
			PlayerIds: nil, // TODO: Send order (will need to map user IDs to usernames)
		}
		data, _ := proto.Marshal(packet)
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_START), data, []runtime.Presence{s.Presences[userID]}, nil, true)
	}

	s.CurrentIdx = 0 // First player
	broadcastTurn(s, dispatcher)
}

func handleMessage(s *MatchState, msg runtime.MatchData, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	// Parse OpCode
	opCode := pb.OpCode(msg.GetOpCode())

	// Get sender presence
	senderID := msg.GetUserId()
	senderPresence, ok := s.Presences[senderID]
	if !ok {
		return // Should not happen
	}

	switch opCode {
	case pb.OpCode_OP_PLAY_CARD:
		req := &pb.PlayCardRequest{}
		if err := proto.Unmarshal(msg.GetData(), req); err != nil {
			logger.Error("Bad play request: %v", err)
			return
		}

		if s.TurnOrder[s.CurrentIdx] != senderID {
			sendError(dispatcher, senderPresence, "Not your turn")
			return
		}

		// Validation Logic
		playerHand := s.Hands[senderID]
		cardsToPlay := []*pb.Card{}

		// Map indices to cards
		// Note: Robust implementation needs checks for out of bounds
		for _, idx := range req.CardIndices {
			if int(idx) < len(playerHand) {
				cardsToPlay = append(cardsToPlay, playerHand[idx])
			}
		}

		// Check Rules
		if !logic.IsValidSet(cardsToPlay) {
			sendError(dispatcher, senderPresence, "Invalid card combination")
			return
		}

		// Check if beats board
		if len(s.Board) > 0 {
			if !logic.CanBeat(s.Board, cardsToPlay) {
				sendError(dispatcher, senderPresence, "Cannot beat current cards")
				return
			}
		}

		// Apply Move
		s.Board = cardsToPlay

		// Remove cards from hand
		newHand := []*pb.Card{}
		// Basic filter: keep cards that were NOT played
		// This logic relies on indices from request being accurate to the server's state.
		// Ideally we remove by Value/Suit match.
		for _, c := range playerHand {
			played := false
			for _, pc := range cardsToPlay {
				if c.Rank == pc.Rank && c.Suit == pc.Suit {
					played = true
					break
				}
			}
			if !played {
				newHand = append(newHand, c)
			}
		}
		s.Hands[senderID] = newHand

		// Next Turn
		s.CurrentIdx = (s.CurrentIdx + 1) % len(s.TurnOrder)
		broadcastTurn(s, dispatcher)

	case pb.OpCode_OP_MATCH_START_REQUEST:
		logger.Info("Match start request received from %s. IsPlaying=%v, Presences=%d", senderID, s.IsPlaying, len(s.Presences))
		if s.IsPlaying {
			logger.Warn("Start request rejected: Match already started")
			sendError(dispatcher, senderPresence, "Match already started")
			return
		}
		if len(s.Presences) < 1 {
			logger.Warn("Start request rejected: Not enough players (%d). Need at least 1.", len(s.Presences))
			sendError(dispatcher, senderPresence, "Not enough players to start")
			return
		}
		logger.Info("Starting match with %d players", len(s.Presences))
		startMatch(s, dispatcher, logger)
	}
}

func broadcastTurn(s *MatchState, dispatcher runtime.MatchDispatcher) {
	packet := &pb.TurnUpdatePacket{
		ActivePlayerId:   s.TurnOrder[s.CurrentIdx],
		LastPlayedCards:  s.Board,
		SecondsRemaining: 30,
	}
	data, _ := proto.Marshal(packet)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_TURN_UPDATE), data, nil, nil, true)
}

func sendError(dispatcher runtime.MatchDispatcher, p runtime.Presence, msg string) {
	// Send OP_ERROR to specific player
	data := []byte(msg)
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_ERROR), data, []runtime.Presence{p}, nil, true)
}

func createDeck() []*pb.Card {
	deck := []*pb.Card{}
	for r := 0; r <= 12; r++ {
		for s := 0; s <= 3; s++ {
			deck = append(deck, &pb.Card{Rank: int32(r), Suit: int32(s)})
		}
	}
	// TODO: Shuffle
	return deck
}
