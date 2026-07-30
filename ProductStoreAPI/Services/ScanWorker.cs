using Microsoft.EntityFrameworkCore;
using ProductStoreAPI.Contracts;
using ProductStoreAPI.Data;
using ProductStoreAPI.Models;

namespace ProductStoreAPI.Services;

public class ScanWorker(ScanQueue scanQueue, IServiceScopeFactory scopes, ILogger<ScanWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var productId in scanQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var scanner = scope.ServiceProvider.GetRequiredService<IProductScanner>();

                var product = await db.Products.FindAsync([productId], stoppingToken);
                if (product is null) continue;

                var images = await db.ProductImages
                    .AsNoTracking()
                    .Where(i => i.ProductId == productId)
                    .Select(i => new ScanImage(i.Content, i.ContentType))
                    .ToListAsync(stoppingToken);

                if (images.Count == 0)
                {
                    product.ScanStatus = ScanStatus.Failed;
                }
                else
                {
                    var suggestion = await scanner.ScanAsync(images, stoppingToken);
                    product.SuggestedName = suggestion.Name;
                    product.SuggestedDescription = suggestion.Description;
                    product.SuggestedCategory = suggestion.Category;
                    product.ScanStatus = ScanStatus.Completed;
                }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;   // app shutting down — leave status Pending for the startup requeue
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Scan failed for product {ProductId}", productId);
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
                .Where(p => p.ScanStatus == ScanStatus.Pending)
                .Select(p => p.Id)
                .ToListAsync(ct);

            foreach (var id in pending)
                await scanQueue.EnqueueAsync(id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not requeue pending scans at startup");
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

            product.ScanStatus = ScanStatus.Failed;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not mark product {ProductId} as failed", productId);
        }
    }
}
