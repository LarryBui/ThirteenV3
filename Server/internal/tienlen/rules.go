package tienlen

import "sort"

// IsValidSet checks if the cards form a legal Tien Len combination:
// single, pair, triple, quad, straight (>=3, no 2s), or consecutive pairs (doi thong, >=3 pairs, no 2s).
func IsValidSet(cards []Card) bool {
	if len(cards) == 0 {
		return false
	}
	if len(cards) == 1 {
		return true
	}

	// Same-rank sets: pair, triple, quad (bomb)
	if allSameRank(cards) {
		return len(cards) <= 4
	}

	// Straights (sanh): length >= 3, ranks consecutive, cannot contain 2 (rank 12), no duplicates.
	if isStraight(cards) {
		return true
	}

	// Consecutive pairs (doi thong): even length >= 6, pairs of same rank, ranks consecutive, cannot contain 2.
	if isConsecutivePairs(cards) {
		return true
	}

	return false
}

// CanBeat determines if newCards can beat prevCards (same length).
// NOTE: This is still a simplified comparison (highest card wins). Extend as needed for bombs/2s.
func CanBeat(prevCards, newCards []Card) bool {
	if len(prevCards) != len(newCards) {
		return false
	}

	maxPrev := int32(-1)
	for _, c := range prevCards {
		p := cardPower(c)
		if p > maxPrev {
			maxPrev = p
		}
	}

	maxNew := int32(-1)
	for _, c := range newCards {
		p := cardPower(c)
		if p > maxNew {
			maxNew = p
		}
	}

	return maxNew > maxPrev
}

func allSameRank(cards []Card) bool {
	if len(cards) == 0 {
		return false
	}
	r := cards[0].Rank
	for _, c := range cards {
		if c.Rank != r {
			return false
		}
	}
	return true
}

func isStraight(cards []Card) bool {
	if len(cards) < 3 {
		return false
	}
	ranks := make([]int32, len(cards))
	for i, c := range cards {
		if c.Rank == 12 { // 2 cannot be in a straight
			return false
		}
		ranks[i] = c.Rank
	}
	sort.Slice(ranks, func(i, j int) bool { return ranks[i] < ranks[j] })

	for i := 1; i < len(ranks); i++ {
		if ranks[i] == ranks[i-1] {
			return false // duplicate rank not allowed
		}
		if ranks[i] != ranks[i-1]+1 {
			return false // not consecutive
		}
	}
	return true
}

func isConsecutivePairs(cards []Card) bool {
	if len(cards) < 6 || len(cards)%2 != 0 {
		return false
	}
	ranks := make([]int32, len(cards))
	for i, c := range cards {
		if c.Rank == 12 { // 2 cannot be part of consecutive pairs
			return false
		}
		ranks[i] = c.Rank
	}
	sort.Slice(ranks, func(i, j int) bool { return ranks[i] < ranks[j] })

	// Check that cards are grouped in pairs of the same rank.
	pairRanks := make([]int32, 0, len(ranks)/2)
	for i := 0; i < len(ranks); i += 2 {
		if ranks[i] != ranks[i+1] {
			return false
		}
		pairRanks = append(pairRanks, ranks[i])
	}

	// Check consecutive ranks across pairs.
	for i := 1; i < len(pairRanks); i++ {
		if pairRanks[i] != pairRanks[i-1]+1 {
			return false
		}
	}
	return true
}
