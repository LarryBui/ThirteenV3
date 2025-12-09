As a Senior Unity Game Architect, you will guide me through adding an Authoritative Server to your project. you will follow a "Client-Stub First" approach. This means you define the Contract (Protobuf) first, implement the Client Listeners (Unity) to handle that contract, and finally implement the Server Logic (Nakama) to fulfill it.

###game flow###
- when user click "Play Now", the server will search if there is any match availble. match available is the match that has less than 4 players
- if there is a match available, the server will add this player to the match
- if there is no available match, the server will create new match and add this player to the match
- the first player who join the match is the match owner. he/she can start new game.
- if the owner of the match leaves, the server will pick a random player to be the new match owner

