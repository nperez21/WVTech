using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MealPlanner.Controllers;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Models.DTO;
using MealPlanner.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

[TestFixture]
public class WVT180AddToPantryTests
{
    private ShoppingController _controller = null!;
    private MealPlannerDBContext _context = null!;
    private Mock<IPantryService> _pantryServiceMock = null!;
    private Mock<IRegistrationService> _registrationServiceMock = null!;
    private User _user = null!;
    private TestSession180 _session = null!;

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        _context = new MealPlannerDBContext(
            new DbContextOptionsBuilder<MealPlannerDBContext>().UseSqlite(connection).Options);
        _context.Database.EnsureCreated();

        _user = new User { Id = "user-1", FullName = "Gary", UserName = "gary@fakeemail.com" };
        _context.Users.Add(_user);
        _context.SaveChanges();

        _pantryServiceMock = new Mock<IPantryService>();
        _registrationServiceMock = new Mock<IRegistrationService>();
        _registrationServiceMock
            .Setup(r => r.FindUserByClaimAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        var userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        _session = new TestSession180();

        _controller = new ShoppingController(
            Mock.Of<IShoppingListService>(),
            _pantryServiceMock.Object,
            userManagerMock.Object,
            null!,
            _registrationServiceMock.Object,
            Mock.Of<IMeasurementRepository>(),
            _context);

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"));
        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal,
            Session = _session
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    private void SeedSession(List<PantryModalItem> items)
    {
        _session.SetString("PantryModalItems", JsonSerializer.Serialize(items));
    }

    [Test]
    public async Task AddToPantry_WithItemsInSession_RedirectsToPantry()
    {
        SeedSession([new PantryModalItem("Eggs", 12f, "Count")]);
        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()))
            .Returns(new Ingredient
            {
                DisplayName = "Eggs",
                IngredientBase = new IngredientBase { Name = "eggs" },
                Measurement = new Measurement { Name = "Count" },
                Amount = 12f
            });

        var result = await _controller.AddToPantry();

        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Pantry"));
    }

    [Test]
    public async Task AddToPantry_WithMultipleItemsInSession_CallsAddPantryItemForEach()
    {
        var items = new List<PantryModalItem>
        {
            new("Eggs", 12f, "Count"),
            new("Milk", 2f, "Cup")
        };
        SeedSession(items);
        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()))
            .Returns((string name, float amt, string meas) => new Ingredient
            {
                DisplayName = name,
                IngredientBase = new IngredientBase { Name = name.ToLower() },
                Measurement = new Measurement { Name = meas },
                Amount = amt
            });

        await _controller.AddToPantry();

        _pantryServiceMock.Verify(
            s => s.AddPantryItem("user-1", It.IsAny<Ingredient>()),
            Times.Exactly(2));
    }

    [Test]
    public async Task AddToPantry_WithMultipleItemsInSession_SavesAfterEachItem()
    {
        var saveCount = 0;
        var items = new List<PantryModalItem>
        {
            new("Eggs", 12f, "Count"),
            new("Milk", 2f, "Cup")
        };
        SeedSession(items);

        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var countingContext = new MealPlannerDBContext(
            new DbContextOptionsBuilder<MealPlannerDBContext>().UseSqlite(connection).Options);
        countingContext.Database.EnsureCreated();
        countingContext.Users.Add(new User { Id = "user-1" });
        countingContext.SaveChanges();
        saveCount = 0;

        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()))
            .Returns((string name, float amt, string meas) => new Ingredient
            {
                DisplayName = name,
                IngredientBase = new IngredientBase { Name = name.ToLower() },
                Measurement = new Measurement { Name = meas },
                Amount = amt
            });
        _pantryServiceMock
            .Setup(s => s.AddPantryItem(It.IsAny<string>(), It.IsAny<Ingredient>()))
            .Callback(() => saveCount++);

        await _controller.AddToPantry();

        Assert.That(saveCount, Is.EqualTo(2), "AddPantryItem should be called once per item");
        countingContext.Dispose();
        connection.Dispose();
    }

    [Test]
    public async Task AddToPantry_WithItemsInSession_ClearsSessionKey()
    {
        SeedSession([new PantryModalItem("Eggs", 12f, "Count")]);
        _pantryServiceMock
            .Setup(s => s.BuildPantryItem(It.IsAny<string>(), It.IsAny<float>(), It.IsAny<string>()))
            .Returns(new Ingredient
            {
                DisplayName = "Eggs",
                IngredientBase = new IngredientBase { Name = "eggs" },
                Measurement = new Measurement { Name = "Count" },
                Amount = 12f
            });

        await _controller.AddToPantry();

        Assert.That(_session.GetString("PantryModalItems"), Is.Null);
    }

    [Test]
    public async Task AddToPantry_WithNoSessionItems_RedirectsToIndex()
    {
        var result = await _controller.AddToPantry();

        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task AddToPantry_WithNoSessionItems_SetsErrorTempData()
    {
        await _controller.AddToPantry();

        Assert.That(_controller.TempData["ShoppingListError"], Is.Not.Null);
    }

    [Test]
    public async Task AddToPantry_WithNoSessionItems_DoesNotCallPantryService()
    {
        await _controller.AddToPantry();

        _pantryServiceMock.Verify(
            s => s.AddPantryItem(It.IsAny<string>(), It.IsAny<Ingredient>()),
            Times.Never);
    }
}

