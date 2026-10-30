using System;
using System.IO;
using Delivery.Api.Endpoints;
using Delivery.Api.Extensions;
using Delivery.Application.Restaurants.Services;
using Delivery.Infrastructure.Persistence;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var rootDirectory = Directory.GetCurrentDirectory();

while (!string.IsNullOrEmpty(rootDirectory))
{
    var envPath = Path.Combine(rootDirectory, ".env");

    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        break;
    }

    rootDirectory = Directory.GetParent(rootDirectory)?.FullName ?? string.Empty;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationIdentity();
builder.Services.AddAuthorization();
builder.Services.AddHosting(builder.Configuration);

// application services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateRestaurantService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(HostingServiceExtensions.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();

public partial class Program
{
}
