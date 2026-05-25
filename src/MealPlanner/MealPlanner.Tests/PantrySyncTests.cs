using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Services;
using Moq;
using NUnit.Framework;

namespace MealPlanner.Tests;

/// <summary>
/// Tests for pantry-to-shopping-list sync logic introduced in WVT-XXX.
/// Verifies that SyncFromDateRangeAsync subtracts pantry amounts from
/// meal ingredient requirements before adding to the shopping list.
/// </summary>
[TestFixture]
public class PantrySyncTests
{
    private Mock<IShoppingListRepository> _repo;
    private Mock<IMealRepository> _mealRepo;
    private Mock<IUserRepository> _userRepo;
    private ShoppingListService _service;

    private static readonly Measurement _cup = new() { Id = 1, Name = "Cup", Abbreviation = "cup" };
    private static readonly Measurement _tbsp = new() { Id = 2, Name = "Tablespoon", Abbreviation = "tbsp" };

    [SetUp]
    public void SetUp()
    {
        _repo = new Mock<IShoppingListRepository>();
        _mealRepo = new Mock<IMealRepository>();
        _userRepo = new Mock<IUserRepository>();

        _repo.Setup(r => r.GetByUserId(It.IsAny<string>())).Returns(new List<ShoppingListItem>());
        _repo.Setup(r => r.GetDismissedIngredientBaseIds(It.IsAny<string>())).Returns(new HashSet<int>());
        _userRepo.Setup(r => r.GetByUserId(It.IsAny<string>())).Returns(new List<Ingredient>());

        _service = new ShoppingListService(
            _repo.Object,
            _mealRepo.Object,
            Mock.Of<IIngredientBaseRepository>(),
            Mock.Of<IRepository<Measurement>>(),
            _userRepo.Object);
    }

    private static Meal MealWithIngredient(IngredientBase ingredientBase, Measurement measurement, float amount) =>
        new()
        {
            Id = 1,
            UserId = "user-1",
            Recipes =
            [
                new Recipe
                {
                    Id = 10,
                    Name = "Recipe",
                    Ingredients =
                    [
                        new Ingredient { IngredientBase = ingredientBase, Measurement = measurement, Amount = amount }
                    ]
                }
            ]
        };

    private static Ingredient PantryItem(IngredientBase ingredientBase, Measurement measurement, float amount) =>
        new() { IngredientBase = ingredientBase, Measurement = measurement, Amount = amount };

    // --- Pantry fully covers the meal ingredient ---

    [Test]
    public async Task Sync_PantryExactlyCoversMealIngredient_NothingAddedToShoppingList()
    {
        var flour = new IngredientBase { Id = 1, Name = "flour" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(flour, _cup, 2f)]);
        _userRepo.Setup(r => r.GetByUserId("user-1"))
            .Returns([PantryItem(flour, _cup, 2f)]);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    [Test]
    public async Task Sync_PantryMoreThanMealIngredient_NothingAddedToShoppingList()
    {
        var sugar = new IngredientBase { Id = 2, Name = "sugar" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(sugar, _cup, 1f)]);
        _userRepo.Setup(r => r.GetByUserId("user-1"))
            .Returns([PantryItem(sugar, _cup, 3f)]);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.IsAny<ShoppingListItem>()), Times.Never);
    }

    // --- Pantry partially covers the meal ingredient ---

    [Test]
    public async Task Sync_PantryPartiallyCoversMealIngredient_ReducedAmountAddedToShoppingList()
    {
        var butter = new IngredientBase { Id = 3, Name = "butter" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(butter, _cup, 3f)]);
        _userRepo.Setup(r => r.GetByUserId("user-1"))
            .Returns([PantryItem(butter, _cup, 1f)]);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Id == butter.Id && i.Amount == 2f
        )), Times.Once);
    }

    // --- Pantry in a different unit — no subtraction ---

    [Test]
    public async Task Sync_PantryDifferentUnit_FullAmountAddedToShoppingList()
    {
        var milk = new IngredientBase { Id = 4, Name = "milk" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(milk, _cup, 2f)]);
        _userRepo.Setup(r => r.GetByUserId("user-1"))
            .Returns([PantryItem(milk, _tbsp, 8f)]); // same ingredient, different unit

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Id == milk.Id && i.Amount == 2f
        )), Times.Once);
    }

    // --- Empty pantry — original behaviour unchanged ---

    [Test]
    public async Task Sync_EmptyPantry_FullMealIngredientAddedToShoppingList()
    {
        var egg = new IngredientBase { Id = 5, Name = "egg" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(egg, _cup, 2f)]);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Id == egg.Id && i.Amount == 2f
        )), Times.Once);
    }

    // --- No userRepo injected — behaves like empty pantry ---

    [Test]
    public async Task Sync_NoUserRepoInjected_FullMealIngredientAddedToShoppingList()
    {
        var serviceWithoutPantry = new ShoppingListService(
            _repo.Object,
            _mealRepo.Object,
            Mock.Of<IIngredientBaseRepository>(),
            Mock.Of<IRepository<Measurement>>());

        var oil = new IngredientBase { Id = 6, Name = "oil" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(oil, _cup, 1f)]);

        await serviceWithoutPantry.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Id == oil.Id && i.Amount == 1f
        )), Times.Once);
    }

    // --- Multiple pantry entries for same ingredient (e.g. added twice) sum correctly ---

    [Test]
    public async Task Sync_MultiplePantryEntriesSameIngredientAndUnit_SumsBeforeSubtracting()
    {
        var rice = new IngredientBase { Id = 7, Name = "rice" };
        _mealRepo.Setup(r => r.GetUserMealsByDateRangeWithIngredientsAsync(It.IsAny<User>(), It.IsAny<System.DateTime>(), It.IsAny<System.DateTime>()))
            .ReturnsAsync([MealWithIngredient(rice, _cup, 4f)]);
        _userRepo.Setup(r => r.GetByUserId("user-1"))
            .Returns([
                PantryItem(rice, _cup, 1f),
                PantryItem(rice, _cup, 2f)  // two entries, total 3
            ]);

        await _service.SyncFromDateRangeAsync("user-1", new User { Id = "user-1" }, System.DateTime.Today, System.DateTime.Today);

        _repo.Verify(r => r.Add(It.Is<ShoppingListItem>(i =>
            i.IngredientBase.Id == rice.Id && i.Amount == 1f // 4 - 3 = 1
        )), Times.Once);
    }
}
