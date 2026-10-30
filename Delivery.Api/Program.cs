using System;
using System.IO;
using Delivery.Api.Endpoints;
using Delivery.Api.Extensions;
using Delivery.Api.Infrastructure.Persistence;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var rootDirectory = Directory.GetCurrentDirectory();

while (!string.IsNullOrEmpty(rootDirectory) && !File.Exists(Path.Combine(rootDirectory, ".env")))
{
    rootDirectory = Directory.GetParent(rootDirectory)?.FullName ?? string.Empty;
}

if (string.IsNullOrEmpty(rootDirectory))
{
    throw new InvalidOperationException("Could not locate the project-root .env file.");
}

Env.Load(Path.Combine(rootDirectory, ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationIdentity();
builder.Services.AddAuthorization();
builder.Services.AddHosting(builder.Configuration);

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