[TestFixture]
public class WVT180SkipAddToPantryTests
{
    private ShoppingController _controller = null!;
    private MealPlannerDBContext _context = null!;
    private Mock<IPantryService> _pantryServiceMock = null!;
    private TestSession180 _session = null!;

    [SetUp]
    public void SetUp()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        _context = new MealPlannerDBContext(
            new DbContextOptionsBuilder<MealPlannerDBContext>().UseSqlite(connection).Options);
        _context.Database.EnsureCreated();

        _pantryServiceMock = new Mock<IPantryService>();
        _session = new TestSession180();

        var userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);

        _controller = new ShoppingController(
            Mock.Of<IShoppingListService>(),
            _pantryServiceMock.Object,
            userManagerMock.Object,
            null!,
            Mock.Of<IRegistrationService>(),
            Mock.Of<IMeasurementRepository>(),
            _context);

        var httpContext = new DefaultHttpContext { Session = _session };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
        _context.Dispose();
    }

    [Test]
    public void SkipAddToPantry_RedirectsToIndex()
    {
        var result = _controller.SkipAddToPantry();

        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public void SkipAddToPantry_DoesNotCallPantryService()
    {
        _controller.SkipAddToPantry();

        _pantryServiceMock.Verify(
            s => s.AddPantryItem(It.IsAny<string>(), It.IsAny<Ingredient>()),
            Times.Never);
    }

    [Test]
    public void SkipAddToPantry_ClearsSessionItems()
    {
        _session.SetString("PantryModalItems", JsonSerializer.Serialize(
            new List<PantryModalItem> { new("Eggs", 12f, "Count") }));

        _controller.SkipAddToPantry();

        Assert.That(_session.GetString("PantryModalItems"), Is.Null);
    }
}

[TestFixture]
public class WVT180KrogerCaptureTests
{
    private KrogerController _controller = null!;
    private Mock<IKrogerExportService> _exportServiceMock = null!;
    private Mock<IShoppingListRepository> _shoppingListRepoMock = null!;
    private ShoppingListService _shoppingListService = null!;
    private TestSession180 _session = null!;

    private const string TestUserId = "user-1";

