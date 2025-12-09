package logic

import (
	"sort"
	"github.com/yourusername/tienlen-server/pb"
)

// Calculate the absolute power of a card for comparison.
// Rank: 0(3) ... 12(2)
// Suit: 0(Spade) ... 3(Heart)
func GetCardPower(c *pb.Card) int32 {
	return c.Rank*4 + c.Suit
}

// SortCards sorts a slice of cards in ascending order of power.
func SortCards(cards []*pb.Card) {
	sort.Slice(cards, func(i, j int) bool {
		return GetCardPower(cards[i]) < GetCardPower(cards[j])
	})
}

// IsValidSet checks if the cards form a valid combination (Single, Pair, Triple, Quad).
// Does not check Sequences/Straights yet for simplicity.
func IsValidSet(cards []*pb.Card) bool {
	if len(cards) == 0 {
		return false
	}
	if len(cards) == 1 {
		return true
	}
	
	// Check for Pair, Triple, Quad (All ranks must be equal)
	firstRank := cards[0].Rank
	for _, c := range cards {
		if c.Rank != firstRank {
			// If ranks are different, it might be a sequence, but we skip that for now.
			return false 
		}
	}
	return true
}

// CanBeat checks if 'newCards' can beat 'prevCards'.
// Assumes both sets are already validated by IsValidSet.
func CanBeat(prevCards []*pb.Card, newCards []*pb.Card) bool {
	if len(prevCards) != len(newCards) {
		return false // Must play same number of cards (ignoring bombs for now)
	}

	// Since we assume IsValidSet (all same rank), we just compare the highest card of each set.
	// But actually, for pairs/triples, we just need to compare the highest card of the set.
	// We need to sort them to find the highest.
	// NOTE: Function signature assumes caller handles sorting or we do it here.
	// Let's not mutate inputs. Find max power.
	
	maxPrev := int32(-1)
	for _, c := range prevCards {
		p := GetCardPower(c)
		if p > maxPrev {
			maxPrev = p
		}
	}

	maxNew := int32(-1)
	for _, c := range newCards {
		p := GetCardPower(c)
		if p > maxNew {
			maxNew = p
		}
	}

	return maxNew > maxPrev
}
