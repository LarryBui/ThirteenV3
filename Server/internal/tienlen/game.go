package tienlen

import (
	"errors"
	"fmt"
	"math/rand"
)

// Event marker interface implemented by all domain events.
type Event interface{}

type MatchStarted struct {
	Hands     map[string][]Card
	TurnOrder []string
	OwnerID   string
}

type HandUpdated struct {
	PlayerID string
	Hand     []Card
}

type TurnChanged struct {
	ActivePlayerID string
	Board          []Card
}

type RoundEnded struct {
	WinnerID string
}

// PlayerFinished is emitted when a player empties their hand.
// Rank indicates their finishing position (1st, 2nd, 3rd, etc.).
type PlayerFinished struct {
	PlayerID string
	Rank     int // 1st, 2nd, 3rd
}

type GameOver struct {
	WinnerID string
}

// Snapshot captures lightweight game state for late joiners.
type Snapshot struct {
	IsPlaying      bool
	OwnerID        string
	Board          []Card
	ActivePlayerID string
	PlayerIDs      []string // Seat/turn order as assigned by the match
	Winners        []string
	FinishedPlayers map[string]bool
}

// Game contains pure Tien Len state and rules.
type Game struct {
	Hands         map[string][]Card
	TurnOrder     []string
	CurrentIdx    int
	Board         []Card
	LastActor     string
	RoundSkippers map[string]bool
	OwnerID       string
	isPlaying     bool
	
	// Winners tracks the players who have finished their hands, in order of finishing.
	// Winners[0] is the 1st place winner, Winners[1] is 2nd, etc.
	Winners       []string
	
	// FinishedPlayers is a set of userIDs for players who have emptied their hands.
	// Used to skip these players during turn advancement.
	FinishedPlayers map[string]bool
}

func NewGame() *Game {
	return &Game{
		Hands:         make(map[string][]Card),
		RoundSkippers: make(map[string]bool),
		CurrentIdx:    0,
		Winners:       make([]string, 0, 3), // Max 3 winners in a 4-player game
		FinishedPlayers: make(map[string]bool),
	}
}

func (g *Game) IsPlaying() bool {
	return g.isPlaying
}

func (g *Game) HasPlayer(userID string) bool {
	_, ok := g.Hands[userID]
	return ok
}

func (g *Game) HandOf(userID string) []Card {
	hand := g.Hands[userID]
	out := make([]Card, len(hand))
	copy(out, hand)
	return out
}

func (g *Game) Snapshot() Snapshot {
	activeID := ""
	if len(g.TurnOrder) > 0 && g.CurrentIdx >= 0 && g.CurrentIdx < len(g.TurnOrder) {
		activeID = g.TurnOrder[g.CurrentIdx]
	}
	playerIDs := make([]string, len(g.TurnOrder))
	copy(playerIDs, g.TurnOrder)

	// Create a copy of FinishedPlayers map
	finishedPlayersCopy := make(map[string]bool, len(g.FinishedPlayers))
	for k, v := range g.FinishedPlayers {
		finishedPlayersCopy[k] = v
	}

	return Snapshot{
		IsPlaying:      g.isPlaying,
		OwnerID:        g.OwnerID,
		Board:          append([]Card(nil), g.Board...),
		ActivePlayerID: activeID,
		PlayerIDs:      playerIDs,
		Winners:        append([]string(nil), g.Winners...), // Create a copy of Winners slice
		FinishedPlayers: finishedPlayersCopy,
	}
}

