package main

import (
	"context"
	"database/sql"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
	"github.com/yourusername/tienlen-server/internal/match"
)

// InitModule is the entry point for the Nakama Go Runtime.
func InitModule(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, initializer runtime.Initializer) error {
	startTime := time.Now()
	logger.Info("TienLen Game Server initializing...")

	// Register Match Handlers here
	if err := initializer.RegisterMatch("tienlen_match", func(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule) (runtime.Match, error) {
		return &match.Match{}, nil
	}); err != nil {
		return err
	}

	logger.Info("TienLen Game Server initialized in %dms", time.Since(startTime).Milliseconds())
	return nil
}

// main is a dummy function to allow 'go build' to pass without flags. 
// Nakama plugins are built as shared objects, but having main() helps with tooling.
func main() {}