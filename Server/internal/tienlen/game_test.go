package tienlen

import (
	"math/rand"
	"testing"
)

func TestGameStartDealsHandsAndSetsTurn(t *testing.T) {
	rand.Seed(1)
	g := NewGame()
	players := []string{"p1", "p2", "p3", "p4"}
	events, err := g.Start(players, "p1")
	if err != nil {
		t.Fatalf("Start returned error: %v", err)
	}
	if !g.IsPlaying() {
		t.Fatalf("expected game to be playing after start")
	}
	if len(events) != 2 {
		t.Fatalf("expected 2 events (MatchStarted + TurnChanged), got %d", len(events))
	}

	ms, ok := events[0].(MatchStarted)
	if !ok {
		t.Fatalf("first event not MatchStarted")
	}
	for id, hand := range ms.Hands {
		if len(hand) != 13 {
			t.Fatalf("player %s hand size = %d, want 13", id, len(hand))
		}
	}
	if len(ms.TurnOrder) != len(players) {
		t.Fatalf("turn order size = %d, want %d", len(ms.TurnOrder), len(players))
	}

	tc, ok := events[1].(TurnChanged)
	if !ok {
		t.Fatalf("second event not TurnChanged")
	}
	if tc.ActivePlayerID == "" {
		t.Fatalf("active player should be set")
	}
}

func TestPlayPassEndsRound(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2"}
	g.Hands = map[string][]Card{
		"p1": {{Rank: 1, Suit: 0}, {Rank: 3, Suit: 0}},
		"p2": {{Rank: 2, Suit: 0}, {Rank: 4, Suit: 0}},
	}
	g.CurrentIdx = 0
	g.isPlaying = true
	g.RoundSkippers = make(map[string]bool)

	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("PlayCards error: %v", err)
	}
	if len(events) == 0 {
		t.Fatalf("expected events after play")
	}
	if g.LastActor != "p1" {
		t.Fatalf("expected LastActor to be p1")
	}
	if g.CurrentIdx != 1 {
		t.Fatalf("expected turn to advance to p2, got idx %d", g.CurrentIdx)
	}

	events, err = g.Pass("p2")
	if err != nil {
		t.Fatalf("Pass error: %v", err)
	}
	foundRoundEnd := false
	for _, ev := range events {
		if _, ok := ev.(RoundEnded); ok {
			foundRoundEnd = true
		}
	}
	if !foundRoundEnd {
		t.Fatalf("expected RoundEnded event after pass")
	}
	if g.CurrentIdx != 0 {
		t.Fatalf("turn should stay with last actor winner index 0, idx=%d", g.CurrentIdx)
	}
}

func TestGameOverWhenHandEmpty(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1"}
	g.Hands = map[string][]Card{"p1": {{Rank: 1, Suit: 0}}}
	g.CurrentIdx = 0
	g.isPlaying = true

	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("PlayCards error: %v", err)
	}
	gameOver := false
	for _, ev := range events {
		if _, ok := ev.(GameOver); ok {
			gameOver = true
		}
	}
	if !gameOver {
		t.Fatalf("expected GameOver event when hand is empty")
	}
	if g.IsPlaying() {
		t.Fatalf("expected game to stop after GameOver")
	}
}

func TestPassSkipsPlayerAndAdvancesTurn(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2", "p3"}
	g.Hands = map[string][]Card{
		"p1": {{Rank: 3, Suit: 0}},
		"p2": {{Rank: 4, Suit: 0}},
		"p3": {{Rank: 5, Suit: 0}},
	}
	g.isPlaying = true
	g.CurrentIdx = 1   // p2's turn
	g.LastActor = "p1" // p1 owns the board
	g.Board = []Card{{Rank: 3, Suit: 0}}
	g.RoundSkippers = make(map[string]bool)

	events, err := g.Pass("p2")
	if err != nil {
		t.Fatalf("Pass error: %v", err)
	}

	if !g.RoundSkippers["p2"] {
		t.Fatalf("expected p2 to be marked as skipped")
	}
	if g.CurrentIdx != 2 {
		t.Fatalf("expected turn to advance to p3, got idx %d", g.CurrentIdx)
	}

	var turnChanged bool
	for _, ev := range events {
		if tc, ok := ev.(TurnChanged); ok {
			turnChanged = true
			if tc.ActivePlayerID != "p3" {
				t.Fatalf("expected active player p3, got %s", tc.ActivePlayerID)
			}
		}
		if _, ok := ev.(RoundEnded); ok {
			t.Fatalf("did not expect round to end when only one player passed")
		}
	}
	if !turnChanged {
		t.Fatalf("expected TurnChanged event after pass")
	}
}

