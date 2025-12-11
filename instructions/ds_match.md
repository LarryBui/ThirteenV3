* since we already have localAvatarView in gamepresenter, instead of using an array of avatarview, add 3 avatarview for Opponent1, opponent2, opponent3. please note there are always a maxium of 4 players in a match
* always use seats parameter as the source of players' ID in the current room
* the order of userId in list<> seats is important. seats[0] is the userID that is assigned to seat 1
* first find the seat number for localAvatarview
* opponent1 avatarview will be (localavatar seat + 1 % 4)
* opponent2 avatarview will be (localavatar seat + 2 % 4)
* opponent3 avatarview will be (localavatar seat + 3 % 4)