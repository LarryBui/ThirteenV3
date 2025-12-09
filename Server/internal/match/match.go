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
	Board      []*pb.Card                  `json:"board"`
	TurnOrder  []string                    `json:"turn_order"`
	CurrentIdx int                         `json:"current_idx"`
	IsPlaying  bool                        `json:"is_playing"`
	OwnerID    string                      `json:"owner_id"` // New field
	Deck       []*pb.Card                  `json:"-"` // Cached deck (not serialized)
}

type Match struct{}

func (m *Match) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Info("Match initialized")
	state := &MatchState{
		Presences: make(map[string]runtime.Presence),
		Hands:     make(map[string][]*pb.Card),
		IsPlaying: false,
		OwnerID:   "", // Initialize OwnerID
		Deck:      createDeck(), // Pre-create deck once
	}
	return state, 10, "TienLen"
}

func (m *Match) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*MatchState)
	if s.IsPlaying {
		return s, false, "Match already in progress"
	}
	if len(s.Presences) >= 4 {
		return s, false, "Match is full"
	}
	return s, true, ""
}

func (m *Match) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	for _, p := range presences {
		s.Presences[p.GetUserId()] = p
		// Assign owner if this is the first player to join
		if s.OwnerID == "" {
			s.OwnerID = p.GetUserId()
			logger.Info("Player %s is new match owner.", s.OwnerID)
			// Broadcast the new owner
			broadcastOwnerUpdate(s, dispatcher)
		}
	}
	return s
}

func (m *Match) MatchLeave(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	ownerLeft := false
	for _, p := range presences {
		if p.GetUserId() == s.OwnerID {
			ownerLeft = true
		}
		delete(s.Presences, p.GetUserId())
	}

	if ownerLeft {
		if len(s.Presences) > 0 {
			// Pick a new owner randomly from remaining players
			for userID := range s.Presences { // Iterating a map is random enough for this purpose
				s.OwnerID = userID
				logger.Info("Previous owner left. New owner is: %s", s.OwnerID)
				break
			}
		} else {
			// No players left, clear owner
			s.OwnerID = ""
			logger.Info("All players left. OwnerID cleared.")
		}
		broadcastOwnerUpdate(s, dispatcher)
	}

	return s
}

// broadcastOwnerUpdate sends an OP_OWNER_UPDATE message to all players with the current owner's ID.
func broadcastOwnerUpdate(s *MatchState, dispatcher runtime.MatchDispatcher) {
	data := []byte(s.OwnerID)
	// Sending to nil presences broadcasts to all connected presences in the match.
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_OWNER_UPDATE), data, nil, nil, true)
}

func (m *Match) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {

	logger.Info("MatchLoop called")

	s := state.(*MatchState)

	// Check if context is cancelled (match should terminate)
	select {
	case <-ctx.Done():
		logger.Info("MatchLoop context cancelled, terminating")
		return s
	default:
	}

	if tick%10 == 0 {
		logger.Info("MatchLoop: tick=%d, messages=%d, presences=%d", tick, len(messages), len(s.Presences))
	}

	for _, msg := range messages {
		if len(messages) > 0 {
			logger.Info("Processing message: opCode=%d", msg.GetOpCode())
		}
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

	deck := s.Deck // Use cached deck
	if len(deck) == 0 {
		logger.Error("Deck is empty!")
		return
	}

	i := 0
	s.TurnOrder = []string{}

	for userID := range s.Presences {
		if i*13+13 > len(deck) {
			break
		}
		hand := deck[i*13 : i*13+13]
		logic.SortCards(hand)
		s.Hands[userID] = hand
		s.TurnOrder = append(s.TurnOrder, userID)
		i++

		packet := &pb.MatchStartPacket{
			Hand:      hand,
			PlayerIds: s.TurnOrder,
		}
		data, err := proto.Marshal(packet)
		if err != nil {
			logger.Error("Failed to marshal MatchStartPacket: %v", err)
			continue
		}
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_START), data, []runtime.Presence{s.Presences[userID]}, nil, true)
	}

	s.CurrentIdx = 0
	broadcastTurn(s, dispatcher)
}

func handleMessage(s *MatchState, msg runtime.MatchData, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	opCode := pb.OpCode(msg.GetOpCode())
	senderID := msg.GetUserId()

	logger.Info("handleMessage: opCode=%d, senderID=%s", opCode, senderID)

	senderPresence, ok := s.Presences[senderID]
	if !ok {
		logger.Warn("Sender not in presences: %s", senderID)
		return
	}

	switch opCode {
	case pb.OpCode_OP_PLAY_CARD:
		logger.Info("Handling OP_PLAY_CARD")
		req := &pb.PlayCardRequest{}
		if err := proto.Unmarshal(msg.GetData(), req); err != nil {
			logger.Error("Bad play request: %v", err)
			return
		}

		if len(s.TurnOrder) == 0 || s.TurnOrder[s.CurrentIdx] != senderID {
			sendError(dispatcher, senderPresence, "Not your turn")
			return
		}

		playerHand := s.Hands[senderID]
		cardsToPlay := []*pb.Card{}

		// Validate card indices before accessing
		for _, idx := range req.CardIndices {
			if idx < 0 || int(idx) >= len(playerHand) {
				sendError(dispatcher, senderPresence, "Invalid card index")
				return
			}
			cardsToPlay = append(cardsToPlay, playerHand[idx])
		}

		if !logic.IsValidSet(cardsToPlay) {
			sendError(dispatcher, senderPresence, "Invalid card combination")
			return
		}

		if len(s.Board) > 0 {
			if !logic.CanBeat(s.Board, cardsToPlay) {
				sendError(dispatcher, senderPresence, "Cannot beat current cards")
				return
			}
		}

		s.Board = cardsToPlay

		newHand := []*pb.Card{}
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

		s.CurrentIdx = (s.CurrentIdx + 1) % len(s.TurnOrder)
		broadcastTurn(s, dispatcher)

	case pb.OpCode_OP_MATCH_START_REQUEST:
		logger.Info("Handling OP_MATCH_START_REQUEST from %s", senderID)
		if s.IsPlaying {
			sendError(dispatcher, senderPresence, "Match already started")
			return
		}
		if len(s.Presences) < 1 {
			sendError(dispatcher, senderPresence, "Not enough players to start")
			return
		}
		startMatch(s, dispatcher, logger)

	default:
		logger.Warn("Unknown opCode: %d", msg.GetOpCode())
	}
}

func broadcastTurn(s *MatchState, dispatcher runtime.MatchDispatcher) {
	if len(s.TurnOrder) == 0 {
		return
	}
	packet := &pb.TurnUpdatePacket{
		ActivePlayerId:   s.TurnOrder[s.CurrentIdx],
		LastPlayedCards:  s.Board,
		SecondsRemaining: 30,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		// Logger not available in helper, but this should never happen
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_TURN_UPDATE), data, nil, nil, true)
}

func sendError(dispatcher runtime.MatchDispatcher, p runtime.Presence, msg string) {
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
	return deck
}
