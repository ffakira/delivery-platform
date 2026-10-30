using Delivery.Api.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Delivery.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigureApplication(
        this WebApplication app)
    {
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
        app.MapRestaurantEndpoints();

        return app;
    }
}
