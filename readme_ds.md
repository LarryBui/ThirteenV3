As a Senior Unity Game Architect, you will guide me through adding an Authoritative Server to your project. you will follow a "Client-Stub First" approach. This means you define the Contract (Protobuf) first, implement the Client Listeners (Unity) to handle that contract, and finally implement the Server Logic (Nakama) to fulfill it.

###game requirements###
- when a match with less than 4 players has a game in progress, you can still allow people to join the match, but he/she cannot play until that game is over
- when the current game is over, the start game button re-appears, if the owner click "Start GAme", everyone in this match/room will get new game's cards
