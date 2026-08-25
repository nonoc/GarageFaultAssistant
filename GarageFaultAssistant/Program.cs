using GarageFaultAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var aiSection = builder.Configuration.GetSection("Ai");
var provider = aiSection.GetValue<string?>("Provider") ?? "Fake";

builder.Services.AddSingleton<IFaultAnalyzer>(sp =>
{
    if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
    {
        var apiKey = sp.GetRequiredService<IConfiguration>().GetValue<string>("Ai:OpenAI:ApiKey")
            ?? throw new InvalidOperationException("Ai:OpenAI:ApiKey is not configured.");
        var model = sp.GetRequiredService<IConfiguration>().GetValue<string>("Ai:OpenAI:Model")
            ?? throw new InvalidOperationException("Ai:OpenAI:Model is not configured.");
        return new OpenAiFaultAnalyzer(apiKey, model, sp.GetRequiredService<ILogger<OpenAiFaultAnalyzer>>());
    }

    return new FakeFaultAnalyzer();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
