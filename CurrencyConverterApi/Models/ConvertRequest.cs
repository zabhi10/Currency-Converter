namespace CurrencyConverterApi.Models
{
    public class ConvertRequest
    {
        public string? BaseCurrency { get; set; }
        public string? TargetCurrency { get; set; }
        public decimal Amount { get; set; }
    }
}
