using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ProductStoreAPI.Data;
using ProductStoreAPI.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddSingleton<ScanQueue>();
builder.Services.AddHostedService<ScanWorker>();

builder.Services.AddHttpClient<IProductScanner, OllamaProductScanner>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<PriceQueue>();
builder.Services.AddHostedService<PriceWorker>();

// Prices come straight from Gumtree SA's search page (ZAR). Browser-like headers are
// required or Gumtree serves a degraded page without prices.
builder.Services.AddHttpClient<IPriceSuggester, GumtreePriceSuggester>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Gumtree:BaseUrl"] ?? "https://www.gumtree.co.za");
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-ZA,en;q=0.9");
});

builder.Services.AddCors(options =>
    options.AddPolicy("AngularDev", policy => 
        policy.WithOrigins(
            "http://localhost:4200",
            "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddControllers()
    .AddJsonOptions(options => 
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AngularDev");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
