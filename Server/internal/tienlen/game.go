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
}

func NewGame() *Game {
	return &Game{
		Hands:         make(map[string][]Card),
		RoundSkippers: make(map[string]bool),
		CurrentIdx:    0,
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
	return Snapshot{
		IsPlaying:      g.isPlaying,
		OwnerID:        g.OwnerID,
		Board:          append([]Card(nil), g.Board...),
		ActivePlayerID: activeID,
		PlayerIDs:      playerIDs,
	}
}

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

	if len(remaining) == 0 {
		g.isPlaying = false
		events = append(events, GameOver{WinnerID: playerID})
		return events, nil
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

	g.RoundSkippers[playerID] = true
	return g.advanceTurn(), nil
}

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

		// Round ends if we loop back to the last actor and someone passed.
		if g.LastActor != "" && nextID == g.LastActor && len(g.RoundSkippers) > 0 {
			g.Board = nil
			g.RoundSkippers = make(map[string]bool)
			g.LastActor = ""
			g.CurrentIdx = nextIdx
			events = append(events,
				RoundEnded{WinnerID: nextID},
				TurnChanged{ActivePlayerID: nextID, Board: g.Board},
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
