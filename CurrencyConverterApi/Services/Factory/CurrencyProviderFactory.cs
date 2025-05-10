using CurrencyConverterApi.Services.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CurrencyConverterApi.Services.Factory
{
    public enum ProviderType
    {
        Default,
        Frankfurter,
        // Future providers can be added here
        // ExchangeRatesAPI,
        // Fixer,
        // CurrencyLayer
    }

    /// <summary>
    /// Factory for dynamically selecting currency providers based on the request
    /// </summary>
    public class CurrencyProviderFactory
    {
        private readonly IEnumerable<ICurrencyProvider> _providers;
        private readonly ILogger<CurrencyProviderFactory> _logger;

        public CurrencyProviderFactory(
            IEnumerable<ICurrencyProvider> providers,
            ILogger<CurrencyProviderFactory> logger)
        {
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!_providers.Any())
            {
                _logger.LogError("No currency providers registered");
                throw new InvalidOperationException("No currency providers are registered");
            }
        }

        /// <summary>
        /// Get the default provider
        /// </summary>
        /// <returns>The default currency provider</returns>
        public virtual ICurrencyProvider GetProvider() // Made virtual
        {
            return GetProvider(ProviderType.Default);
        }

        /// <summary>
        /// Get a specific provider by type
        /// </summary>
        /// <param name="type">The provider type to retrieve</param>
        /// <returns>The requested currency provider</returns>
        public virtual ICurrencyProvider GetProvider(ProviderType type) // Made virtual
        {
            _logger.LogDebug("Getting currency provider of type: {ProviderType}", type);
            
            return type switch
            {
                ProviderType.Frankfurter => _providers.OfType<FrankfurterCurrencyProvider>().FirstOrDefault() 
                    ?? _providers.First(),
                // Future providers can be added here
                //ProviderType.ExchangeRatesAPI => _providers.OfType<ExchangeRatesAPICurrencyProvider>().FirstOrDefault() 
                //    ?? _providers.First(),
                
                _ => _providers.First()
            };
        }

        /// <summary>
        /// Get providers that support a specific currency
        /// </summary>
        /// <param name="currency">Currency code</param>
        /// <returns>All providers that support the given currency</returns>
        public IEnumerable<ICurrencyProvider> GetProvidersBySupportedCurrency(string currency)
        {
            _logger.LogDebug("Getting providers that support currency: {Currency}", currency);
            // For future implementation - return providers that support the specific currency
            return _providers;
        }
    }
}