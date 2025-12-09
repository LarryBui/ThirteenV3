Act as a sr Unity software architect, read the following instructions and generate code accordingly using best practice and clean architecture

###

This is a crucial architectural pivot. Placing all logic inside a single match.go file is the most common mistake developers make with Nakama. It leads to a "God Object" that is impossible to unit test and painful to maintain.

To fix this, we will apply Clean Architecture principles. We will separate the Network Layer (Nakama) from the Domain Layer (Tien Len Game Rules).

The Core Concept: "Humble Object" Pattern
The Handler (Nakama): Acts as the "HTTP Controller" or "Port". It knows about OpCodes, Dispatchers, and JSON/Protobuf. It is "dumb."

The Engine (Domain): Acts as the "Brain." It knows about Cards, Hands, and Rules. It knows nothing about Nakama.

1. Recommended Folder Structure
Move away from a flat structure. Adopt this standard Go layout:

/Server ├── go.mod ├── main.go # Entry point (Registers the Match Handler) ├── api/ # Generated Protobuf files (game.pb.go) └── internal/ ├── match/ # LAYER 1: Nakama Integration │ ├── handler.go # Implements MatchInit, MatchLoop (The "Controller") │ └── adapter.go # Converts Domain Events -> Protobuf Messages │ └── tienlen/ # LAYER 2: Pure Game Domain (No Nakama imports!) ├── game.go # The primary 'Game' struct and State Machine ├── rules.go # Pure functions: IsValidMove(), DetectChop() ├── player.go # Player logic (Hand management) ├── deck.go # Sorting and Card logic └── events.go # Definition of events (e.g., RoundEnded, TurnChanged)