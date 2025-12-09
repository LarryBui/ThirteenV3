package match

import (
	"context"
	"database/sql"
	"math/rand"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/logic"
	"github.com/yourusername/tienlen-server/pb"
	"google.golang.org/protobuf/proto"
)

type MatchState struct {
	Presences      map[string]runtime.Presence `json:"presences"`
	Hands          map[string][]*pb.Card       `json:"hands"`
	Board          []*pb.Card                  `json:"board"`
	TurnOrder      []string                    `json:"turn_order"`
	CurrentIdx     int                         `json:"current_idx"`
	IsPlaying      bool                        `json:"is_playing"`
	OwnerID        string                      `json:"owner_id"`
	Spectators     map[string]bool             `json:"spectators"`       // Players watching (no hand)
	Deck           []*pb.Card                  `json:"-"`                // Cached deck (not serialized)
	StartTime      int64                       `json:"start_time"`       // When game started (Unix timestamp)
	LastUpdateTick int64                       `json:"last_update_tick"` // Track last tick for recovery
	LastActor      string                      `json:"last_actor"`       // UserID who played current table cards
	RoundSkippers  map[string]bool             `json:"round_skippers"`   // UserIDs locked out this round (passed)
}

type Match struct{}

func (m *Match) MatchInit(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, params map[string]interface{}) (interface{}, int, string) {
	logger.Info("Match initialized")
	state := &MatchState{
		Presences:      make(map[string]runtime.Presence),
		Hands:          make(map[string][]*pb.Card),
		Spectators:     make(map[string]bool),
		RoundSkippers:  make(map[string]bool),
		IsPlaying:      false,
		OwnerID:        "",
		Deck:           createDeck(),
		StartTime:      0,
		LastUpdateTick: 0,
		LastActor:      "",
	}
	return state, 10, "TienLen"
}

func (m *Match) MatchJoinAttempt(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presence runtime.Presence, metadata map[string]string) (interface{}, bool, string) {
	s := state.(*MatchState)
	// Allow joining if match is not full (max 4 players)
	// Players can join even if game is in progress (they will spectate)
	if len(s.Presences) >= 4 {
		return s, false, "Match is full"
	}
	return s, true, ""
}

func (m *Match) MatchJoin(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, presences []runtime.Presence) interface{} {
	s := state.(*MatchState)
	for _, p := range presences {
		s.Presences[p.GetUserId()] = p
		userID := p.GetUserId()

		// Assign owner if this is the first player to join
		if s.OwnerID == "" {
			s.OwnerID = userID
			logger.Info("Player %s is new match owner.", s.OwnerID)
			broadcastOwnerUpdate(s, dispatcher)
		}

		logger.Info("Player %s joined match. IsPlaying=%v, PlayersCount=%d", userID, s.IsPlaying, len(s.Presences))

		// If game is in progress, send current match state to the new joiner
		if s.IsPlaying {
			// Check if player had a hand before (reconnect) or is a new spectator
			if _, hasHand := s.Hands[userID]; hasHand {
				// Returning player with their original hand
				logger.Info("Player %s rejoining with existing hand", userID)
				sendMatchState(s, dispatcher, logger, p)
			} else {
				// New spectator joining mid-game
				s.Spectators[userID] = true
				logger.Info("Player %s joining as spectator", userID)
				sendMatchState(s, dispatcher, logger, p)
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
		delete(s.Presences, userID)
		delete(s.Spectators, userID)

		// Keep hands in case player reconnects
		// s.Hands[userID] is preserved for potential rejoin
		logger.Info("Player %s left match", userID)
	}

	if ownerLeft {
		if len(s.Presences) > 0 {
			// Pick a new owner from active players (preferring non-spectators)
			for userID := range s.Presences {
				if !s.Spectators[userID] {
					s.OwnerID = userID
					logger.Info("Previous owner left. New owner is: %s", s.OwnerID)
					break
				}
			}
			// If all remaining are spectators, pick any
			if s.OwnerID == "" {
				for userID := range s.Presences {
					s.OwnerID = userID
					logger.Info("Previous owner left. New owner (spectator) is: %s", s.OwnerID)
					break
				}
			}
			broadcastOwnerUpdate(s, dispatcher)
		} else {
			// No players left, clear owner
			s.OwnerID = ""
			logger.Info("All players left. OwnerID cleared.")
		}
	}

	if len(s.Presences) == 0 {
		logger.Info("Match destroyed - no players remaining")
		return nil // Destroy match
	}

	return s
}

// broadcastOwnerUpdate sends an OP_OWNER_UPDATE message to all players with the current owner's ID.
func broadcastOwnerUpdate(s *MatchState, dispatcher runtime.MatchDispatcher) {
	data := []byte(s.OwnerID)
	// Sending to nil presences broadcasts to all connected presences in the match.
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_OWNER_UPDATE), data, nil, nil, true)
}

