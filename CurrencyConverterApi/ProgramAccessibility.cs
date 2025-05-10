// This file enables integration tests to access the Program class
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("CurrencyConverterApi.Tests")]

namespace CurrencyConverterApi
{
    public partial class Program { }
}
