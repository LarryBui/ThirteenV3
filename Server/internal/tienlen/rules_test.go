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
}
