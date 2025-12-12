package tienlen

import "testing"

func TestIsValidSet(t *testing.T) {
	tests := []struct {
		name  string
		cards []Card
		want  bool
	}{
		{"single", []Card{{Rank: 0, Suit: 0}}, true},
		{"pair", []Card{{Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}}, true},
		{"triple", []Card{{Rank: 5, Suit: 0}, {Rank: 5, Suit: 1}, {Rank: 5, Suit: 2}}, true},
		{"quad", []Card{{Rank: 9, Suit: 0}, {Rank: 9, Suit: 1}, {Rank: 9, Suit: 2}, {Rank: 9, Suit: 3}}, true},
		{"straight_len3", []Card{{Rank: 0, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 2}}, true},                                         // 3-4-5
		{"straight_len5", []Card{{Rank: 5, Suit: 0}, {Rank: 6, Suit: 1}, {Rank: 7, Suit: 2}, {Rank: 8, Suit: 3}, {Rank: 9, Suit: 0}}, true}, // 8-9-10-J-Q
		{"straight_with_duplicate", []Card{{Rank: 0, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 1, Suit: 2}}, false},
		{"straight_with_two", []Card{{Rank: 9, Suit: 0}, {Rank: 10, Suit: 1}, {Rank: 11, Suit: 2}, {Rank: 12, Suit: 3}}, false},                                     // includes 2
		{"consecutive_pairs", []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 2}, {Rank: 2, Suit: 3}}, true}, // 33 44 55
		{"consecutive_pairs_with_two", []Card{{Rank: 10, Suit: 0}, {Rank: 10, Suit: 1}, {Rank: 11, Suit: 0}, {Rank: 11, Suit: 1}, {Rank: 12, Suit: 2}, {Rank: 12, Suit: 3}}, false},
		{"consecutive_pairs_misaligned", []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 2}, {Rank: 3, Suit: 3}}, false},
		{"mismatched", []Card{{Rank: 1, Suit: 0}, {Rank: 2, Suit: 0}}, false},
		{"empty", nil, false},
	}

	for _, tt := range tests {
		if got := IsValidSet(tt.cards); got != tt.want {
			t.Fatalf("%s: IsValidSet() = %v, want %v", tt.name, got, tt.want)
		}
	}
}

