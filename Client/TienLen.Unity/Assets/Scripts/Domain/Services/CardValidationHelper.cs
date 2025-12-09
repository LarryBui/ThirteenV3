using System;
using System.Collections.Generic;
using System.Linq;
using TienLen.Unity.Domain.ValueObjects;

namespace TienLen.Unity.Domain.Services
{
    /// <summary>
    /// Helper service for validating card moves and determining playability.
    /// Mirrors the server-side rules logic for consistency.
    /// </summary>
    public static class CardValidationHelper
    {
        /// <summary>
        /// Calculates the power rating of a single card.
        /// Power = Rank * 4 + Suit
        /// This matches the server's cardPower calculation.
        /// </summary>
        private static int GetCardPower(Card card)
        {
            return (int)card.Rank * 4 + (int)card.Suit;
        }

        /// <summary>
        /// Determines if newCards can beat prevCards.
        /// Returns true if:
        /// - Both sets have the same length
        /// - The highest power card in newCards is greater than the highest in prevCards
        /// </summary>
        public static bool CanBeat(IReadOnlyList<Card> prevCards, IReadOnlyList<Card> newCards)
        {
            if (prevCards.Count != newCards.Count)
            {
                return false;
            }

            int maxPrev = prevCards.Max(c => GetCardPower(c));
            int maxNew = newCards.Max(c => GetCardPower(c));

            return maxNew > maxPrev;
        }

        /// <summary>
        /// Checks if the player has any valid move against the current board.
        /// A player has a valid move if:
        /// - The board is empty (they can play any card), OR
        /// - They have at least one card that can beat the board
        /// 
        /// If the player has no valid moves, they should pass (skip).
        /// </summary>
        public static bool HasValidMove(IReadOnlyList<Card> playerHand, IReadOnlyList<Card> currentBoard)
        {
            // Empty board means player can play anything (first card of round)
            if (currentBoard == null || currentBoard.Count == 0)
            {
                return playerHand != null && playerHand.Count > 0;
            }

            // Player must beat the board with same-length card set
            // For simplicity, we check if they can play any single card that beats the board
            // (This is a basic check; full implementation would verify valid sets like pairs, straights, etc.)
            
            // Check if player has a single card that beats the board
            var singleCardThatBeats = playerHand?.FirstOrDefault(card =>
                CanBeat(currentBoard, new List<Card> { card })
            );

            return singleCardThatBeats != null;
        }
    }
}