// sendMatchState is defined below with full implementation

func (m *Match) MatchLoop(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, dispatcher runtime.MatchDispatcher, tick int64, state interface{}, messages []runtime.MatchData) interface{} {
	s := state.(*MatchState)

	// Check if context is cancelled (match should terminate)
	select {
	case <-ctx.Done():
		logger.Info("MatchLoop context cancelled, terminating")
		return s
	default:
	}

	// Log every 100 ticks (10 seconds at 10 ticks/sec)
	if tick%100 == 0 {
		logger.Info("MatchLoop: tick=%d, isPlaying=%v, players=%d, spectators=%d, boardCards=%d",
			tick, s.IsPlaying, len(s.Presences), len(s.Spectators), len(s.Board))
	}

	// Track last update tick for recovery
	s.LastUpdateTick = tick

	// Process all incoming messages
	for _, msg := range messages {
		if len(messages) > 0 {
			logger.Info("Processing message: opCode=%d from user %s", msg.GetOpCode(), msg.GetUserId())
		}
		handleMessage(m, s, msg, dispatcher, logger)
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

// playerIndex returns the index of a player within the current turn order, or -1 if not found.
func (s *MatchState) playerIndex(userID string) int {
	for i, uid := range s.TurnOrder {
		if uid == userID {
			return i
		}
	}
	return -1
}

// resetRoundState clears the table and unlocks players for a new round.
func (s *MatchState) resetRoundState() {
	s.Board = nil
	s.RoundSkippers = make(map[string]bool)
	s.LastActor = ""
}

func (m *Match) RotateTurn(s *MatchState, logger runtime.Logger, dispatcher runtime.MatchDispatcher) {
	if len(s.TurnOrder) == 0 {
		logger.Error("TurnOrder is empty, cannot rotate")
		return
	}

	count := len(s.TurnOrder)
	originalIdx := s.CurrentIdx
	if originalIdx < 0 || originalIdx >= count {
		logger.Warn("CurrentIdx %d out of bounds, resetting to 0", originalIdx)
		originalIdx = 0
		s.CurrentIdx = 0
	}

	// Loop to find next valid player (not locked out)
	for i := 1; i <= count; i++ {
		nextIdx := (originalIdx + i) % count
		nextPlayerID := s.TurnOrder[nextIdx]

		// Win Condition: turn loops back to LastActor
		if s.LastActor != "" && nextPlayerID == s.LastActor {
			// Everyone else passed; LastActor wins this round
			m.EndRound(s, nextPlayerID, dispatcher, logger)
			return
		}

		// Skip locked-out players
		if s.RoundSkippers[nextPlayerID] {
			continue
		}

		// Valid player found
		s.CurrentIdx = nextIdx
		broadcastTurn(s, dispatcher)
		return
	}

	// Fallback (should not reach if lastActor is set correctly)
	logger.Warn("No valid players to rotate to")
}

func (m *Match) EndRound(s *MatchState, winnerID string, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	logger.Info("Round ended. Winner: %s", winnerID)

	// 1. Clear round state
	s.resetRoundState()

	// 2. Set turn to winner (they play first next round)
	winnerIdx := s.playerIndex(winnerID)
	if winnerIdx >= 0 {
		s.CurrentIdx = winnerIdx
	} else {
		logger.Warn("Winner %s not found in turn order", winnerID)
	}

	// 3. Broadcast round end event
	packet := &pb.RoundEndPacket{
		WinnerId: winnerID,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		logger.Error("Failed to marshal RoundEndPacket: %v", err)
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_ROUND_END), data, nil, nil, true)

	// 4. Broadcast turn update so next player knows to play
	broadcastTurn(s, dispatcher)
}

func startMatch(s *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	// Validate we can start the game
	activePlayers := 0
	for userID := range s.Presences {
		if !s.Spectators[userID] {
			activePlayers++
		}
	}

	if activePlayers < 1 {
		logger.Error("Cannot start match with no players (have %d)", activePlayers)
		return
	}

	s.resetRoundState()
	s.IsPlaying = true
	s.StartTime = time.Now().Unix()
	s.CurrentIdx = 0

	logger.Info("Starting Match with %d active players + %d spectators", activePlayers, len(s.Spectators))

	// Shuffle the deck before dealing
	// Always create a fresh deck for a new match, then shuffle
	s.Deck = createDeck()
	rand.Seed(time.Now().UnixNano())
	rand.Shuffle(len(s.Deck), func(i, j int) { s.Deck[i], s.Deck[j] = s.Deck[j], s.Deck[i] })

	deck := s.Deck // Use shuffled deck

	// Only include active players (non-spectators) in TurnOrder
	s.TurnOrder = make([]string, 0, activePlayers)
	for userID := range s.Presences {
		if !s.Spectators[userID] {
			s.TurnOrder = append(s.TurnOrder, userID)
		}
	}

	// Shuffle the turn order
	rand.Seed(time.Now().UnixNano())
	rand.Shuffle(len(s.TurnOrder), func(i, j int) { s.TurnOrder[i], s.TurnOrder[j] = s.TurnOrder[j], s.TurnOrder[i] })

	s.Hands = make(map[string][]*pb.Card) // Clear previous hands

	// Deal exactly 13 cards to each active player
	cardsPerPlayer := 13
	totalCardsNeeded := len(s.TurnOrder) * cardsPerPlayer
	if len(deck) < totalCardsNeeded {
		logger.Warn("Not enough cards: %d cards available for %d players (need %d)", len(deck), len(s.TurnOrder), totalCardsNeeded)
		return
	}

	//always deal 13 cards to 4 players

	for i, userID := range s.TurnOrder {
		startIdx := i * cardsPerPlayer
		endIdx := startIdx + cardsPerPlayer
		hand := deck[startIdx:endIdx]
		logic.SortCards(hand)
		s.Hands[userID] = hand
		logger.Info("Dealt %d cards to player %s", len(hand), userID)

		// Send initial hand to each active player
		packet := &pb.MatchStartPacket{
			Hand:      hand,
			PlayerIds: s.TurnOrder,
			OwnerId:   s.OwnerID,
		}
		data, err := proto.Marshal(packet)
		if err != nil {
			logger.Error("Failed to marshal MatchStartPacket for user %s: %v", userID, err)
			continue
		}
		dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_START), data, []runtime.Presence{s.Presences[userID]}, nil, true)
	}

	s.CurrentIdx = 0
	broadcastTurn(s, dispatcher)
}