func TestCanBeat(t *testing.T) {
	// --- Standard Tests ---
	prev := []Card{{Rank: 3, Suit: 0}}
	higher := []Card{{Rank: 4, Suit: 0}}
	lower := []Card{{Rank: 2, Suit: 3}}

	if !CanBeat(prev, higher) {
		t.Fatalf("expected higher card to beat previous")
	}
	if CanBeat(prev, lower) {
		t.Fatalf("expected lower card not to beat previous")
	}

	prevPair := []Card{{Rank: 6, Suit: 0}, {Rank: 6, Suit: 1}}
	higherPair := []Card{{Rank: 7, Suit: 2}, {Rank: 7, Suit: 3}}
	mismatch := []Card{{Rank: 9, Suit: 1}}

	if !CanBeat(prevPair, higherPair) {
		t.Fatalf("expected higher pair to beat previous pair")
	}
	if CanBeat(prevPair, mismatch) {
		t.Fatalf("expected mismatched length to fail CanBeat")
	}

	// --- Pig Chopping Tests ---
	
	// Cards Setup
	single2 := []Card{{Rank: 12, Suit: 0}}
	pair2 := []Card{{Rank: 12, Suit: 0}, {Rank: 12, Suit: 1}}
	single3 := []Card{{Rank: 0, Suit: 0}}
	
	// Bombs Setup
	// 3-Pine: 33 44 55
	pine3Low := []Card{{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}}
	pine3High := []Card{{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}} 
	
	// Quad: 4x 6s (Low), 4x 7s (High)
	quadLow := []Card{{Rank: 6, Suit: 0}, {Rank: 6, Suit: 1}, {Rank: 6, Suit: 2}, {Rank: 6, Suit: 3}}
	quadHigh := []Card{{Rank: 7, Suit: 0}, {Rank: 7, Suit: 1}, {Rank: 7, Suit: 2}, {Rank: 7, Suit: 3}}

	// 4-Pine: 33 44 55 66 (Low), 44 55 66 77 (High)
	pine4Low := []Card{
		{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, 
		{Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}}
	pine4High := []Card{
		{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, 
		{Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, {Rank: 4, Suit: 0}, {Rank: 4, Suit: 1}}
	
	// 5-Pine: 33 44 55 66 77 (Low), 44 55 66 77 88 (High)
	pine5Low := []Card{
		{Rank: 0, Suit: 0}, {Rank: 0, Suit: 1}, {Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, 
		{Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, {Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, 
		{Rank: 4, Suit: 0}, {Rank: 4, Suit: 1}}
	pine5High := []Card{
		{Rank: 1, Suit: 0}, {Rank: 1, Suit: 1}, {Rank: 2, Suit: 0}, {Rank: 2, Suit: 1}, 
		{Rank: 3, Suit: 0}, {Rank: 3, Suit: 1}, {Rank: 4, Suit: 0}, {Rank: 4, Suit: 1},
		{Rank: 5, Suit: 0}, {Rank: 5, Suit: 1}}

	tests := []struct {
		name string
		prev []Card
		next []Card
		want bool
	}{
		// 1. 3-Pine Tests
		{"3-Pine vs Single 2", single2, pine3Low, true},
		{"3-Pine vs Smaller 3-Pine", pine3Low, pine3High, true},
		{"3-Pine vs Pair 2 (Fail)", pair2, pine3Low, false},
		{"3-Pine vs Quad (Fail - Quad wins)", pine3Low, quadLow, true}, // Wait, can Quad beat 3-Pine? Yes. Can 3-Pine beat Quad? No. 
		// "CanBeat(prev, next)" means "Does next beat prev?"
		// So "pine3Low vs quadLow" -> "Does quadLow beat pine3Low?" -> Yes.
		// "quadLow vs pine3Low" -> "Does pine3Low beat quadLow?" -> No.
		{"3-Pine beats Quad (Fail)", quadLow, pine3Low, false},

		// 2. Quad Tests
		{"Quad vs Single 2", single2, quadLow, true},
		{"Quad vs Pair 2", pair2, quadLow, true},
		{"Quad vs 3-Pine", pine3Low, quadLow, true},
		{"Quad vs Smaller Quad", quadLow, quadHigh, true},
		{"Quad vs 4-Pine (Fail)", pine4Low, quadLow, false}, // Quad does NOT beat 4-Pine
		{"4-Pine beats Quad", quadLow, pine4Low, true},      // 4-Pine DOES beat Quad

		// 3. 4-Pine Tests
		{"4-Pine vs Single 2", single2, pine4Low, true},
		{"4-Pine vs Pair 2", pair2, pine4Low, true},
		{"4-Pine vs Quad", quadLow, pine4Low, true},
		{"4-Pine vs 3-Pine", pine3Low, pine4Low, true},
		{"4-Pine vs Smaller 4-Pine", pine4Low, pine4High, true},
		{"4-Pine vs 5-Pine (Fail)", pine5Low, pine4Low, false}, // 4-Pine does NOT beat 5-Pine
		{"5-Pine beats 4-Pine", pine4Low, pine5Low, true},

		// 4. 5-Pine Tests
		{"5-Pine vs Single 2", single2, pine5Low, true},
		{"5-Pine vs Pair 2", pair2, pine5Low, true},
		{"5-Pine vs Quad", quadLow, pine5Low, true},
		{"5-Pine vs 4-Pine", pine4Low, pine5Low, true},
		{"5-Pine vs Smaller 5-Pine", pine5Low, pine5High, true},

		// 5. Mismatches / Negative Tests
		{"Quad vs Single 3 (Fail)", single3, quadLow, false},
		{"3-Pine vs Single 3 (Fail)", single3, pine3Low, false},
	}

	for _, tt := range tests {
		if got := CanBeat(tt.prev, tt.next); got != tt.want {
			t.Errorf("%s: CanBeat() = %v, want %v", tt.name, got, tt.want)
		}
	}
}