using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CurrencyConverterApi.Services.Factory;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CurrencyConverterApi.Services
{
    public interface ICurrencyService
    {
        Task<IDictionary<string, decimal>> GetLatestAsync(string baseCurrency);
        Task<IDictionary<string, decimal>> ConvertAsync(string baseCurrency, IEnumerable<string> targets, decimal amount);
        Task<IEnumerable<(DateTime Date, IDictionary<string, decimal> Rates)>> GetHistoricalAsync(string baseCurrency, DateTime start, DateTime end);
    }

    /// <summary>
    /// Caches currency data to minimize direct calls to external APIs
    /// </summary>
    public class CachedCurrencyService : ICurrencyService
    {
        private readonly IMemoryCache _cache;
        private readonly CurrencyProviderFactory _factory;
        private readonly ILogger<CachedCurrencyService> _logger;
        private static readonly ActivitySource _activitySource = new ActivitySource("CurrencyConverterApi.CacheService");

        // Cache expiration times
        private static readonly TimeSpan LatestRatesExpiration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ConversionExpiration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan HistoricalRatesExpiration = TimeSpan.FromHours(6);

        public CachedCurrencyService(
            IMemoryCache cache,
            CurrencyProviderFactory factory,
            ILogger<CachedCurrencyService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the latest exchange rates for a base currency from the cache or from the provider
        /// </summary>
        /// <param name="baseCurrency">Base currency code</param>
        /// <returns>Dictionary of currency codes to exchange rates</returns>
        public Task<IDictionary<string, decimal>> GetLatestAsync(string baseCurrency)
        {
            if (baseCurrency == null)
            {
                throw new ArgumentNullException(nameof(baseCurrency));
            }

            using var activity = _activitySource.StartActivity("CacheLatestRates");
            activity?.SetTag("currency.base", baseCurrency);

            var cacheKey = $"latest-{baseCurrency}";
            activity?.SetTag("cache.key", cacheKey);

            _logger.LogDebug("Attempting to retrieve latest rates for {BaseCurrency} from cache", baseCurrency);

            return _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = LatestRatesExpiration;
                activity?.SetTag("cache.hit", false);
                
                _logger.LogInformation("Cache miss for latest rates with {BaseCurrency}, fetching from provider", baseCurrency);
                
                var stopwatch = Stopwatch.StartNew();
                var result = await _factory.GetProvider().GetLatestAsync(baseCurrency);
                stopwatch.Stop();
                
                _logger.LogInformation("Retrieved latest rates for {BaseCurrency} from provider in {ElapsedMs}ms and stored in cache", 
                    baseCurrency, stopwatch.ElapsedMilliseconds);
                
                activity?.SetTag("provider.time_ms", stopwatch.ElapsedMilliseconds);
                
                return result;
            })!;
        }

        /// <summary>
        /// Converts an amount from one currency to others from the cache or from the provider
        /// </summary>
        /// <param name="baseCurrency">The source currency</param>
        /// <param name="targets">The target currencies</param>
        /// <param name="amount">The amount to convert</param>
        /// <returns>Dictionary of currency codes to converted amounts</returns>
        public Task<IDictionary<string, decimal>> ConvertAsync(string baseCurrency, IEnumerable<string> targets, decimal amount)
        {
            if (baseCurrency == null)
            {
                throw new ArgumentNullException(nameof(baseCurrency));
            }
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            using var activity = _activitySource.StartActivity("CacheConversion");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("amount", amount);
            
            var targetsString = string.Join(",", targets);
            activity?.SetTag("currency.targets", targetsString);
            
            var key = $"convert-{baseCurrency}-{targetsString}-{amount}";
            activity?.SetTag("cache.key", key);
            
            _logger.LogDebug("Attempting to retrieve conversion of {Amount} {BaseCurrency} to {Targets} from cache", 
                amount, baseCurrency, targetsString);

            return _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ConversionExpiration;
                activity?.SetTag("cache.hit", false);
                
                _logger.LogInformation("Cache miss for conversion of {Amount} {BaseCurrency} to {Targets}, fetching from provider", 
                    amount, baseCurrency, targetsString);
                
                var stopwatch = Stopwatch.StartNew();
                var result = await _factory.GetProvider().ConvertAsync(baseCurrency, targets, amount);
                stopwatch.Stop();
                
                _logger.LogInformation("Converted {Amount} {BaseCurrency} to {Targets} from provider in {ElapsedMs}ms and stored in cache", 
                    amount, baseCurrency, targetsString, stopwatch.ElapsedMilliseconds);
                
                activity?.SetTag("provider.time_ms", stopwatch.ElapsedMilliseconds);
                
                return result;
            })!;
        }

        /// <summary>
        /// Gets historical exchange rates for a base currency from the cache or from the provider
        /// </summary>
        /// <param name="baseCurrency">Base currency code</param>
        /// <param name="start">Start date</param>
        /// <param name="end">End date</param>
        /// <returns>Sequence of dates and corresponding exchange rates</returns>
        public Task<IEnumerable<(DateTime Date, IDictionary<string, decimal> Rates)>> GetHistoricalAsync(
            string baseCurrency, DateTime start, DateTime end)
        {
            if (baseCurrency == null)
            {
                throw new ArgumentNullException(nameof(baseCurrency));
            }

            using var activity = _activitySource.StartActivity("CacheHistoricalRates");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("date.start", start.ToString("yyyy-MM-dd"));
            activity?.SetTag("date.end", end.ToString("yyyy-MM-dd"));
            
            var key = $"history-{baseCurrency}-{start:yyyyMMdd}-{end:yyyyMMdd}";
            activity?.SetTag("cache.key", key);
            
            _logger.LogDebug("Attempting to retrieve historical rates for {BaseCurrency} from {Start} to {End} from cache", 
                baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

            return _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = HistoricalRatesExpiration;
                activity?.SetTag("cache.hit", false);
                
                _logger.LogInformation("Cache miss for historical rates for {BaseCurrency} from {Start} to {End}, fetching from provider", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                
                var stopwatch = Stopwatch.StartNew();
                var result = await _factory.GetProvider().GetHistoricalAsync(baseCurrency, start, end);
                stopwatch.Stop();
                
                // Convert the result to a list to avoid deferred execution issues when caching
                var resultList = result?.ToList() ?? new List<(DateTime Date, IDictionary<string, decimal> Rates)>();
                
                _logger.LogInformation("Retrieved historical rates for {BaseCurrency} from {Start} to {End} from provider in {ElapsedMs}ms and stored in cache. Days: {Days}", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), stopwatch.ElapsedMilliseconds, resultList.Count);
                
                activity?.SetTag("provider.time_ms", stopwatch.ElapsedMilliseconds);
                activity?.SetTag("date.count", resultList.Count);
                
                return resultList as IEnumerable<(DateTime Date, IDictionary<string, decimal> Rates)>;
                
            })!;
        }
    }
}