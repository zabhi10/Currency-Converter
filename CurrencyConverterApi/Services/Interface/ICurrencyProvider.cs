using System.Threading.Tasks;
using System.Collections.Generic;
using System;
namespace CurrencyConverterApi.Services.Interface
{
    public interface ICurrencyProvider
    {
        Task<IDictionary<string, decimal>> GetLatestAsync(string baseCurrency);
        Task<IDictionary<string, decimal>> ConvertAsync(string baseCurrency, IEnumerable<string> targets, decimal amount);
        Task<IEnumerable<(DateTime Date, IDictionary<string, decimal> Rates)>> GetHistoricalAsync(string baseCurrency, DateTime start, DateTime end);
    }
}