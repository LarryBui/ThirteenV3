
As a Senior Unity Architect, I will guide you through implementing the Round Logic for Tien Len (Thirteen).

###feature explanation###
In Tien Len, the round logic follows a "King of the Hill" structure. It is not just about skipping a turn; passing locks you out of the current round entirely. The round ends when the turn returns to the player who played the highest card (because everyone else has passed/locked out).

Here is the best-practice architecture to implement this on your Authoritative Server (Go) and visualize it in Unity.

1. Architectural Concept: "The Last Actor Rule"
Do not rely on counting "3 passes" explicitly, as this gets messy with disconnects or variable player counts. Instead, use the Turn Loop Check:

State: The server tracks LastActor (the player who placed the current cards on the table).

Lockout: If a player passes, they are flagged as Skipped and cannot play again until the table is cleared.

Win Condition: When calculating the next turn, if the next valid player IS the LastActor, it means everyone else has passed. The LastActor wins the round.

2. Server-Side Implementation (Go)
We need to modify the MatchState to track lockouts and handle the "Pass" OpCode.

Step A: Update state.go
Add fields to track the round status.

Go

type MatchState struct {
    Presences map[string]*Player
    TurnIndex int
    
    // Round Specifics
    TableCards  int  // IDs of cards currently on table
    LastActor    string // UserID of the person who played TableCards
    RoundSkippers map[string]bool // Set of UserIDs who passed this round
}
Step B: The Turn Rotation Logic (match.go)
This helper function finds the next player. If it circles back to the LastActor, it triggers the round end.

Go

func (s *MatchState) RotateTurn(logger runtime.Logger, dispatcher runtime.MatchDispatcher) {
    originalIndex := s.TurnIndex
    players := s.GetSortedPlayerIDs() // Helper to get IDs in seat order 
    count := len(players)

    // 1. Loop to find the next active player
    for i := 1; i <= count; i++ {
        nextIndex := (originalIndex + i) % count
        nextPlayerID := players[nextIndex]

        // CHECK: Is this the person who played the last cards?
        // If yes, everyone else has passed/skipped. They win the round.
        if nextPlayerID == s.LastActor {
            s.EndRound(logger, dispatcher, nextPlayerID)
            return
        }

        // CHECK: Is this player locked out?
        if s.RoundSkippers {
            continue // Skip them, they passed earlier
        }

        // Found a valid player
        s.TurnIndex = nextIndex
        s.BroadcastTurnUpdate(dispatcher)
        return
    }
}

func (s *MatchState) EndRound(logger runtime.Logger, dispatcher runtime.MatchDispatcher, winnerID string) {
    // 1. Clear State
    s.TableCards =int{}
    s.RoundSkippers = make(map[string]bool) // Unlock everyone
    s.LastActor = "" // No one owns the table (New Round)
    
    // 2. Set Turn to Winner
    s.TurnIndex = s.GetPlayerIndex(winnerID)

    // 3. Broadcast to Unity
    // OpCode 10 = OP_ROUND_END
    dispatcher.BroadcastMessage(10, nil, nil, nil, true)
}
Step C: Handling the "Pass" Message
When a client sends a "Pass" request:

Go

// Inside MatchLoop switch
case OpCode_PASS:
    // 1. Mark them as skipped for this round
    state.RoundSkippers = true
    
    // 2. Rotate to next person
    state.RotateTurn(logger, dispatcher)

    3. Unity Client Implementation
On the client side, we need to handle the OP_ROUND_END event to create a satisfying "Clear Table" animation.

Step A: The Visual Controller (TableManager.cs)
We use DOTween to sweep cards off the table.
