package tienlen

import "testing"

func TestAllOthersSkipped(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2", "p3", "p4"}
	g.FinishedPlayers = make(map[string]bool)
	g.RoundSkippers = make(map[string]bool)

	// Case 1: No one skipped, current player is p1
	// Expected: False
	if g.allOthersSkipped("p1") {
		t.Fatalf("Case 1: expected allOthersSkipped to be false when no one skipped")
	}

	// Case 2: p2 and p3 skipped, p4 active. Current is p1.
	// Expected: False (p4 is still active)
	g.RoundSkippers["p2"] = true
	g.RoundSkippers["p3"] = true
	if g.allOthersSkipped("p1") {
		t.Fatalf("Case 2: expected allOthersSkipped to be false when p4 is still active")
	}

	// Case 3: p2, p3, p4 skipped. Current is p1.
	// Expected: True
	g.RoundSkippers["p4"] = true
	if !g.allOthersSkipped("p1") {
		t.Fatalf("Case 3: expected allOthersSkipped to be true when all others skipped")
	}

	// Case 4: p2 finished, p3 skipped, p4 skipped. Current is p1.
	// Expected: True (finished players are ignored)
	g.RoundSkippers = make(map[string]bool)
	g.FinishedPlayers["p2"] = true
	g.RoundSkippers["p3"] = true
	g.RoundSkippers["p4"] = true
	if !g.allOthersSkipped("p1") {
		t.Fatalf("Case 4: expected allOthersSkipped to be true when active others skipped (ignoring finished)")
	}

	// Case 5: p2 finished, p3 active, p4 skipped. Current is p1.
	// Expected: False
	g.RoundSkippers["p3"] = false
	if g.allOthersSkipped("p1") {
		t.Fatalf("Case 5: expected allOthersSkipped to be false when p3 is active")
	}
}

func TestAdvanceTurnSkipsFinishedPlayers(t *testing.T) {
	g := NewGame()
	g.TurnOrder = []string{"p1", "p2", "p3", "p4"}
	g.CurrentIdx = 0 // p1's turn
	g.FinishedPlayers = make(map[string]bool)
	g.RoundSkippers = make(map[string]bool)
	
	// Mark p2 as finished
	g.FinishedPlayers["p2"] = true

	// p1 plays/passes. Turn should advance to p3 (skipping p2)
	events := g.advanceTurn()
	
	if g.TurnOrder[g.CurrentIdx] != "p3" {
		t.Fatalf("expected turn to advance to p3, got %s", g.TurnOrder[g.CurrentIdx])
	}
	
	// Check for TurnChanged event
	foundTurnChanged := false
	for _, ev := range events {
		if tc, ok := ev.(TurnChanged); ok {
			foundTurnChanged = true
			if tc.ActivePlayerID != "p3" {
				t.Fatalf("expected TurnChanged event with ActivePlayerID p3, got %s", tc.ActivePlayerID)
			}
		}
	}
	if !foundTurnChanged {
		t.Fatalf("expected TurnChanged event")
	}

	// Mark p3 as finished too.
	g.FinishedPlayers["p3"] = true
	
	// p3 (was current) -> advance -> should skip p4 if p4 was next? No, p4 is next.
	// Turn is currently at p3 (index 2).
	// advanceTurn calculates next from current.
	// We need to simulate turn passing from p3 (conceptually, though p3 is finished, logic uses CurrentIdx)
	// Actually, advanceTurn is called *after* a player action. 
	// If p3 was active and just finished, advanceTurn is called.
	// Next is p4.
	
	events = g.advanceTurn()
	if g.TurnOrder[g.CurrentIdx] != "p4" {
		t.Fatalf("expected turn to advance to p4, got %s", g.TurnOrder[g.CurrentIdx])
	}

	// Mark p4 as skipped. p1 is active.
	// p4 finishes turn (advanceTurn called).
	// Next is p1.
	events = g.advanceTurn()
	if g.TurnOrder[g.CurrentIdx] != "p1" {
		t.Fatalf("expected turn to advance to p1, got %s", g.TurnOrder[g.CurrentIdx])
	}
}
