using System.Security.Cryptography;
using System.Text;
using TcoMvp.Core;
using TcoMvp.Core.Calculation;
using TcoMvp.Core.Model;

var builder = WebApplication.CreateBuilder(args);

// Modelldaten: unkritischer Teil im Repo, sensibler Teil (Leasing, Wartungspreise) nur auf dem Server.
// Der Pfad zur sensiblen Datei kann z.B. per Umgebungsvariable SensitiveModelDataPath auf einen
// Ort außerhalb des Deployments zeigen.
var dataPath = Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration["ModelDataPath"] ?? "Data/model-data.json");
var sensitivePath = builder.Configuration["SensitiveModelDataPath"] is { Length: > 0 } p
    ? Path.Combine(builder.Environment.ContentRootPath, p)
    : null;
var modelData = ModelDataLoader.Load(dataPath, sensitivePath);

// Im Admin-Bereich geänderte Standardannahmen (überschreiben customerDefaults aus model-data.json)
var defaultsPath = Path.Combine(builder.Environment.ContentRootPath,
    builder.Configuration["CustomerDefaultsPath"] ?? "Data/customer-defaults.json");

builder.Services.AddSingleton(modelData);
builder.Services.AddSingleton(new CustomerDefaultsStore(modelData, defaultsPath));
builder.Services.AddSingleton<TcoCalculator>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();

// Security-Header für alle Antworten. Die Seiten kommen ohne Inline-Skripte/-Styles aus,
// daher reicht 'self'; frame-ancestors verhindert das Einbetten in fremde Seiten (Clickjacking).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = "default-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

// Admin-Bereich (/admin, /api/admin): HTTP Basic, Passwort aus Konfiguration "Admin:Password"
// (z.B. Umgebungsvariable Admin__Password). Ohne Passwort ist der Admin-Bereich abgeschaltet.
var adminPassword = app.Configuration["Admin:Password"];
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!path.StartsWithSegments("/admin") && !path.StartsWithSegments("/api/admin"))
    {
        await next();
        return;
    }
    if (string.IsNullOrEmpty(adminPassword))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    if (!IsAuthorized(context.Request, adminPassword))
    {
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"TCO Admin\", charset=\"UTF-8\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// Loks der Kundenansicht + Standardannahmen ("Annahmen zurücksetzen")
api.MapGet("/model", (ModelData data, CustomerDefaultsStore defaults) => CustomerView.Describe(data, defaults.Current));

// Berechnung – liefert nur die Werte des Reiters "Betriebskosten"
api.MapPost("/calculate", (CustomerInputs input, ModelData data, TcoCalculator calculator) =>
{
    var errors = InputValidator.Validate(data, input);
    if (errors.Count > 0) return Results.ValidationProblem(errors, title: "Ungültige Eingaben");

    var result = calculator.Calculate(input);
    return Results.Ok(CustomerView.From(data, result));
});

// Admin: Standardannahmen lesen, speichern, auf model-data.json zurücksetzen
var admin = api.MapGroup("/admin/defaults");
admin.MapGet("", (CustomerDefaultsStore defaults) => DefaultsInfo(defaults));
admin.MapPut("", (CustomerInputs input, CustomerDefaultsStore defaults) =>
{
    var errors = defaults.Save(input);
    return errors.Count > 0
        ? Results.ValidationProblem(errors, title: "Ungültige Standardannahmen")
        : Results.Ok(DefaultsInfo(defaults));
});
admin.MapDelete("", (CustomerDefaultsStore defaults) =>
{
    defaults.Reset();
    return Results.Ok(DefaultsInfo(defaults));
});

app.Run();

static object DefaultsInfo(CustomerDefaultsStore s) => new { current = s.Current, original = s.Original, customized = s.IsCustomized };

static bool IsAuthorized(HttpRequest request, string password)
{
    var header = request.Headers.Authorization.ToString();
    if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;
    string decoded;
    try { decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..].Trim())); }
    catch (FormatException) { return false; }
    var given = decoded[(decoded.IndexOf(':') + 1)..]; // Benutzername wird ignoriert
    return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(password));
}

public partial class Program;
