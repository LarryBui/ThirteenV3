package api

import (
	"context"
	"database/sql"
	"encoding/json"

	"github.com/heroiclabs/nakama-common/runtime"
)

// RpcCreateMatch creates a new authoritative match and returns the match ID.
func RpcCreateMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	matchID, err := nk.MatchCreate(ctx, "tienlen_match", nil)
	if err != nil {
		logger.Error("Error creating match: %v", err)
		return "", err
	}

	response := map[string]string{"match_id": matchID}
	bytes, err := json.Marshal(response)
	if err != nil {
		logger.Error("Error marshalling response: %v", err)
		return "", err
	}

	return string(bytes), nil
}

// RpcQuickMatch searches for an available match or creates a new one.
func RpcQuickMatch(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	// Search for an available match
	// MatchList(ctx, limit, authoritative, label, minSize, maxSize, query)
	limit := 1
	authoritative := true
	label := "TienLen"
	minSize := 0
	maxSize := 3 // We want matches with at most 3 players so there is room for one more
	
	matches, err := nk.MatchList(ctx, limit, authoritative, label, &minSize, &maxSize, "")
	if err != nil {
		logger.Error("Error listing matches: %v", err)
		return "", err
	}

	var matchID string
	if len(matches) > 0 {
		// Found an available match
		matchID = matches[0].GetMatchId()
		logger.Info("Found existing match: %s", matchID)
	} else {
		// No available match, create a new one
		matchID, err = nk.MatchCreate(ctx, "tienlen_match", nil)
		if err != nil {
			logger.Error("Error creating new match: %v", err)
			return "", err
		}
		logger.Info("Created new match: %s", matchID)
	}

	response := map[string]string{"match_id": matchID}
	bytes, err := json.Marshal(response)
	if err != nil {
		logger.Error("Error marshalling response: %v", err)
		return "", err
	}

	return string(bytes), nil
}
