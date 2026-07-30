using Microsoft.EntityFrameworkCore;
using ProductStoreAPI.Data;
using ProductStoreAPI.Models;

namespace ProductStoreAPI.Services;

public class PriceWorker(PriceQueue priceQueue, IServiceScopeFactory scopes, ILogger<PriceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var productId in priceQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var suggester = scope.ServiceProvider.GetRequiredService<IPriceSuggester>();

                var product = await db.Products.FindAsync([productId], stoppingToken);
                if (product is null) continue;

                // Search on what the scan identified; fall back to the (possibly edited) name.
                var query = FirstNonBlank(product.SuggestedName, product.Name);
                if (query is null)
                {
                    log.LogWarning("Product {ProductId} has nothing to price on", productId);
                    product.PriceStatus = PriceStatus.Failed;
                    await db.SaveChangesAsync(stoppingToken);
                    continue;
                }

                var suggestion = await suggester.SuggestPriceAsync(query, product.SuggestedCategory, stoppingToken);

                await db.PriceComparables
                    .Where(c => c.ProductId == productId)
                    .ExecuteDeleteAsync(stoppingToken);

                foreach (var c in suggestion.Comparables)
                {
                    db.PriceComparables.Add(new PriceComparable
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        Title = c.Title,
                        Price = c.Price,
                        Url = c.Url,
                    });
                }

                product.SuggestedPrice = suggestion.Price;
                product.PriceStatus = PriceStatus.Completed;
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;   // app shutting down — leave status Pending for the startup requeue
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Price suggestion failed for product {ProductId}", productId);
                await MarkFailedAsync(productId);
            }
        }
    }

    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var pending = await db.Products
                .Where(p => p.PriceStatus == PriceStatus.Pending)
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var id in pending)
                await priceQueue.EnqueueAsync(id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not requeue pending price suggestions at startup");
        }
    }

    private async Task MarkFailedAsync(Guid productId)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var product = await db.Products.FindAsync(productId);
            if (product is null) return;

            product.PriceStatus = PriceStatus.Failed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not mark product {ProductId} price as failed", productId);
        }
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