// Start initializes the game with the given players.
// It determines the starting player based on 'lastWinnerID' (winner of previous game).
// If 'lastWinnerID' is empty or not in the game, the player with the smallest card (lowest power) starts.
func (g *Game) Start(players []string, ownerID string, lastWinnerID string) ([]Event, error) {
	if len(players) == 0 {
		return nil, errors.New("no players provided")
	}
	g.OwnerID = ownerID

	turnOrder := make([]string, len(players))
	copy(turnOrder, players)
	rand.Shuffle(len(turnOrder), func(i, j int) { turnOrder[i], turnOrder[j] = turnOrder[j], turnOrder[i] })
	g.TurnOrder = turnOrder

	deck := ShuffleDeck(NewDeck())
	handSize := 13
	if len(deck) < len(turnOrder)*handSize {
		return nil, fmt.Errorf("not enough cards for %d players", len(turnOrder))
	}

	g.Hands = make(map[string][]Card, len(turnOrder))
	for i, uid := range turnOrder {
		start := i * handSize
		end := start + handSize
		hand := append([]Card(nil), deck[start:end]...)
		SortHand(hand)
		g.Hands[uid] = hand
	}

	// Determine starting player
	startIndex := -1

	// 1. Try last winner
	if lastWinnerID != "" {
		for i, uid := range g.TurnOrder {
			if uid == lastWinnerID {
				startIndex = i
				break
			}
		}
	}

	// 2. Fallback to smallest card (if no last winner or last winner left)
	if startIndex == -1 {
		lowestPower := int32(1000) // Start high
		lowestPlayerIndex := 0

		for i, uid := range g.TurnOrder {
			hand := g.Hands[uid]
			if len(hand) > 0 {
				// Hands are sorted, so the first card is the smallest
				power := cardPower(hand[0])
				if power < lowestPower {
					lowestPower = power
					lowestPlayerIndex = i
				}
			}
		}
		startIndex = lowestPlayerIndex
	}

	g.CurrentIdx = startIndex
	g.Board = nil
	g.RoundSkippers = make(map[string]bool)
	g.LastActor = ""
	g.isPlaying = true

	events := []Event{
		MatchStarted{
			Hands:     g.HandsCopy(),
			TurnOrder: turnOrder,
			OwnerID:   g.OwnerID,
		},
		TurnChanged{
			ActivePlayerID: turnOrder[g.CurrentIdx],
			Board:          g.Board,
		},
	}
	return events, nil
}

func (g *Game) PlayCards(playerID string, indices []int) ([]Event, error) {
	if !g.isPlaying {
		return nil, errors.New("match not in progress")
	}
	if len(g.TurnOrder) == 0 || g.TurnOrder[g.CurrentIdx] != playerID {
		return nil, errors.New("not your turn")
	}
	hand, ok := g.Hands[playerID]
	if !ok {
		return nil, errors.New("player has no hand")
	}
	if len(indices) == 0 {
		return nil, errors.New("no cards selected")
	}

	if err := validateIndices(indices, len(hand)); err != nil {
		return nil, err
	}

	cardsToPlay := extractByIndices(hand, indices)
	if !IsValidSet(cardsToPlay) {
		return nil, errors.New("invalid card combination")
	}
	if len(g.Board) > 0 && !CanBeat(g.Board, cardsToPlay) {
		return nil, errors.New("cannot beat current board")
	}

	// Update table
	g.Board = cardsToPlay
	g.LastActor = playerID
	g.RoundSkippers = make(map[string]bool)

	remaining := removeByIndices(hand, indices)
	SortHand(remaining)
	g.Hands[playerID] = remaining

	events := []Event{
		HandUpdated{PlayerID: playerID, Hand: g.HandOf(playerID)},
	}

	// Check if player has finished their hand (Win Condition logic)
	if len(remaining) == 0 {
		g.Winners = append(g.Winners, playerID)
		g.FinishedPlayers[playerID] = true
		
		// Emit PlayerFinished event with their rank (1st, 2nd, 3rd)
		events = append(events, PlayerFinished{PlayerID: playerID, Rank: len(g.Winners)})

		// If this is the (N-1)th player to finish, the game ends.
		// We stop when only one player remains (the loser).
		if len(g.Winners) == len(g.TurnOrder)-1 {
			g.isPlaying = false
			// The overall game winner is the 1st place player
			events = append(events, GameOver{WinnerID: g.Winners[0]})
			return events, nil
		}

		// Player finished, remove them from active turn rotation for subsequent turns
		// but game continues.
		// We can directly advance turn here, but the turn advancement logic in advanceTurn
		// will skip this player if they are in g.FinishedPlayers.
	}
	
	events = append(events, g.advanceTurn()...)
	return events, nil
}

