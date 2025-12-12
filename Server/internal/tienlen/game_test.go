package tienlen

import (
	"math/rand"
	"testing"
)

func TestGameStartDealsHandsAndSetsTurn(t *testing.T) {
	rand.Seed(1)
	g := NewGame()
	players := []string{"p1", "p2", "p3", "p4"}
	events, err := g.Start(players, "p1", "")
	if err != nil {
		t.Fatalf("Start returned error: %v", err)
	}
	if !g.IsPlaying() {
		t.Fatalf("expected game to be playing after start")
	}
	if len(events) != 2 {
		t.Fatalf("expected 2 events (GameStarted + TurnChanged), got %d", len(events))
	}

	ms, ok := events[0].(GameStarted)
	if !ok {
		t.Fatalf("first event not GameStarted")
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

func setupDeterministicGame(players []string, owner string, hands map[string][]Card) *Game {
	g := NewGame()
	g.TurnOrder = make([]string, len(players))
	copy(g.TurnOrder, players) // Set explicit turn order
	g.OwnerID = owner
	g.Hands = make(map[string][]Card, len(hands))
	for k, v := range hands {
		handCopy := make([]Card, len(v))
		copy(handCopy, v)
		SortHand(handCopy) // Ensure hands are sorted
		g.Hands[k] = handCopy
	}
	g.CurrentIdx = 0 // Start with the first player in turnOrder
	g.isPlaying = true
	g.RoundSkippers = make(map[string]bool)
	return g
}

func TestGameOverWhenThirdPlayerHandEmpty(t *testing.T) {
	players := []string{"p1", "p2", "p3", "p4"}
	hands := map[string][]Card{
		"p1": {{Rank: 0, Suit: 0}},                     // One card for p1
		"p2": {{Rank: 1, Suit: 0}},                     // One card for p2
		"p3": {{Rank: 2, Suit: 0}},                     // One card for p3
		"p4": {{Rank: 3, Suit: 0}, {Rank: 4, Suit: 0}}, // Two cards for p4 (the eventual loser)
	}
	g := setupDeterministicGame(players, "p1", hands)

	// Ensure p1 is the current turn player
	if g.TurnOrder[g.CurrentIdx] != "p1" {
		t.Fatalf("expected turn to be p1, got %s", g.TurnOrder[g.CurrentIdx])
	}

	var events []Event
	var gameOverEvent *GameOver
	var playerFinishedEvents []PlayerFinished

	// --- Player 1 plays last card (1st winner) ---
	g.Board = nil // Clear board to allow any card
	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("p1 PlayCards error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if gameOverEvent != nil {
		t.Fatal("expected no GameOver after 1st player finishes")
	}
	if len(playerFinishedEvents) != 1 || playerFinishedEvents[0].PlayerID != "p1" || playerFinishedEvents[0].Rank != 1 {
		t.Fatalf("expected p1 as 1st finisher, got %+v", playerFinishedEvents)
	}
	if !g.IsPlaying() { // Check if game continues
		t.Fatal("expected game to continue after 1st player finishes")
	}
	if len(g.Winners) != 1 || g.Winners[0] != "p1" {
		t.Fatalf("expected p1 in Winners, got %v", g.Winners)
	}
	if !g.FinishedPlayers["p1"] {
		t.Fatalf("expected p1 to be in FinishedPlayers")
	}
	playerFinishedEvents = nil // Reset for next iteration

	// --- Player 2 plays last card (2nd winner) ---
	// g.CurrentIdx should have advanced to p2 after p1's turn and p1 was skipped
	if g.TurnOrder[g.CurrentIdx] != "p2" {
		t.Fatalf("expected turn to be p2, got %s", g.TurnOrder[g.CurrentIdx])
	}
	events, err = g.PlayCards("p2", []int{0})
	if err != nil {
		t.Fatalf("p2 PlayCards error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if gameOverEvent != nil {
		t.Fatal("expected no GameOver after 2nd player finishes")
	}
	if len(playerFinishedEvents) != 1 || playerFinishedEvents[0].PlayerID != "p2" || playerFinishedEvents[0].Rank != 2 {
		t.Fatalf("expected p2 as 2nd finisher, got %+v", playerFinishedEvents)
	}
	if !g.IsPlaying() { // Check if game continues
		t.Fatal("expected game to continue after 2nd player finishes")
	}
	if len(g.Winners) != 2 || g.Winners[1] != "p2" {
		t.Fatalf("expected p2 in Winners, got %v", g.Winners)
	}
	if !g.FinishedPlayers["p2"] {
		t.Fatalf("expected p2 to be in FinishedPlayers")
	}
	playerFinishedEvents = nil

	// --- Player 3 plays last card (3rd winner - game over) ---
	// g.CurrentIdx should have advanced to p3
	if g.TurnOrder[g.CurrentIdx] != "p3" {
		t.Fatalf("expected turn to be p3, got %s", g.TurnOrder[g.CurrentIdx])
	}
	events, err = g.PlayCards("p3", []int{0})
	if err != nil {
		t.Fatalf("p3 PlayCards error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if gameOverEvent == nil || gameOverEvent.WinnerID != "p1" { // WinnerID is 1st place
		t.Fatalf("expected GameOver with winner p1, got %+v", gameOverEvent)
	}
	if len(playerFinishedEvents) != 1 || playerFinishedEvents[0].PlayerID != "p3" || playerFinishedEvents[0].Rank != 3 {
		t.Fatalf("expected p3 as 3rd finisher, got %+v", playerFinishedEvents)
	}
	if g.IsPlaying() { // Check if game has stopped
		t.Fatal("expected game to stop after 3rd player finishes")
	}
	if len(g.Winners) != 3 || g.Winners[2] != "p3" {
		t.Fatalf("expected p3 in Winners, got %v", g.Winners)
	}
	if !g.FinishedPlayers["p3"] {
		t.Fatalf("expected p3 to be in FinishedPlayers")
	}
	// PlayerFinishedEvents for p3 is now processed, no need to reset, test is ending.

	// Verify p4 is the loser (last remaining active player)
	if g.FinishedPlayers["p4"] {
		t.Fatalf("expected p4 NOT to be in FinishedPlayers")
	}
	if len(g.Hands["p4"]) != 2 {
		t.Fatalf("expected p4 to still have 2 cards, got %d", len(g.Hands["p4"]))
	}
}

func TestGameEndsWhenOnePlayerRemains_2Players(t *testing.T) {
	players := []string{"p1", "p2"}
	hands := map[string][]Card{
		"p1": {{Rank: 0, Suit: 0}},
		"p2": {{Rank: 1, Suit: 0}},
	}
	g := setupDeterministicGame(players, "p1", hands)

	var events []Event
	var gameOverEvent *GameOver
	var playerFinishedEvents []PlayerFinished

	// p1 plays last card -> Game Should End (1 winner, 1 loser)
	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("p1 PlayCards error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if len(playerFinishedEvents) != 1 || playerFinishedEvents[0].PlayerID != "p1" {
		t.Fatalf("expected p1 to finish")
	}
	if gameOverEvent == nil {
		t.Fatal("expected GameOver event after 1st player finished in 2-player game")
	}
	if g.IsPlaying() {
		t.Fatal("expected game to stop")
	}
	if len(g.Winners) != 1 {
		t.Fatalf("expected 1 winner, got %d", len(g.Winners))
	}
}

func TestGameEndsWhenOnePlayerRemains_3Players(t *testing.T) {
	players := []string{"p1", "p2", "p3"}
	hands := map[string][]Card{
		"p1": {{Rank: 0, Suit: 0}},
		"p2": {{Rank: 1, Suit: 0}},
		"p3": {{Rank: 2, Suit: 0}},
	}
	g := setupDeterministicGame(players, "p1", hands)

	var events []Event
	var gameOverEvent *GameOver
	var playerFinishedEvents []PlayerFinished

	// p1 finishes
	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("p1 error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)
	if gameOverEvent != nil {
		t.Fatal("expected game to continue after p1 finishes")
	}
	if !g.IsPlaying() {
		t.Fatal("expected game to continue")
	}
	playerFinishedEvents = nil

	// p2 finishes -> Game Should End (2 winners, 1 loser)
	// p1 is finished, turn should move to p2
	if g.TurnOrder[g.CurrentIdx] != "p2" {
		t.Fatalf("expected turn to satisfy p2, got %s", g.TurnOrder[g.CurrentIdx])
	}
	events, err = g.PlayCards("p2", []int{0})
	if err != nil {
		t.Fatalf("p2 error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if gameOverEvent == nil {
		t.Fatal("expected GameOver after 2nd player finished in 3-player game")
	}
	if g.IsPlaying() {
		t.Fatal("expected game to stop")
	}
	if len(g.Winners) != 2 {
		t.Fatalf("expected 2 winners, got %d", len(g.Winners))
	}
}

func TestGameEndsWhenOnePlayerRemains_1Player(t *testing.T) {
	players := []string{"p1"}
	hands := map[string][]Card{
		"p1": {{Rank: 0, Suit: 0}},
	}
	g := setupDeterministicGame(players, "p1", hands)

	var events []Event
	var gameOverEvent *GameOver
	var playerFinishedEvents []PlayerFinished

	// p1 plays last card -> Game Should End immediately
	events, err := g.PlayCards("p1", []int{0})
	if err != nil {
		t.Fatalf("p1 PlayCards error: %v", err)
	}
	processEvents(events, &gameOverEvent, &playerFinishedEvents)

	if len(playerFinishedEvents) != 1 || playerFinishedEvents[0].PlayerID != "p1" {
		t.Fatalf("expected p1 to finish")
	}
	if gameOverEvent == nil {
		t.Fatal("expected GameOver event after 1st player finished in 1-player game")
	}
	if g.IsPlaying() {
		t.Fatal("expected game to stop")
	}
	if len(g.Winners) != 1 {
		t.Fatalf("expected 1 winner, got %d", len(g.Winners))
	}
}

// Helper to extract events for testing
func processEvents(events []Event, gameOver **GameOver, playerFinished *[]PlayerFinished) {
	// Clear previous player finished events before processing new ones
	*playerFinished = (*playerFinished)[:0]
	for _, ev := range events {
		if goEv, ok := ev.(GameOver); ok {
			*gameOver = &goEv
		}
		if pfEv, ok := ev.(PlayerFinished); ok {
			*playerFinished = append(*playerFinished, pfEv)
		}
	}
}

// Helper to find player index in TurnOrder
func findPlayerIndex(turnOrder []string, playerID string) int {
	for i, id := range turnOrder {
		if id == playerID {
			return i
		}
	}
	return -1
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
