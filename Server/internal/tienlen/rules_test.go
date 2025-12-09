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
