package tienlen

// IsValidSet checks if the cards form a single, pair, triple, or quad.
func IsValidSet(cards []Card) bool {
	if len(cards) == 0 {
		return false
	}
	if len(cards) == 1 {
		return true
	}
	firstRank := cards[0].Rank
	for _, c := range cards {
		if c.Rank != firstRank {
			return false
		}
	}
	return true
}

// CanBeat determines if newCards can beat prevCards (same length).
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