func handleMessage(m *Match, s *MatchState, msg runtime.MatchData, dispatcher runtime.MatchDispatcher, logger runtime.Logger) {
	if msg == nil {
		logger.Warn("Received nil message")
		return
	}

	opCode := pb.OpCode(msg.GetOpCode())
	senderID := msg.GetUserId()

	logger.Info("handleMessage: opCode=%d, senderID=%s, messageSize=%d bytes", opCode, senderID, len(msg.GetData()))

	// Validate sender is in match
	senderPresence, ok := s.Presences[senderID]
	if !ok {
		logger.Warn("Sender not in presences: %s", senderID)
		return
	}

	// Prevent spectators from playing cards
	if s.Spectators[senderID] && opCode == pb.OpCode_OP_PLAY_CARD {
		logger.Warn("Spectator %s attempted to play cards", senderID)
		sendError(dispatcher, senderPresence, "You are spectating")
		return
	}

	switch opCode {
	case pb.OpCode_OP_PLAY_CARD:
		logger.Info("Handling OP_PLAY_CARD from %s", senderID)
		req := &pb.PlayCardRequest{}
		if err := proto.Unmarshal(msg.GetData(), req); err != nil {
			logger.Error("Bad play request: %v", err)
			sendError(dispatcher, senderPresence, "Invalid request format")
			return
		}

		// Validate it's player's turn
		if len(s.TurnOrder) == 0 {
			logger.Error("TurnOrder is empty")
			sendError(dispatcher, senderPresence, "Game state error")
			return
		}

		if s.TurnOrder[s.CurrentIdx] != senderID {
			logger.Warn("Player %s tried to play but it's %s's turn", senderID, s.TurnOrder[s.CurrentIdx])
			sendError(dispatcher, senderPresence, "Not your turn")
			return
		}

		playerHand := s.Hands[senderID]
		if len(playerHand) == 0 {
			sendError(dispatcher, senderPresence, "You have no cards")
			return
		}

		// Validate and extract cards to play
		cardsToPlay := []*pb.Card{}
		for _, idx := range req.CardIndices {
			if idx < 0 || int(idx) >= len(playerHand) {
				logger.Warn("Invalid card index: %d (hand size: %d)", idx, len(playerHand))
				sendError(dispatcher, senderPresence, "Invalid card index")
				return
			}
			cardsToPlay = append(cardsToPlay, playerHand[int(idx)])
		}

		if len(cardsToPlay) == 0 {
			sendError(dispatcher, senderPresence, "No cards selected")
			return
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
		s.LastActor = senderID                  // Mark this player as table owner
		s.RoundSkippers = make(map[string]bool) // Reset lockouts; new cards on table

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

		// Send updated hand to the player
		handPacket := &pb.HandUpdatePacket{
			Hand: newHand,
		}
		handData, err := proto.Marshal(handPacket)
		if err != nil {
			logger.Error("Failed to marshal HandUpdatePacket: %v", err)
		} else {
			dispatcher.BroadcastMessage(int64(pb.OpCode_OP_HAND_UPDATE), handData, []runtime.Presence{senderPresence}, nil, true)
		}

		if len(newHand) == 0 {
			// Player has no cards left, they win the MATCH!
			s.IsPlaying = false
			logger.Info("Player %s won the game! Remaining players: %d", senderID, len(s.Presences))

			// Broadcast game over with winner
			broadcastGameOver(s, dispatcher, senderID, logger)
			logger.Info("Game over broadcast sent")
		} else {
			// Rotate to next player for this round
			m.RotateTurn(s, logger, dispatcher)
		}

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

	case pb.OpCode_OP_PASS:
		logger.Info("Player %s passed", senderID)

		// Validate it's their turn
		if len(s.TurnOrder) == 0 {
			sendError(dispatcher, senderPresence, "Game state error")
			return
		}
		if s.TurnOrder[s.CurrentIdx] != senderID {
			sendError(dispatcher, senderPresence, "Not your turn")
			return
		}

		// Validate a LastActor exists (cards are on table)
		if s.LastActor == "" {
			sendError(dispatcher, senderPresence, "No cards on table")
			return
		}

		// Lock player out for this round
		s.RoundSkippers[senderID] = true
		logger.Info("Player %s is now locked out this round", senderID)

		// Rotate to next player
		m.RotateTurn(s, logger, dispatcher)

	default:
		logger.Warn("Unknown opCode: %d", msg.GetOpCode())
	}
}

func broadcastTurn(s *MatchState, dispatcher runtime.MatchDispatcher) {
	if len(s.TurnOrder) == 0 || s.CurrentIdx < 0 || s.CurrentIdx >= len(s.TurnOrder) {
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

// sendMatchState sends the current match state to a specific player (e.g., a late joiner).
func sendMatchState(s *MatchState, dispatcher runtime.MatchDispatcher, logger runtime.Logger, receiver runtime.Presence) {
	playerIDs := make([]string, 0, len(s.Presences))
	for userID := range s.Presences {
		playerIDs = append(playerIDs, userID)
	}

	activePlayerID := ""
	if len(s.TurnOrder) > 0 && s.CurrentIdx >= 0 && s.CurrentIdx < len(s.TurnOrder) {
		activePlayerID = s.TurnOrder[s.CurrentIdx]
	}

	packet := &pb.MatchStatePacket{
		IsPlaying:      s.IsPlaying,
		OwnerId:        s.OwnerID,
		Board:          s.Board,
		ActivePlayerId: activePlayerID,
		PlayerIds:      playerIDs,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		logger.Error("Failed to marshal MatchStatePacket: %v", err)
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_MATCH_STATE), data, []runtime.Presence{receiver}, nil, true)
}

// broadcastGameOver sends an OP_GAME_OVER message to all players.
func broadcastGameOver(s *MatchState, dispatcher runtime.MatchDispatcher, winnerID string, logger runtime.Logger) {
	packet := &pb.GameOverPacket{
		WinnerId: winnerID,
	}
	data, err := proto.Marshal(packet)
	if err != nil {
		logger.Error("Failed to marshal GameOverPacket: %v", err)
		return
	}
	dispatcher.BroadcastMessage(int64(pb.OpCode_OP_GAME_OVER), data, nil, nil, true)
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
