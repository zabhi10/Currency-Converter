using CurrencyConverterApi.Services;
using CurrencyConverterApi.Services.Factory;
using CurrencyConverterApi.Services.Interface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Services
{
    public class TestCachedErrorProviderFactory : CurrencyProviderFactory
    {
        private readonly ICurrencyProvider _provider;
        public bool ThrowErrorOnGetProvider { get; set; } = false;

        public TestCachedErrorProviderFactory(ICurrencyProvider provider, ILogger<CurrencyProviderFactory> logger)
            : base(new[] { provider }, logger)
        {
            _provider = provider;
        }

        public override ICurrencyProvider GetProvider()
        {
            if (ThrowErrorOnGetProvider)
            {
                throw new InvalidOperationException("Test error from GetProvider");
            }
            return _provider;
        }

        public override ICurrencyProvider GetProvider(ProviderType type)
        {
            if (ThrowErrorOnGetProvider)
            {
                throw new InvalidOperationException("Test error from GetProvider with type");
            }
            return _provider;
        }
    }

    public class CachedCurrencyServiceErrorTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ICurrencyProvider> _mockProvider;
        private readonly TestCachedErrorProviderFactory _testFactory;
        private readonly Mock<ILogger<CachedCurrencyService>> _mockLogger;
        private readonly Mock<ILogger<CurrencyProviderFactory>> _mockFactoryLogger;
        private readonly CachedCurrencyService _service;

        public CachedCurrencyServiceErrorTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockProvider = new Mock<ICurrencyProvider>();
            _mockLogger = new Mock<ILogger<CachedCurrencyService>>();
            _mockFactoryLogger = new Mock<ILogger<CurrencyProviderFactory>>();

            _testFactory = new TestCachedErrorProviderFactory(_mockProvider.Object, _mockFactoryLogger.Object);

            _mockCache.Setup(m => m.CreateEntry(It.IsAny<object>()))
                      .Returns(Mock.Of<ICacheEntry>()); 

            object tryGetValueOutput; 
            _mockCache.Setup(m => m.TryGetValue(It.IsAny<object>(), out tryGetValueOutput))
                      .Returns(false); 

            _service = new CachedCurrencyService(_mockCache.Object, _testFactory, _mockLogger.Object); 
        }

        [Fact]
        public async Task GetLatestAsync_ProviderThrowsException_PropagatesException()
        {
            // Arrange
            _mockProvider.Setup(p => p.GetLatestAsync(It.IsAny<string>()))
                         .ThrowsAsync(new InvalidOperationException("Provider error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetLatestAsync("USD"));
            Assert.Equal("Provider error", exception.Message);
        }

        [Fact]
        public async Task ConvertAsync_ProviderThrowsException_PropagatesException()
        {
            // Arrange
            _mockProvider.Setup(p => p.ConvertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<decimal>()))
                         .ThrowsAsync(new HttpRequestException("Provider network error"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(() => _service.ConvertAsync("USD", new[] { "EUR" }, 100m));
            Assert.Equal("Provider network error", exception.Message);
        }

        [Fact]
        public async Task GetHistoricalAsync_ProviderThrowsException_PropagatesException()
        {
            // Arrange
            _mockProvider.Setup(p => p.GetHistoricalAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                         .ThrowsAsync(new TimeoutException("Provider timeout"));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => _service.GetHistoricalAsync("USD", DateTime.Today, DateTime.Today.AddDays(1)));
            Assert.Equal("Provider timeout", exception.Message);
        }

        [Fact]
        public async Task GetLatestAsync_FactoryThrowsException_PropagatesException()
        {
            // Arrange
            _testFactory.ThrowErrorOnGetProvider = true;
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetLatestAsync("USD"));
            Assert.Equal("Test error from GetProvider", exception.Message);
        }
        
        [Fact]
        public async Task ConvertAsync_NullBaseCurrency_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ConvertAsync(null, new[] { "EUR" }, 100m));
        }

        [Fact]
        public async Task ConvertAsync_NullTargets_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ConvertAsync("USD", null, 100m));
        }
        
        [Fact]
        public async Task GetHistoricalAsync_EndDateBeforeStartDate_ThrowsArgumentException()
        {
            // Arrange
            _mockProvider.Setup(p => p.GetHistoricalAsync(It.IsAny<string>(), DateTime.Today, DateTime.Today.AddDays(-1)))
                         .ThrowsAsync(new ArgumentException("End date cannot be before start date."));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetHistoricalAsync("USD", DateTime.Today, DateTime.Today.AddDays(-1)));
            Assert.Equal("End date cannot be before start date.", exception.Message);
        }

        [Fact]
        public async Task GetLatestAsync_NullBaseCurrency_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetLatestAsync(null));
        }

        [Fact]
        public async Task GetHistoricalAsync_NullBaseCurrency_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.GetHistoricalAsync(null, DateTime.Today, DateTime.Today.AddDays(1)));
        }
    }
}
