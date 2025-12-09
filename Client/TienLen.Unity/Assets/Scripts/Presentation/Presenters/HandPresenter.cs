using UnityEngine;
using Microsoft.Extensions.Logging;
using VContainer;
using TienLen.Core.Domain.ValueObjects;
using TienLen.Unity.Infrastructure.Logging;
using TienLen.Unity.Infrastructure.Network; // Added
using System.Collections.Generic;

namespace TienLen.Unity.Presentation.Presenters
{
    public class HandPresenter : MonoBehaviour
    {
        private ILogger<HandPresenter> _logger;
        private IGameNetwork _network; // Added

        [Inject]
        public void Construct(ILogger<HandPresenter> logger, IGameNetwork network)
        {
            _logger = logger;
            _network = network;
            _logger.LogInformation("HandPresenter injected.");
        }

        public void OnCardClicked(Card card)
        {
            _logger?.LogInformation("Card clicked: {Card}", card);
            FastLog.Info("FastLog: Card clicked {Card}", card);
            
            // TODO: UI Selection logic here (Toggle selection state)
        }

        // Call this from a UI Button
        public async void OnPlayButtonClicked()
        {
            _logger?.LogInformation("Play Button Clicked");
            
            // Test: Play the first card (Index 0) just to verify network
            // In real app: Get indices of selected cards from the View/Model
            try 
            {
                await _network.SendPlayCardAsync(new List<int> { 0 });
                _logger?.LogInformation("Sent PlayCard Request");
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Failed to send PlayCard");
            }
        }

        public void OnError(string errorMsg)
        {
            _logger?.LogError("Hand Error: {Error}", errorMsg);
        }
    }
}
