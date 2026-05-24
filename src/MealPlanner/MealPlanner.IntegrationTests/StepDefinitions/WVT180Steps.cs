using MealPlanner.Models;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Reqnroll;

namespace Mealplanner.IntegrationTests;

[Binding]
public class WVT180Steps
{
    private readonly IWebDriver _driver;
    private readonly WebDriverWait _wait;
    private readonly string _baseUrl;

    public WVT180Steps()
    {
        _driver = BDDSetup.Driver;
        _wait = BDDSetup.Wait;
        _baseUrl = AUTHost.BaseUrl;
    }

    [Given("{string} has the following items pending in the pantry modal:")]
    public void GivenItemsPendingInPantryModal(string userName, Table table)
    {
        var measurementNames = table.Rows.Select(r => r["Measurement"]).Distinct();
        using var ctx = BDDSetup.CreateContext();
        foreach (var m in measurementNames)
        {
            if (!ctx.Set<Measurement>().Any(x => x.Name == m))
                ctx.Set<Measurement>().Add(new Measurement { Name = m, Abbreviation = m });
        }
        ctx.SaveChanges();

        var names = string.Join(",", table.Rows.Select(r => r["Name"]));
        var amounts = string.Join(",", table.Rows.Select(r => r["Amount"]));
        var measurements = string.Join(",", table.Rows.Select(r => r["Measurement"]));

        var url = $"{_baseUrl}/Shopping/TestSetupPantryModal" +
                  $"?names={Uri.EscapeDataString(names)}" +
                  $"&amounts={Uri.EscapeDataString(amounts)}" +
                  $"&measurements={Uri.EscapeDataString(measurements)}";

        _driver.Navigate().GoToUrl(url);
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [When("{string} is on the shopping list page")]
    public void WhenUserIsOnShoppingListPage(string userName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("a modal is visible asking whether to add items to the pantry")]
    public void ThenModalIsVisible()
    {
        var modal = _wait.Until(d =>
        {
            try
            {
                var el = d.FindElement(By.Id("addToPantryModal"));
                return el.Displayed ? el : null;
            }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(modal, Is.Not.Null, "Add-to-pantry modal (#addToPantryModal) is not visible");
    }

    [When("{string} clicks confirm on the add-to-pantry modal")]
    public void WhenUserClicksConfirmOnModal(string userName)
    {
        var btn = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("confirmAddToPantry")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(btn, Is.Not.Null, "Confirm button (#confirmAddToPantry) not found");
        btn!.Click();
        _wait.Until(d => d.Url.Contains("/Pantry", StringComparison.OrdinalIgnoreCase));
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("{string} is redirected to the pantry page")]
    public void ThenUserIsRedirectedToPantry(string userName)
    {
        Assert.That(_driver.Url, Does.Contain("/Pantry").IgnoreCase);
    }

    [Then("the pantry page displays {string}")]
    public void ThenPantryPageDisplays(string itemName)
    {
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");

        var found = _wait.Until(d =>
        {
            try
            {
                return d.FindElements(By.CssSelector(".pantry-item-name"))
                    .Any(e => e.Text.Contains(itemName, StringComparison.OrdinalIgnoreCase));
            }
            catch (StaleElementReferenceException) { return false; }
        });
        Assert.That(found, Is.True, $"'{itemName}' not found on pantry page");
    }

    [When("{string} clicks cancel on the add-to-pantry modal")]
    public void WhenUserClicksCancelOnModal(string userName)
    {
        var btn = _wait.Until(d =>
        {
            try { return d.FindElement(By.Id("cancelAddToPantry")); }
            catch (NoSuchElementException) { return null; }
        });
        Assert.That(btn, Is.Not.Null, "Cancel button (#cancelAddToPantry) not found");
        btn!.Click();
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");
    }

    [Then("{string} remains on the shopping list page")]
    public void ThenUserRemainsOnShoppingListPage(string userName)
    {
        Assert.That(_driver.Url, Does.Contain("/Shopping").IgnoreCase);
        Assert.That(_driver.Url, Does.Not.Contain("/Pantry").IgnoreCase);
    }

    [Then("the pantry page does not display {string}")]
    public void ThenPantryPageDoesNotDisplay(string itemName)
    {
        _driver.Navigate().GoToUrl($"{_baseUrl}/Shopping/Pantry");
        _wait.Until(d => ((IJavaScriptExecutor)d)
            .ExecuteScript("return document.readyState").ToString() == "complete");

        var found = _driver.FindElements(By.CssSelector(".pantry-item-name"))
            .Any(e => e.Text.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        Assert.That(found, Is.False, $"'{itemName}' was unexpectedly found on pantry page");
    }
}
