using CurrencyConverterApi.Services;
using CurrencyConverterApi.Services.Factory;
using CurrencyConverterApi.Services.Interface;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Services.Test
{
    public class TestCurrencyProviderFactory : CurrencyProviderFactory
    {
        private readonly ICurrencyProvider _provider;

        public TestCurrencyProviderFactory(ICurrencyProvider provider, ILogger<CurrencyProviderFactory> logger) 
            : base(new[] { provider }, logger)
        {
            _provider = provider;
        }

        public new ICurrencyProvider GetProvider()
        {
            return _provider;
        }

        public new ICurrencyProvider GetProvider(ProviderType type)
        {
            return _provider;
        }
    }
    
    public class CachedCurrencyServiceTests
    {
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ICurrencyProvider> _mockProvider;
        private readonly TestCurrencyProviderFactory _factory;
        private readonly Mock<ILogger<CachedCurrencyService>> _mockLogger;
        private readonly Mock<ILogger<CurrencyProviderFactory>> _mockFactoryLogger;
        private readonly CachedCurrencyService _service;
        
        public CachedCurrencyServiceTests()
        {
            _mockCache = new Mock<IMemoryCache>();
            _mockProvider = new Mock<ICurrencyProvider>();
            _mockLogger = new Mock<ILogger<CachedCurrencyService>>();
            _mockFactoryLogger = new Mock<ILogger<CurrencyProviderFactory>>();
            
            _factory = new TestCurrencyProviderFactory(_mockProvider.Object, _mockFactoryLogger.Object);
            
            // Setup memory cache mock
            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache
                .Setup(m => m.CreateEntry(It.IsAny<object>()))
                .Returns(mockCacheEntry.Object);
            
            _service = new CachedCurrencyService(_mockCache.Object, _factory, _mockLogger.Object);
        }
        
        [Fact]
        public async Task GetLatestAsync_CacheMiss_CallsProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            var mockRates = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m }
            };
            
            object cachedValue = null;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(false);
                
            _mockProvider
                .Setup(p => p.GetLatestAsync(baseCurrency))
                .ReturnsAsync(mockRates);
            
            // Act
            var result = await _service.GetLatestAsync(baseCurrency);
            
            // Assert
            _mockProvider.Verify(p => p.GetLatestAsync(baseCurrency), Times.Once);
            Assert.Equal(mockRates, result);
        }
        
        [Fact]
        public async Task GetLatestAsync_CacheHit_DoesNotCallProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            var mockRates = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m }
            };
            
            object cachedValue = mockRates;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(true);
            
            // Act
            var result = await _service.GetLatestAsync(baseCurrency);
            
            // Assert
            _mockProvider.Verify(p => p.GetLatestAsync(It.IsAny<string>()), Times.Never);
            Assert.Equal(mockRates, result);
        }
        
        [Fact]
        public async Task ConvertAsync_CacheMiss_CallsProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            string[] targets = { "EUR" };
            decimal amount = 100m;
            var mockConversion = new Dictionary<string, decimal>
            {
                { "EUR", 85m }
            };
            
            object cachedValue = null;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(false);
                
            _mockProvider
                .Setup(p => p.ConvertAsync(baseCurrency, targets, amount))
                .ReturnsAsync(mockConversion);
            
            // Act
            var result = await _service.ConvertAsync(baseCurrency, targets, amount);
            
            // Assert
            _mockProvider.Verify(p => p.ConvertAsync(baseCurrency, targets, amount), Times.Once);
            Assert.Equal(mockConversion, result);
        }
        
        [Fact]
        public async Task ConvertAsync_CacheHit_DoesNotCallProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            string[] targets = { "EUR" };
            decimal amount = 100m;
            var mockConversion = new Dictionary<string, decimal>
            {
                { "EUR", 85m }
            };
            
            object cachedValue = mockConversion;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(true);
            
            // Act
            var result = await _service.ConvertAsync(baseCurrency, targets, amount);
            
            // Assert
            _mockProvider.Verify(p => p.ConvertAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<decimal>()), Times.Never);
            Assert.Equal(mockConversion, result);
        }
        
        [Fact]
        public async Task GetHistoricalAsync_CacheMiss_CallsProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            DateTime start = new DateTime(2023, 1, 1);
            DateTime end = new DateTime(2023, 1, 5);
            
            var mockHistorical = new List<(DateTime Date, IDictionary<string, decimal> Rates)>
            {
                (new DateTime(2023, 1, 1), new Dictionary<string, decimal> { { "EUR", 0.85m } }),
                (new DateTime(2023, 1, 2), new Dictionary<string, decimal> { { "EUR", 0.86m } })
            };
            
            object cachedValue = null;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(false);
                
            _mockProvider
                .Setup(p => p.GetHistoricalAsync(baseCurrency, start, end))
                .ReturnsAsync(mockHistorical);
            
            // Act
            var result = await _service.GetHistoricalAsync(baseCurrency, start, end);
            
            // Assert
            _mockProvider.Verify(p => p.GetHistoricalAsync(baseCurrency, start, end), Times.Once);
            Assert.Equal(mockHistorical, result);
        }
        
        [Fact]
        public async Task GetHistoricalAsync_CacheHit_DoesNotCallProvider()
        {
            // Arrange
            string baseCurrency = "USD";
            DateTime start = new DateTime(2023, 1, 1);
            DateTime end = new DateTime(2023, 1, 5);
            
            var mockHistorical = new List<(DateTime Date, IDictionary<string, decimal> Rates)>
            {
                (new DateTime(2023, 1, 1), new Dictionary<string, decimal> { { "EUR", 0.85m } }),
                (new DateTime(2023, 1, 2), new Dictionary<string, decimal> { { "EUR", 0.86m } })
            };
            
            object cachedValue = mockHistorical;
            
            _mockCache
                .Setup(m => m.TryGetValue(It.IsAny<object>(), out cachedValue))
                .Returns(true);
            
            // Act
            var result = await _service.GetHistoricalAsync(baseCurrency, start, end);
            
            // Assert
            _mockProvider.Verify(p => p.GetHistoricalAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
            Assert.Equal(mockHistorical, result);
        }
    }
}