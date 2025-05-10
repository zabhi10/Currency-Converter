// /Users/sharma5abhishek/Downloads/CurrencyConverterApi.Tests/Integration/TestModels.cs
using System;
using System.Collections.Generic;

namespace CurrencyConverterApi.Tests.Integration.Models
{
    public class TokenResponseModel
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class RateResponseModel
    {
        public string? Base { get; set; }
        public DateTime Date { get; set; }
        public Dictionary<string, decimal>? Rates { get; set; }
    }

    public class ConversionResponseModel
    {
        public string? Base { get; set; }
        public string? Target { get; set; }
        public decimal ConvertedAmount { get; set; }
        public DateTime Date { get; set; }
    }

    public class HistoricalRateModel
    {
        public DateTime Date { get; set; }
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