    [SetUp]
    public void SetUp()
    {
        var userManagerMock = new Mock<UserManager<User>>(
            Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new User { Id = TestUserId });

        _exportServiceMock = new Mock<IKrogerExportService>();
        _shoppingListRepoMock = new Mock<IShoppingListRepository>();

        _shoppingListService = new ShoppingListService(
            _shoppingListRepoMock.Object,
            Mock.Of<IMealRepository>(),
            Mock.Of<IIngredientBaseRepository>(),
            Mock.Of<IRepository<Measurement>>());

        var userSettingsMock = new Mock<IUserSettingsRepository>();
        userSettingsMock
            .Setup(r => r.SaveZipCodeAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _controller = new KrogerController(
            userSettingsMock.Object,
            _shoppingListService,
            userManagerMock.Object,
            _exportServiceMock.Object,
            Mock.Of<IKrogerService>());

        _session = new TestSession180();
        _session.SetString("KrogerAccessToken", "valid-token");
        _session.SetString("KrogerAccessTokenExpiry",
            DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());

        var httpContext = new DefaultHttpContext
        {
            Session = _session,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, TestUserId)], "TestAuth"))
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown() => _controller.Dispose();

    [Test]
    public async Task Export_OnSuccess_SetsShowPantryModalInSession()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "chicken" },
                Amount = 2f,
                Measurement = new Measurement { Name = "Cup", Abbreviation = "cup" }
            }]);
        _exportServiceMock
            .Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.Success, ItemsAdded = 1, Skipped = [] });

        await _controller.Export("97401", "store-123");

        Assert.That(_session.GetString("ShowPantryModal"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Export_OnSuccess_StoresItemsInSession()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "chicken" },
                Amount = 2f,
                Measurement = new Measurement { Name = "Cup", Abbreviation = "cup" }
            }]);
        _exportServiceMock
            .Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.Success, ItemsAdded = 1, Skipped = [] });

        await _controller.Export("97401", "store-123");

        var json = _session.GetString("PantryModalItems");
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        var items = JsonSerializer.Deserialize<List<PantryModalItem>>(json!);
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items![0].Name, Is.EqualTo("chicken"));
        Assert.That(items[0].Amount, Is.EqualTo(2f));
    }

    [Test]
    public async Task Export_OnFailure_DoesNotSetShowPantryModal()
    {
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "chicken" },
                Amount = 2f,
                Measurement = new Measurement { Name = "Cup", Abbreviation = "cup" }
            }]);
        _exportServiceMock
            .Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.ExportFailed, Skipped = [] });

        await _controller.Export("97401", "store-123");

        Assert.That(_session.GetString("ShowPantryModal"), Is.Null.Or.Not.EqualTo("true"));
    }

    [Test]
    public async Task Export_OnSuccess_CapturesItemsBeforeExportClears()
    {
        var capturedBeforeExport = false;
        _shoppingListRepoMock.Setup(r => r.GetByUserId(TestUserId))
            .Returns([new ShoppingListItem
            {
                UserId = TestUserId,
                IngredientBase = new IngredientBase { Name = "broccoli" },
                Amount = 3f,
                Measurement = new Measurement { Name = "Count", Abbreviation = "ct" }
            }]);
        _exportServiceMock
            .Setup(s => s.RunExportAsync(TestUserId, "store-123", "valid-token"))
            .Callback(() => capturedBeforeExport = _session.GetString("PantryModalItems") == null)
            .ReturnsAsync(new KrogerExportResult { Outcome = KrogerExportOutcome.Success, ItemsAdded = 1, Skipped = [] });

        await _controller.Export("97401", "store-123");

        // Items should be captured (stored in session) AFTER export completes
        Assert.That(_session.GetString("PantryModalItems"), Is.Not.Null);
        // And they were not set before RunExportAsync was called
        Assert.That(capturedBeforeExport, Is.True, "Session should not have items set before export runs");
    }
}

internal class TestSession180 : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();
    public bool IsAvailable => true;
    public string Id => "test-session-180";
    public IEnumerable<string> Keys => _store.Keys;
    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
