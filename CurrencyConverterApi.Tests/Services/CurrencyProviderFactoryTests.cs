using CurrencyConverterApi.Services;
using CurrencyConverterApi.Services.Factory;
using CurrencyConverterApi.Services.Interface;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CurrencyConverterApi.Tests.Services
{
    public class CurrencyProviderFactoryTests
    {
        private readonly Mock<ILogger<CurrencyProviderFactory>> _mockLogger;
        
        public CurrencyProviderFactoryTests()
        {
            _mockLogger = new Mock<ILogger<CurrencyProviderFactory>>();
        }
        
        [Fact]
        public void Constructor_NullProviders_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CurrencyProviderFactory(null, _mockLogger.Object));
        }
        
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var mockProvider = new Mock<ICurrencyProvider>();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new CurrencyProviderFactory(new[] { mockProvider.Object }, null));
        }
        
        [Fact]
        public void Constructor_EmptyProviders_ThrowsInvalidOperationException()
        {
            // Arrange
            var emptyProviders = Enumerable.Empty<ICurrencyProvider>();
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => 
                new CurrencyProviderFactory(emptyProviders, _mockLogger.Object));
        }
        
        [Fact]
        public void GetProvider_Default_ReturnsFirstProvider()
        {
            // Arrange
            var mockProvider1 = new Mock<ICurrencyProvider>();
            var mockProvider2 = new Mock<ICurrencyProvider>();
            
            var providers = new[] { mockProvider1.Object, mockProvider2.Object };
            var factory = new CurrencyProviderFactory(providers, _mockLogger.Object);
            
            // Act
            var result = factory.GetProvider();
            
            // Assert
            Assert.Equal(mockProvider1.Object, result);
        }
        
        [Fact]
        public void GetProvider_FrankfurterTypeWithFrankfurterProvider_ReturnsFrankfurterProvider()
        {
            // Arrange
            var mockGenericProvider = new Mock<ICurrencyProvider>();
            
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();
            var httpClient = new HttpClient(new Mock<HttpMessageHandler>().Object) 
            { 
                BaseAddress = new Uri("https://api.frankfurter.app/") 
            };
            mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(httpClient);

            var frankfurterProvider = new FrankfurterCurrencyProvider(
                mockHttpClientFactory.Object, 
                Mock.Of<ILogger<FrankfurterCurrencyProvider>>());
            
            var providers = new ICurrencyProvider[] { mockGenericProvider.Object, frankfurterProvider };
            var factory = new CurrencyProviderFactory(providers, _mockLogger.Object);
            
            // Act
            var result = factory.GetProvider(ProviderType.Frankfurter);
            
            // Assert
            Assert.Equal(frankfurterProvider, result);
        }
        
        [Fact]
        public void GetProvider_FrankfurterTypeWithoutFrankfurterProvider_ReturnsFirstProvider()
        {
            // Arrange
            var mockProvider1 = new Mock<ICurrencyProvider>();
            var mockProvider2 = new Mock<ICurrencyProvider>();
            
            var providers = new[] { mockProvider1.Object, mockProvider2.Object };
            var factory = new CurrencyProviderFactory(providers, _mockLogger.Object);
            
            // Act
            var result = factory.GetProvider(ProviderType.Frankfurter);
            
            // Assert
            Assert.Equal(mockProvider1.Object, result);
        }
        
        [Fact]
        public void GetProvidersBySupportedCurrency_ReturnsAllProviders()
        {
            // Arrange
            var mockProvider1 = new Mock<ICurrencyProvider>();
            var mockProvider2 = new Mock<ICurrencyProvider>();
            
            var providers = new[] { mockProvider1.Object, mockProvider2.Object };
            var factory = new CurrencyProviderFactory(providers, _mockLogger.Object);
            
            // Act
            var result = factory.GetProvidersBySupportedCurrency("USD");
            
            // Assert
            Assert.Equal(providers, result);
            Assert.Equal(2, result.Count());
        }
    }
}