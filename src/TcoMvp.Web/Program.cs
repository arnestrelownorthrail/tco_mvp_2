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

builder.Services.AddSingleton(modelData);
builder.Services.AddSingleton<TcoCalculator>();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// Loks der Kundenansicht + Standardannahmen ("Annahmen zurücksetzen")
api.MapGet("/model", (ModelData data) => CustomerView.Describe(data));

// Berechnung – liefert nur die Werte des Reiters "Betriebskosten"
api.MapPost("/calculate", (CustomerInputs input, ModelData data, TcoCalculator calculator) =>
{
    var errors = InputValidator.Validate(data, input);
    if (errors.Count > 0) return Results.ValidationProblem(errors, title: "Ungültige Eingaben");

    var result = calculator.Calculate(input);
    return Results.Ok(CustomerView.From(data, result));
});

app.Run();

public partial class Program;
