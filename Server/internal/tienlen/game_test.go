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
