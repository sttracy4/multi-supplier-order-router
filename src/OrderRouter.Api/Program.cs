using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderRouter.Api.Data;
using OrderRouter.Api.Data.Seeding;
using OrderRouter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(o =>
    {
        // Disable the [ApiController] automatic 400 response so the route endpoint
        // can handle validation errors itself and always return 200 with feasible:false.
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
{
    o.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IOrderRoutingService, OrderRoutingService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Multi-Supplier Order Router", Version = "v1" });
});

var app = builder.Build();

// Run migrations and seed data before accepting requests
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Skip relational migrations when using InMemory provider (e.g. integration tests)
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    var resourcesPath = app.Configuration["ResourcesPath"]
        ?? Path.Combine(app.Environment.ContentRootPath, "resources");

    if (Directory.Exists(resourcesPath))
        await DatabaseSeeder.SeedAsync(db, resourcesPath, logger);
    else
        logger.LogWarning("Resources path '{Path}' not found — skipping seed.", resourcesPath);
}

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        // /api/route always returns 200; all other paths return 500
        if (context.Request.Path.StartsWithSegments("/api/route"))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                feasible = false,
                infeasibility_reason = "An internal error occurred while processing the order.",
                routing = Array.Empty<object>(),
            });
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "An unexpected error occurred.",
                status = 500,
            });
        }
    });
});

app.UseDefaultFiles();   // serves index.html at /
app.UseStaticFiles();    // serves wwwroot/

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Router v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();

public partial class Program { }
