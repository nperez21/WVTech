using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Models.DTO;
using MealPlanner.Services;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class KrogerExportServiceTests
{
    private Mock<IKrogerService> _krogerServiceMock;
    private Mock<IKrogerExportRepository> _exportRepoMock;
    private Mock<IShoppingListRepository> _shoppingListRepoMock;
    private ShoppingListService _shoppingListService;
    private KrogerExportService _service;

    private const string UserId = "user-1";
    private const string StoreId = "store-123";
    private const string Token = "kroger-token";

    [SetUp]
    public void SetUp()
    {
        _krogerServiceMock = new Mock<IKrogerService>();
        _exportRepoMock = new Mock<IKrogerExportRepository>();
        _shoppingListRepoMock = new Mock<IShoppingListRepository>();

        _shoppingListRepoMock.Setup(r => r.GetByUserId(UserId))
            .Returns([new ShoppingListItem { UserId = UserId, IngredientBase = new IngredientBase { Name = "chicken broth" }, Amount = 2, Measurement = new Measurement { Name = "Cup(s)" } }]);

        _exportRepoMock.Setup(r => r.SaveExportAsync(It.IsAny<KrogerExport>()))
            .Returns(Task.CompletedTask);

        _shoppingListService = new ShoppingListService(_shoppingListRepoMock.Object, Mock.Of<IMealRepository>(), Mock.Of<IIngredientBaseRepository>(), Mock.Of<IRepository<Measurement>>());

        _service = new KrogerExportService(
            _exportRepoMock.Object,
            _shoppingListService,
            _krogerServiceMock.Object);
    }

    [Test]
    public async Task RunExportAsync_ReturnsNoItems_WhenShoppingListIsEmpty()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(UserId)).Returns([]);

        var result = await _service.RunExportAsync(UserId, StoreId, Token);

        Assert.That(result.Outcome, Is.EqualTo(KrogerExportOutcome.NoItems));
    }

    [Test]
    public async Task RunExportAsync_ReturnsSearchTokenFailed_WhenClientCredentialsFail()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync((string?)null);

        var result = await _service.RunExportAsync(UserId, StoreId, Token);

        Assert.That(result.Outcome, Is.EqualTo(KrogerExportOutcome.SearchTokenFailed));
    }

    [Test]
    public async Task RunExportAsync_ReturnsNoMatchesFound_WhenNoProductsMatch()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((KrogerProductMatch?)null);

        var result = await _service.RunExportAsync(UserId, StoreId, Token);

        Assert.That(result.Outcome, Is.EqualTo(KrogerExportOutcome.NoMatchesFound));
    }

    [Test]
    public async Task RunExportAsync_ReturnsExportFailed_WhenCartExportFails()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new KrogerProductMatch { Upc = "0001234567890", Quantity = 1 });
        _krogerServiceMock.Setup(s => s.ExportCartAsync(It.IsAny<IEnumerable<KrogerCartItem>>(), Token))
            .ReturnsAsync(false);

        var result = await _service.RunExportAsync(UserId, StoreId, Token);

        Assert.That(result.Outcome, Is.EqualTo(KrogerExportOutcome.ExportFailed));
    }

    [Test]
    public async Task RunExportAsync_ReturnsSuccess_WhenExportSucceeds()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new KrogerProductMatch { Upc = "0001234567890", Quantity = 1 });
        _krogerServiceMock.Setup(s => s.ExportCartAsync(It.IsAny<IEnumerable<KrogerCartItem>>(), Token))
            .ReturnsAsync(true);

        var result = await _service.RunExportAsync(UserId, StoreId, Token);

        Assert.That(result.Outcome, Is.EqualTo(KrogerExportOutcome.Success));
        Assert.That(result.ItemsAdded, Is.EqualTo(1));
    }

    [Test]
    public async Task RunExportAsync_DoesNotClearShoppingList_WhenExportSucceeds()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new KrogerProductMatch { Upc = "0001234567890", Quantity = 1 });
        _krogerServiceMock.Setup(s => s.ExportCartAsync(It.IsAny<IEnumerable<KrogerCartItem>>(), Token))
            .ReturnsAsync(true);

        await _service.RunExportAsync(UserId, StoreId, Token);

        _shoppingListRepoMock.Verify(r => r.ClearAllItems(UserId), Times.Never);
    }

    [Test]
    public async Task RunExportAsync_DoesNotClearShoppingList_WhenExportFails()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new KrogerProductMatch { Upc = "0001234567890", Quantity = 1 });
        _krogerServiceMock.Setup(s => s.ExportCartAsync(It.IsAny<IEnumerable<KrogerCartItem>>(), Token))
            .ReturnsAsync(false);

        await _service.RunExportAsync(UserId, StoreId, Token);

        _shoppingListRepoMock.Verify(r => r.ClearAllItems(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task RunExportAsync_SavesExportRecord_WhenExportSucceeds()
    {
        _krogerServiceMock.Setup(s => s.GetClientCredentialsTokenAsync()).ReturnsAsync("search-token");
        _krogerServiceMock.Setup(s => s.SearchProductUpcAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new KrogerProductMatch { Upc = "0001234567890", Quantity = 1 });
        _krogerServiceMock.Setup(s => s.ExportCartAsync(It.IsAny<IEnumerable<KrogerCartItem>>(), Token))
            .ReturnsAsync(true);

        await _service.RunExportAsync(UserId, StoreId, Token);

        _exportRepoMock.Verify(r => r.SaveExportAsync(It.Is<KrogerExport>(e =>
            e.UserId == UserId && e.Items.Count == 1)), Times.Once);
    }
}
