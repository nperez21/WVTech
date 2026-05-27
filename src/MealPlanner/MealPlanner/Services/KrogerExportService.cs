using MealPlanner.DAL.Abstract;
using MealPlanner.Models;
using MealPlanner.Models.DTO;

namespace MealPlanner.Services;

public class KrogerExportService : IKrogerExportService
{
    private readonly IKrogerExportRepository _exportRepo;
    private readonly ShoppingListService _shoppingListService;
    private readonly IKrogerService? _krogerService;

    public KrogerExportService(
        IKrogerExportRepository exportRepo,
        ShoppingListService shoppingListService,
        IKrogerService? krogerService = null)
    {
        _exportRepo = exportRepo;
        _shoppingListService = shoppingListService;
        _krogerService = krogerService;
    }

    public async Task<KrogerExportResult> RunExportAsync(string userId, string storeId, string krogerToken)
    {
        var items = _shoppingListService.GetItemsForUser(userId).ToList();
        if (items.Count == 0)
            return KrogerExportResult.Of(KrogerExportOutcome.NoItems);

        var searchToken = await _krogerService!.GetClientCredentialsTokenAsync();
        if (searchToken == null)
            return KrogerExportResult.Of(KrogerExportOutcome.SearchTokenFailed);

        var matches = await Task.WhenAll(items.Select(item =>
            _krogerService.SearchProductUpcAsync(item.IngredientBase.Name, storeId, item.Amount, item.Measurement.Name, searchToken)
                .ContinueWith(t => (Name: item.IngredientBase.Name, item.Amount, Measurement: item.Measurement.Name, match: t.Result))));

        var found = matches.Where(r => r.match != null).ToList();
        var skipped = matches.Where(r => r.match == null).Select(r => r.Name).ToList();
        var cartItems = found.Select(r => new KrogerCartItem { Upc = r.match!.Upc, Quantity = r.match.Quantity }).ToList();

        if (cartItems.Count == 0)
            return new KrogerExportResult { Outcome = KrogerExportOutcome.NoMatchesFound, Skipped = skipped };

        var success = await _krogerService.ExportCartAsync(cartItems, krogerToken);
        if (!success)
            return KrogerExportResult.Of(KrogerExportOutcome.ExportFailed);

        await _exportRepo.SaveExportAsync(new KrogerExport
        {
            UserId = userId,
            ExportedAt = DateTime.UtcNow,
            Items = found.Select(r => new KrogerExportItem
            {
                Name = r.Name,
                Amount = r.Amount,
                Measurement = r.Measurement,
                Upc = r.match!.Upc,
                Quantity = r.match.Quantity
            }).ToList()
        });

        return new KrogerExportResult
        {
            Outcome = KrogerExportOutcome.Success,
            ItemsAdded = cartItems.Count,
            Skipped = skipped
        };
    }

    public async Task<IEnumerable<KrogerExportSummaryDto>> GetExportHistoryAsync(string userId)
    {
        var exports = await _exportRepo.GetExportsForUserAsync(userId);
        return exports.Select(e => new KrogerExportSummaryDto
        {
            Id = e.Id,
            ExportedAt = e.ExportedAt,
            ItemCount = e.Items.Count
        });
    }

    public async Task<KrogerExportDetailDto?> GetExportDetailAsync(int exportId, string userId)
    {
        var export = await _exportRepo.GetExportWithItemsAsync(exportId, userId);
        if (export == null) return null;

        return new KrogerExportDetailDto
        {
            Id = export.Id,
            ExportedAt = export.ExportedAt,
            Items = export.Items.Select(i => new KrogerExportItemDto
            {
                Name = i.Name,
                Amount = i.Amount,
                Measurement = i.Measurement
            }).ToList()
        };
    }
}