func (g *Game) Pass(playerID string) ([]Event, error) {
	if !g.isPlaying {
		return nil, errors.New("match not in progress")
	}
	if len(g.TurnOrder) == 0 || g.TurnOrder[g.CurrentIdx] != playerID {
		return nil, errors.New("not your turn")
	}
	if g.LastActor == "" {
		return nil, errors.New("no cards on table to pass")
	}
	if g.FinishedPlayers[playerID] { // A finished player cannot pass
		return nil, errors.New("player has already finished")
	}

	g.RoundSkippers[playerID] = true
	return g.advanceTurn(), nil
}

// advanceTurn moves the turn to the next valid player.
// It skips players who have finished their hands or have passed the current round.
// It also handles round endings and resets the board if everyone else skips.
func (g *Game) advanceTurn() []Event {
	events := []Event{}
	count := len(g.TurnOrder)
	if count == 0 {
		return events
	}
	original := g.CurrentIdx

	for i := 1; i <= count; i++ {
		nextIdx := (original + i) % count
		nextID := g.TurnOrder[nextIdx]

		if g.FinishedPlayers[nextID] { // Skip players who have finished their hand
			continue
		}

		// Round ends if we loop back to the last actor and everyone else skipped
		if g.LastActor != "" && nextID == g.LastActor && g.allOthersSkipped(nextID) {
			g.Board = nil
			g.RoundSkippers = make(map[string]bool)
			g.LastActor = "" // Clear last actor after round end
			
			// Find next non-finished player to lead the new round
			g.CurrentIdx = nextIdx
			// Ensure the actual player who leads the new round is an active player
			for g.FinishedPlayers[g.TurnOrder[g.CurrentIdx]] {
				g.CurrentIdx = (g.CurrentIdx + 1) % count
			}
			
			events = append(events,
				RoundEnded{WinnerID: nextID},
				TurnChanged{ActivePlayerID: g.TurnOrder[g.CurrentIdx], Board: g.Board},
			)
			return events
		}

		if g.RoundSkippers[nextID] {
			continue
		}

		g.CurrentIdx = nextIdx
		events = append(events, TurnChanged{ActivePlayerID: nextID, Board: g.Board})
		return events
	}

	// If all else fails, stay on the same player but emit a turn update to avoid deadlock.
	events = append(events, TurnChanged{ActivePlayerID: g.TurnOrder[g.CurrentIdx], Board: g.Board})
	return events
}

// allOthersSkipped checks if all players who are not the given playerID and have not finished
// have marked themselves as skipped in the current round.
func (g *Game) allOthersSkipped(playerID string) bool {
	skippedCount := 0
	activePlayers := 0
	for _, uid := range g.TurnOrder {
		if g.FinishedPlayers[uid] {
			continue // Don't count finished players
		}
		activePlayers++
		if uid == playerID {
			continue // Don't count the current player
		}
		if g.RoundSkippers[uid] {
			skippedCount++
		}
	}
	return skippedCount == (activePlayers - 1)
}

// HandsCopy returns a deep copy of current hands.
func (g *Game) HandsCopy() map[string][]Card {
	out := make(map[string][]Card, len(g.Hands))
	for k, v := range g.Hands {
		hand := make([]Card, len(v))
		copy(hand, v)
		out[k] = hand
	}
	return out
}

func validateIndices(indices []int, handSize int) error {
	seen := make(map[int]bool)
	for _, idx := range indices {
		if idx < 0 || idx >= handSize {
			return fmt.Errorf("invalid card index %d", idx)
		}
		if seen[idx] {
			return fmt.Errorf("duplicate card index %d", idx)
		}
		seen[idx] = true
	}
	return nil
}

func extractByIndices(hand []Card, indices []int) []Card {
	out := make([]Card, 0, len(indices))
	for _, idx := range indices {
		out = append(out, hand[idx])
	}
	return out
}

func removeByIndices(hand []Card, indices []int) []Card {
	mark := make(map[int]bool, len(indices))
	for _, idx := range indices {
		mark[idx] = true
	}
	out := make([]Card, 0, len(hand)-len(indices))
	for i, c := range hand {
		if mark[i] {
			continue
		}
		out = append(out, c)
	}
	return out
}
