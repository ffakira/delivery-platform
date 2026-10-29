using Delivery.Api.Extensions;
using Delivery.Api.Infrastructure.Persistence;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationIdentity();
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

app.Run();
