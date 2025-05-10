using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CurrencyConverterApi.Services.Interface;
using Microsoft.Extensions.Logging;

namespace CurrencyConverterApi.Services
{
    public class FrankfurterCurrencyProvider : ICurrencyProvider
    {
        private readonly IHttpClientFactory _factory;
        private readonly HttpClient _client;
        private readonly ILogger<FrankfurterCurrencyProvider> _logger;
        private static readonly ActivitySource _activitySource = new ActivitySource("CurrencyConverterApi.FrankfurterProvider");

        public FrankfurterCurrencyProvider(
            IHttpClientFactory factory,
            ILogger<FrankfurterCurrencyProvider> logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = _factory.CreateClient("Frankfurter");
            _client.BaseAddress = new Uri("https://api.frankfurter.app");
            
            _logger.LogInformation("FrankfurterCurrencyProvider initialized with base URI: {BaseAddress}", _client.BaseAddress);
        }

        public async Task<IDictionary<string, decimal>> GetLatestAsync(string baseCurrency)
        {
            using var activity = _activitySource.StartActivity("FrankfurterGetLatest");
            activity?.SetTag("currency.base", baseCurrency);
            
            _logger.LogDebug("Getting latest rates from Frankfurter API for base currency: {BaseCurrency}", baseCurrency);

            try
            {
                var requestUri = $"/latest?from={baseCurrency}";
                activity?.SetTag("http.url", $"{_client.BaseAddress}{requestUri}");
                activity?.SetTag("http.method", "GET");

                var stopwatch = Stopwatch.StartNew();
                var res = await _client.GetFromJsonAsync<ExchangeResponse>(requestUri);
                stopwatch.Stop();

                if (res == null)
                {
                    _logger.LogWarning("Frankfurter API returned null response for {BaseCurrency}", baseCurrency);
                    throw new InvalidOperationException($"Failed to retrieve rates for {baseCurrency}");
                }

                _logger.LogInformation("Retrieved latest rates from Frankfurter API for {BaseCurrency} in {ElapsedMs}ms: {CurrencyCount} currencies", 
                    baseCurrency, stopwatch.ElapsedMilliseconds, res.Rates.Count);
                
                activity?.SetTag("currency.count", res.Rates.Count);
                activity?.SetTag("response.time_ms", stopwatch.ElapsedMilliseconds);
                
                return res.Rates;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred while getting latest rates for {BaseCurrency}: {ErrorMessage}", 
                    baseCurrency, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error occurred while getting latest rates for {BaseCurrency}: {ErrorMessage}", 
                    baseCurrency, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new InvalidOperationException($"Failed to parse exchange rate data for {baseCurrency}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while getting latest rates for {BaseCurrency}: {ErrorMessage}", 
                    baseCurrency, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<IDictionary<string, decimal>> ConvertAsync(string baseCurrency, IEnumerable<string> targets, decimal amount)
        {
            using var activity = _activitySource.StartActivity("FrankfurterConvert");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("currency.targets", string.Join(",", targets));
            activity?.SetTag("amount", amount);

            var to = string.Join(",", targets);
            _logger.LogDebug("Converting {Amount} {BaseCurrency} to currencies: {TargetCurrencies}", 
                amount, baseCurrency, to);

            try
            {
                var requestUri = $"/latest?amount={amount.ToString(CultureInfo.InvariantCulture)}&from={baseCurrency}&to={to}";
                activity?.SetTag("http.url", $"{_client.BaseAddress}{requestUri}");
                activity?.SetTag("http.method", "GET");

                var stopwatch = Stopwatch.StartNew();
                var res = await _client.GetFromJsonAsync<ExchangeResponse>(requestUri);
                stopwatch.Stop();
                
                if (res == null)
                {
                    _logger.LogWarning("Frankfurter API returned null response for conversion: {Amount} {BaseCurrency} to {TargetCurrencies}", 
                        amount, baseCurrency, to);
                    throw new InvalidOperationException($"Failed to convert {amount} {baseCurrency} to {to}");
                }

                _logger.LogInformation("Converted {Amount} {BaseCurrency} to {TargetCurrencies} via Frankfurter API in {ElapsedMs}ms", 
                    amount, baseCurrency, to, stopwatch.ElapsedMilliseconds);
                
                activity?.SetTag("currency.count", res.Rates.Count);
                activity?.SetTag("response.time_ms", stopwatch.ElapsedMilliseconds);
                
                return res.Rates;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred during currency conversion: {Amount} {BaseCurrency} to {TargetCurrencies}: {ErrorMessage}", 
                    amount, baseCurrency, to, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error during currency conversion: {Amount} {BaseCurrency} to {TargetCurrencies}: {ErrorMessage}", 
                    amount, baseCurrency, to, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new InvalidOperationException($"Failed to parse conversion data: {amount} {baseCurrency} to {to}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during currency conversion: {Amount} {BaseCurrency} to {TargetCurrencies}: {ErrorMessage}", 
                    amount, baseCurrency, to, ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<(DateTime Date, IDictionary<string, decimal> Rates)>> GetHistoricalAsync(string baseCurrency, DateTime start, DateTime end)
        {
            using var activity = _activitySource.StartActivity("FrankfurterGetHistorical");
            activity?.SetTag("currency.base", baseCurrency);
            activity?.SetTag("date.start", start.ToString("yyyy-MM-dd"));
            activity?.SetTag("date.end", end.ToString("yyyy-MM-dd"));

            _logger.LogDebug("Getting historical rates from Frankfurter API for {BaseCurrency} from {StartDate} to {EndDate}", 
                baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));

            try
            {
                var requestUri = $"/{start:yyyy-MM-dd}..{end:yyyy-MM-dd}?from={baseCurrency}";
                activity?.SetTag("http.url", $"{_client.BaseAddress}{requestUri}");
                activity?.SetTag("http.method", "GET");

                var stopwatch = Stopwatch.StartNew();
                var res = await _client.GetFromJsonAsync<ExchangeHistoryResponse>(requestUri);
                stopwatch.Stop();
                
                if (res == null)
                {
                    _logger.LogWarning("Frankfurter API returned null response for historical rates: {BaseCurrency} from {StartDate} to {EndDate}", 
                        baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
                    throw new InvalidOperationException($"Failed to retrieve historical rates for {baseCurrency}");
                }

                var dates = res.Rates.Count;
                _logger.LogInformation("Retrieved historical rates from Frankfurter API for {BaseCurrency} from {StartDate} to {EndDate} in {ElapsedMs}ms: {DateCount} dates", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), stopwatch.ElapsedMilliseconds, dates);

                activity?.SetTag("date.count", dates);
                activity?.SetTag("response.time_ms", stopwatch.ElapsedMilliseconds);
                
                return res.Rates.Select(r => (DateTime.Parse(r.Key), (IDictionary<string, decimal>)r.Value));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error occurred while getting historical rates: {BaseCurrency} from {StartDate} to {EndDate}: {ErrorMessage}", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error while getting historical rates: {BaseCurrency} from {StartDate} to {EndDate}: {ErrorMessage}", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new InvalidOperationException($"Failed to parse historical rate data for {baseCurrency}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while getting historical rates: {BaseCurrency} from {StartDate} to {EndDate}: {ErrorMessage}", 
                    baseCurrency, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"), ex.Message);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        private record ExchangeResponse(Dictionary<string, decimal> Rates);
        private record ExchangeHistoryResponse(Dictionary<string, Dictionary<string, decimal>> Rates);
    }
}