func TestPassResetsRoundWhenReturningToLastActor(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2"}
	g.Hands = map[string][]Card{
		"p1": {{Rank: 3, Suit: 0}},
		"p2": {{Rank: 4, Suit: 0}},
	}
	g.isPlaying = true
	g.CurrentIdx = 1   // p2's turn
	g.LastActor = "p1" // p1 owns the board
	g.Board = []Card{{Rank: 3, Suit: 0}}
	g.RoundSkippers = make(map[string]bool)

	events, err := g.Pass("p2")
	if err != nil {
		t.Fatalf("Pass error: %v", err)
	}

	if g.LastActor != "" {
		t.Fatalf("expected LastActor reset, got %s", g.LastActor)
	}
	if len(g.Board) != 0 {
		t.Fatalf("expected board cleared, got %d cards", len(g.Board))
	}
	if len(g.RoundSkippers) != 0 {
		t.Fatalf("expected round skippers reset, got %d entries", len(g.RoundSkippers))
	}
	if g.CurrentIdx != 0 {
		t.Fatalf("expected turn to return to p1, idx=%d", g.CurrentIdx)
	}

	var roundEnded, turnChanged bool
	for _, ev := range events {
		switch e := ev.(type) {
		case RoundEnded:
			roundEnded = true
			if e.WinnerID != "p1" {
				t.Fatalf("expected p1 to win round, got %s", e.WinnerID)
			}
		case TurnChanged:
			turnChanged = true
			if e.ActivePlayerID != "p1" {
				t.Fatalf("expected next active p1, got %s", e.ActivePlayerID)
			}
		}
	}
	if !roundEnded {
		t.Fatalf("expected RoundEnded event when turn loops to last actor")
	}
	if !turnChanged {
		t.Fatalf("expected TurnChanged event after round end")
	}
}

func TestLastActorStartsNewRoundWithAnyCard(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2"}
	g.Hands = map[string][]Card{
		"p1": {{Rank: 5, Suit: 0}, {Rank: 3, Suit: 1}, {Rank: 2, Suit: 1}}, // high then low
		"p2": {{Rank: 6, Suit: 0}},
	}
	g.isPlaying = true
	g.CurrentIdx = 0

	// p1 plays the higher card first
	if _, err := g.PlayCards("p1", []int{0}); err != nil {
		t.Fatalf("PlayCards p1 initial error: %v", err)
	}
	if g.LastActor != "p1" {
		t.Fatalf("expected LastActor p1 after play")
	}
	if g.CurrentIdx != 1 {
		t.Fatalf("expected turn to advance to p2, idx=%d", g.CurrentIdx)
	}

	// p2 passes, causing round end and reset
	if _, err := g.Pass("p2"); err != nil {
		t.Fatalf("Pass error: %v", err)
	}
	if g.LastActor != "" {
		t.Fatalf("expected LastActor cleared after round end")
	}
	if len(g.Board) != 0 {
		t.Fatalf("expected board cleared after round end")
	}
	if g.CurrentIdx != 0 {
		t.Fatalf("expected turn back to p1, idx=%d", g.CurrentIdx)
	}
	if got := len(g.Hands["p1"]); got != 2 {
		t.Fatalf("expected p1 to still have 2 cards before starting new round, got %d", got)
	}
	if len(g.Board) != 0 {
		t.Fatalf("expected board cards to be empty before new round starts")
	}

	// p1 should now be able to play any card (even lower than previous board)
	if _, err := g.PlayCards("p1", []int{0}); err != nil {
		t.Fatalf("PlayCards after round reset should succeed, got error: %v", err)
	}
}
