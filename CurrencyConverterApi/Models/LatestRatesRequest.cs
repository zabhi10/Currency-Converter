using Microsoft.AspNetCore.Mvc; // Required for FromQuery

namespace CurrencyConverterApi.Models
{
    public class LatestRatesRequest
    {
        [FromQuery(Name = "baseCurrency")]
        public string? BaseCurrency { get; set; }
    }
}
