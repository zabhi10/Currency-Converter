using System;
using Microsoft.AspNetCore.Mvc;

namespace CurrencyConverterApi.Models
{
    public class HistoricalRatesRequest
    {
        [FromQuery(Name = "baseCurrency")]
        public string? BaseCurrency { get; set; }

        [FromQuery(Name = "start")]
        public DateTime? StartDate { get; set; }

        [FromQuery(Name = "end")]
        public DateTime? EndDate { get; set; }

        [FromQuery(Name = "page")]
        public int Page { get; set; } = 1;

        [FromQuery(Name = "pageSize")]
        public int PageSize { get; set; } = 10;
    }
}
