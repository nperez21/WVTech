using MealPlanner.Models;
using MealPlanner.Services;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT20Steps
{
    private readonly IWebDriver _driver;
    private readonly string _baseUrl;
    private readonly WebDriverWait _wait;

    private string _testIngredientName = string.Empty;
    private readonly string _manualItemName = "ManualShoppingItem";
    private int _testMealId;


    public WVT20Steps()
    {
        _driver = BDDSetup.Driver;
        _baseUrl = AUTHost.BaseUrl;
        _wait = BDDSetup.Wait;
    }

    private void NavigateToShoppingList()
    {
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListSynced");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateFrom");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateTo");
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping/Index");
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
    }

    private string GetAliceId(MealPlannerDBContext ctx) =>
        ctx.Users.First(u => u.Email == "Alice@fakeemail.com").Id;

    private Recipe CreateRecipeWithIngredient(MealPlannerDBContext ctx, string ingredientName)
    {
        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == ingredientName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = ingredientName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = $"Recipe_{ingredientName}",
            Directions = "Test directions",
            Calories = 100,
            Protein = 5,
            Carbs = 10,
            Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = ingredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = 2 }
            }
        };

        ctx.Recipes.Add(recipe);
        return recipe;
    }

    [Given("'Alice' has an upcoming meal with ingredients")]
    public void GivenAliceHasAnUpcomingMealWithIngredients()
    {
        _testIngredientName = "AliceTestIngredient";

        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var recipe = CreateRecipeWithIngredient(ctx, _testIngredientName);

        var meal = new Meal
        {
            UserId = userId,
            Title = "Alice Test Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [When("'Alice' views her shopping list")]
    public void WhenAliceViewsHerShoppingList()
    {
        NavigateToShoppingList();
    }

    [Then("the shopping list contains the ingredients from her upcoming meal")]
    public void ThenTheShoppingListContainsIngredientsFromMeal()
    {
        _wait.Until(d => d.PageSource.Contains(_testIngredientName));
        Assert.That(_driver.PageSource, Does.Contain(_testIngredientName));
    }

    [Given("'Alice' is on the create meal page")]
    public void GivenAliceIsOnTheCreateMealPage()
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Meal/NewMeal");
    }

    [When("'Alice' creates a meal with a recipe that has ingredients")]
    public void WhenAliceCreatesAMealWithARecipeThatHasIngredients()
    {
        _testIngredientName = "AliceNewMealIngredient";

        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var recipe = CreateRecipeWithIngredient(ctx, _testIngredientName);

        var meal = new Meal
        {
            UserId = userId,
            Title = "Alice Created Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [Then("the ingredients from that recipe appear on her shopping list")]
    public void ThenIngredientsFromRecipeAppearOnShoppingList()
    {
        NavigateToShoppingList();
        _wait.Until(d => d.PageSource.Contains(_testIngredientName));
        Assert.That(_driver.PageSource, Does.Contain(_testIngredientName));
    }

    [When("'Alice' deletes that meal")]
    public void WhenAliceDeletesThatMeal()
    {
        using var ctx = BDDSetup.CreateContext();
        var meal = ctx.Meals.Find(_testMealId);
        if (meal != null)
        {
            ctx.Meals.Remove(meal);
            ctx.SaveChanges();
        }

        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListSynced");
    }

    [Then("the ingredients from that meal are no longer on her shopping list")]
    public void ThenIngredientsFromMealAreNoLongerOnShoppingList()
    {
        NavigateToShoppingList();
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        Assert.That(_driver.PageSource, Does.Not.Contain(_testIngredientName));
    }

    [Given("'Alice' has two upcoming meals that share an ingredient")]
    public void GivenAliceHasTwoUpcomingMealsThatShareAnIngredient()
    {
        _testIngredientName = "SharedMealIngredient";

        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == _testIngredientName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = _testIngredientName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        var recipe1 = new Recipe
        {
            Name = "SharedIngredientRecipe1",
            Directions = "Test",
            Calories = 100, Protein = 5, Carbs = 10, Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = _testIngredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = 1 }
            }
        };
        var recipe2 = new Recipe
        {
            Name = "SharedIngredientRecipe2",
            Directions = "Test",
            Calories = 100, Protein = 5, Carbs = 10, Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = _testIngredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = 2 }
            }
        };

        var meal1 = new Meal { UserId = userId, Title = "Shared Meal 1", StartTime = DateTime.Today.AddHours(10) };
        var meal2 = new Meal { UserId = userId, Title = "Shared Meal 2", StartTime = DateTime.Today.AddHours(14) };
        meal1.Recipes.Add(recipe1);
        meal2.Recipes.Add(recipe2);

        ctx.Recipes.AddRange(recipe1, recipe2);
        ctx.Meals.AddRange(meal1, meal2);
        ctx.SaveChanges();
    }

    [Then("that shared ingredient appears only once on the shopping list")]
    public void ThenSharedIngredientAppearsOnlyOnce()
    {
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        var occurrences = _driver.FindElements(By.XPath($"//*[contains(text(), '{_testIngredientName}')]")).Count;
        Assert.That(occurrences, Is.EqualTo(1));
    }

    [Given("'Alice' has manually added an item to her shopping list")]
    public void GivenAliceHasManuallyAddedAnItem()
    {
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == _manualItemName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = _manualItemName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        ctx.Set<ShoppingListItem>().Add(new ShoppingListItem
        {
            UserId = userId,
            IngredientBase = ingredientBase,
            Measurement = measurement,
            Amount = 1,
            IsAutoAdded = false
        });
        ctx.SaveChanges();
    }

    [Then("both the auto-populated ingredients and the manually added item are present")]
    public void ThenBothAutoAndManualItemsArePresent()
    {
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        Assert.That(_driver.PageSource, Does.Contain(_testIngredientName));
        Assert.That(_driver.PageSource, Does.Contain(_manualItemName));
    }

    [Then("the manually added item is still on her shopping list")]
    public void ThenManualItemIsStillOnShoppingList()
    {
        NavigateToShoppingList();
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        Assert.That(_driver.PageSource, Does.Contain(_manualItemName));
    }

    [Then("the shopping list items are saved to the database")]
    public void ThenShoppingListItemsAreSavedToDatabase()
    {
        NavigateToShoppingList();
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var items = ctx.Set<ShoppingListItem>().Where(i => i.UserId == userId).ToList();
        Assert.That(items.Any(i => i.IngredientBase.Name.ToLower() == _testIngredientName.ToLower()), Is.True);
    }

    [Given("'Alice' has an upcoming meal with an ingredient named {string}")]
    public void GivenAliceHasAnUpcomingMealWithAnIngredientNamed(string ingredientName)
    {
        _testIngredientName = ingredientName;
        var normalizedName = IngredientNameNormalizer.NormalizeKey(ingredientName);

        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var staleItems = ctx.Set<ShoppingListItem>()
            .Where(i => i.UserId == userId && i.IngredientBase.Name == normalizedName).ToList();
        ctx.Set<ShoppingListItem>().RemoveRange(staleItems);
        var staleMeals = ctx.Meals
            .Where(m => m.UserId == userId && m.Title == $"{ingredientName} Meal").ToList();
        ctx.Meals.RemoveRange(staleMeals);
        ctx.SaveChanges();

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == normalizedName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = normalizedName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        var recipe = new Recipe
        {
            Name = $"{ingredientName}Recipe",
            Directions = "Test",
            Calories = 100, Protein = 5, Carbs = 10, Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = ingredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = 10 }
            }
        };
        ctx.Recipes.Add(recipe);

        var meal = new Meal
        {
            UserId = userId,
            Title = $"{ingredientName} Meal",
            StartTime = DateTime.Today.AddHours(12)
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
        _testMealId = meal.Id;
    }

    [Given("'Alice' has manually added {string} to her shopping list")]
    public void GivenAliceHasManuallyAddedNamedItemToShoppingList(string itemName)
    {
        var normalizedName = IngredientNameNormalizer.NormalizeKey(itemName);

        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == normalizedName);
        if (ingredientBase == null)
        {
            ingredientBase = new IngredientBase { Name = normalizedName };
            ctx.Set<IngredientBase>().Add(ingredientBase);
            ctx.SaveChanges();
        }

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count");
        if (measurement == null)
        {
            measurement = new Measurement { Name = "Count", Abbreviation = "Count" };
            ctx.Set<Measurement>().Add(measurement);
            ctx.SaveChanges();
        }

        ctx.Set<ShoppingListItem>().Add(new ShoppingListItem
        {
            UserId = userId,
            IngredientBase = ingredientBase,
            Measurement = measurement,
            Amount = 1,
            IsAutoAdded = false
        });
        ctx.SaveChanges();
    }

    [Then("{string} appears only once on the shopping list")]
    public void ThenIngredientAppearsOnlyOnce(string ingredientName)
    {
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        var occurrences = _driver.FindElements(
            By.XPath($"//*[contains(@class,'item-display') and contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{ingredientName.ToLower()}')]")
        ).Count;
        Assert.That(occurrences, Is.EqualTo(1));
    }

    [When("'Alice' updates the quantity of '(.*)' to (.*)")]
    public void WhenAliceUpdatesTheQuantityOf(string itemName, float newAmount)
    {
        var amountStr = newAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var span = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(itemName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(span, Is.Not.Null, $"Shopping list item '{itemName}' not found");

        var input = span!.FindElement(By.XPath("../preceding-sibling::div[1]//input[contains(@class,'qty-input')]"));
        var measurement = input.GetAttribute("data-measurement") ?? "";
        var combinedValue = string.IsNullOrEmpty(measurement) ? amountStr : $"{amountStr} {measurement}";

        // Set the displayed value and trigger focusout so the JS handler detects amountChanged and saves
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].value = arguments[1]", input, combinedValue);
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].dispatchEvent(new Event('focusout', {bubbles:true}))", input);

        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the shopping list shows quantity (.*) for '(.*)'")]
    public void ThenTheShoppingListShowsQuantityFor(float expectedAmount, string itemName)
    {
        NavigateToShoppingList();
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));

        var input = _wait.Until(d =>
        {
            try
            {
                var span = d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(itemName, StringComparison.OrdinalIgnoreCase));
                if (span == null) return null;
                return span.FindElement(By.XPath("../preceding-sibling::div[1]//input[contains(@class,'qty-input')]"));
            }
            catch (StaleElementReferenceException) { return null; }
        });

        Assert.That(input, Is.Not.Null);
        var displayedAmount = float.Parse(
            input!.GetAttribute("data-original-amount") ?? "0",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(displayedAmount, Is.EqualTo(expectedAmount));
    }

    [When("{string} sets the quantity display of {string} to {string}")]
    public void WhenUserSetsQuantityDisplayOf(string username, string ingredientName, string displayValue)
    {
        var span = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(span, Is.Not.Null, $"Shopping list item '{ingredientName}' not found");

        var input = span!.FindElement(By.XPath("../preceding-sibling::div[1]//input[contains(@class,'qty-input')]"));
        var measurement = input.GetAttribute("data-measurement") ?? "";
        var combinedValue = string.IsNullOrEmpty(measurement) ? displayValue : $"{displayValue} {measurement}";

        bool isDecimal = displayValue.Contains('.');
        var slashIdx = displayValue.LastIndexOf('/');
        string denominator = slashIdx > 0 ? displayValue[(slashIdx + 1)..].Trim() : "1";

        ((IJavaScriptExecutor)_driver).ExecuteScript(
            @"arguments[0].value = arguments[1];
              arguments[0].dataset.original = arguments[1];
              arguments[0].dataset.originalAmount = arguments[2];
              arguments[0].dataset.isDecimal = arguments[3];
              arguments[0].dataset.denominator = arguments[4];",
            input, combinedValue, displayValue, isDecimal.ToString().ToLower(), denominator);
    }

    [When("{string} clicks increment on {string}")]
    public void WhenUserClicksIncrementOn(string username, string ingredientName)
    {
        var span = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".item-display[data-name]"))
                    .FirstOrDefault(s => s.GetAttribute("data-name")
                        .Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return null; }
        });
        Assert.That(span, Is.Not.Null, $"Shopping list item '{ingredientName}' not found");

        var btn = span!.FindElement(By.XPath("../preceding-sibling::div[1]//button[contains(@class,'qty-increment')]"));
        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].click()", btn);

        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("the associated shopping list items are removed from the database")]
    public void ThenAssociatedShoppingListItemsAreRemovedFromDatabase()
    {
        NavigateToShoppingList();
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);
        var items = ctx.Set<ShoppingListItem>()
            .Where(i => i.UserId == userId && i.IsAutoAdded)
            .ToList();
        Assert.That(items.Any(i => i.IngredientBase.Name.ToLower() == _testIngredientName.ToLower()), Is.False);
    }

    private void NavigateToShoppingListForDate(DateTime date)
    {
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListSynced");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateFrom");
        _driver.Manage().Cookies.DeleteCookieNamed("ShoppingListDateTo");
        var dateStr = date.ToString("yyyy-MM-dd");
        _driver.Manage().Cookies.AddCookie(new Cookie("ShoppingListDateFrom", dateStr));
        _driver.Manage().Cookies.AddCookie(new Cookie("ShoppingListDateTo", dateStr));
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping/Index");
        _wait.Until(d => d.Url.Contains("/Shopping/Index") &&
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").ToString() == "complete");
    }

    private void CreateMealWithIngredientOnDate(string ingredientName, DateTime startTime)
    {
        var normalizedName = IngredientNameNormalizer.NormalizeKey(ingredientName);
        using var ctx = BDDSetup.CreateContext();
        var userId = GetAliceId(ctx);

        var staleMeals = ctx.Meals
            .Where(m => m.UserId == userId && m.Title == $"{ingredientName}_RangeMeal")
            .ToList();
        ctx.Meals.RemoveRange(staleMeals);
        ctx.SaveChanges();

        var ingredientBase = ctx.Set<IngredientBase>().FirstOrDefault(ib => ib.Name == normalizedName)
            ?? ctx.Set<IngredientBase>().Add(new IngredientBase { Name = normalizedName }).Entity;
        ctx.SaveChanges();

        var measurement = ctx.Set<Measurement>().FirstOrDefault(m => m.Name == "Count")
            ?? ctx.Set<Measurement>().Add(new Measurement { Name = "Count", Abbreviation = "Count" }).Entity;
        ctx.SaveChanges();

        var recipe = new Recipe
        {
            Name = $"Recipe_{ingredientName}",
            Directions = "Test",
            Calories = 100, Protein = 5, Carbs = 10, Fat = 3,
            Ingredients = new List<Ingredient>
            {
                new Ingredient { DisplayName = ingredientName, IngredientBase = ingredientBase, Measurement = measurement, Amount = 1 }
            }
        };
        ctx.Recipes.Add(recipe);

        var meal = new Meal
        {
            UserId = userId,
            Title = $"{ingredientName}_RangeMeal",
            StartTime = startTime
        };
        meal.Recipes.Add(recipe);
        ctx.Meals.Add(meal);
        ctx.SaveChanges();
    }

    [Given("'{string}' has a meal with ingredient '{string}' scheduled on today")]
    [Given("{string} has a meal with ingredient {string} scheduled on today")]
    public void GivenUserHasMealWithIngredientScheduledToday(string username, string ingredientName)
    {
        CreateMealWithIngredientOnDate(ingredientName, DateTime.Today.AddHours(12));
    }

    [Given("'{string}' has a meal with ingredient '{string}' scheduled {int} days from now")]
    [Given("{string} has a meal with ingredient {string} scheduled {int} days from now")]
    public void GivenUserHasMealWithIngredientScheduledDaysFromNow(string username, string ingredientName, int days)
    {
        CreateMealWithIngredientOnDate(ingredientName, DateTime.Today.AddDays(days).AddHours(12));
    }

    [When("'{string}' views the shopping list for today's date")]
    [When("{string} views the shopping list for today's date")]
    public void WhenUserViewsShoppingListForToday(string username)
    {
        NavigateToShoppingListForDate(DateTime.Today);
    }

    [When("'{string}' views the shopping list for {int} days from now")]
    [When("{string} views the shopping list for {int} days from now")]
    public void WhenUserViewsShoppingListForDaysFromNow(string username, int days)
    {
        NavigateToShoppingListForDate(DateTime.Today.AddDays(days));
    }

    [Then("the shopping list contains {string}")]
    public void ThenShoppingListContains(string ingredientName)
    {
        _wait.Until(d => d.PageSource.Contains(ingredientName, StringComparison.OrdinalIgnoreCase));
        Assert.That(_driver.PageSource, Does.Contain(ingredientName).IgnoreCase);
    }

    [Then("the shopping list does not contain {string}")]
    public void ThenShoppingListDoesNotContain(string ingredientName)
    {
        _wait.Until(d => d.Url.Contains("/Shopping/Index"));
        Assert.That(_driver.PageSource, Does.Not.Contain(ingredientName).IgnoreCase);
    }
}
