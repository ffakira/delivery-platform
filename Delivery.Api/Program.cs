using System.IO;
using Delivery.Api.Extensions;
using Delivery.Application;
using Delivery.Infrastructure;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;

LoadEnvironmentFile();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.ConfigureApplication();

app.Run();

static void LoadEnvironmentFile()
{
    var rootDirectory = Directory.GetCurrentDirectory();

    while (!string.IsNullOrEmpty(rootDirectory))
    {
        var envPath = Path.Combine(rootDirectory, ".env");

        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            return;
        }

        rootDirectory =
            Directory.GetParent(rootDirectory)?.FullName
            ?? string.Empty;
    }
}

public partial class Program
{
}