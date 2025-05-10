using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CurrencyConverterApi.Models
{
    /// <summary>
    /// Model for latest exchange rates response
    /// </summary>
    public class LatestRatesResponse
    {
        [JsonPropertyName("base")]
        public string Base { get; set; } = string.Empty;
        
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        
        [JsonPropertyName("rates")]
        public IDictionary<string, decimal> Rates { get; set; } = new Dictionary<string, decimal>();
    }
    
    /// <summary>
    /// Model for currency conversion response
    /// </summary>
    public class ConversionResponse
    {
        [JsonPropertyName("base")]
        public string Base { get; set; } = string.Empty;
        
        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("convertedAmount")]
        public decimal ConvertedAmount { get; set; }
        
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
    }
    
    /// <summary>
    /// Model for historical rates response with pagination
    /// </summary>
    public class HistoricalRatesResponse
    {
        [JsonPropertyName("base")]
        public string Base { get; set; } = string.Empty;
        
        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }
        
        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }
        
        [JsonPropertyName("page")]
        public int Page { get; set; }
        
        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }
        
        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }
        
        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }
        
        [JsonPropertyName("data")]
        public IEnumerable<DailyRates> Data { get; set; } = new List<DailyRates>();
        
        public class DailyRates
        {
            [JsonPropertyName("date")]
            public DateTime Date { get; set; }
            
            [JsonPropertyName("rates")]
            public IDictionary<string, decimal> Rates { get; set; } = new Dictionary<string, decimal>();
        }
    }
}