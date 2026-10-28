using LinkShortener.Api.Caching;
using LinkShortener.Api.Endpoints;
using LinkShortener.Application.Links;
using LinkShortener.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("A connection string 'DefaultConnection' is required.");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddDbContext<LinkShortenerDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddScoped<ILinkRepository, LinkRepository>();
builder.Services.AddSingleton<ILinkCache, MemoryLinkCache>();
builder.Services.AddScoped<LinkService>();
builder.Services.AddSingleton<ILinkCodeGenerator, LinkCodeGenerator>();
builder.Services.AddSingleton<IManagementKeyGenerator, ManagementKeyGenerator>();
builder.Services.AddSingleton<ILinkHostResolver, DnsLinkHostResolver>();
builder.Services.AddSingleton<TargetUrlValidator>();

var app = builder.Build();
var shortLinkBaseUrl = builder.Configuration["ShortLinks:BaseUrl"]
    ?? throw new InvalidOperationException("The 'ShortLinks:BaseUrl' setting is required.");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LinkShortenerDbContext>();
    dbContext.Database.Migrate();
}

app.MapLinkEndpoints(shortLinkBaseUrl);
app.Run();

public partial class Program;
