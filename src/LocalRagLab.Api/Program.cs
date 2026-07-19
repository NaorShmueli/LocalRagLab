using System.Text.Json.Serialization;
using LocalRagLab.Api.Infrastructure;
using LocalRagLab.Api.Middleware;
using LocalRagLab.Api.Options;
using LocalRagLab.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.WriteIndented = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Local RAG Lab API",
        Version = "v1",
        Description = "A fully local, debuggable RAG learning project using C#, Ollama, local embeddings, an in-memory vector store, and detailed traces."
    });
});

builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.Configure<RagOptions>(
    builder.Configuration.GetSection(RagOptions.SectionName));

builder.Services.AddHttpClient(OllamaApiClient.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

builder.Services.AddSingleton<OllamaApiClient>();
builder.Services.AddSingleton<IEmbeddingClient>(sp => sp.GetRequiredService<OllamaApiClient>());
builder.Services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<OllamaApiClient>());
builder.Services.AddSingleton<IOllamaDiagnostics>(sp => sp.GetRequiredService<OllamaApiClient>());

builder.Services.AddSingleton<ITextExtractor, LocalTextExtractor>();
builder.Services.AddSingleton<ITextChunker, NaturalBoundaryTextChunker>();
builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
builder.Services.AddSingleton<IReranker, HybridDebugReranker>();
builder.Services.AddSingleton<IPromptBuilder, RagPromptBuilder>();
builder.Services.AddSingleton<IRagTraceStore, InMemoryRagTraceStore>();
builder.Services.AddSingleton<IGroundednessEvaluator, OllamaGroundednessEvaluator>();

builder.Services.AddSingleton<DocumentIngestionService>();
builder.Services.AddSingleton<SemanticSearchService>();
builder.Services.AddSingleton<RagQueryService>();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Local RAG Lab API v1");
    options.DocumentTitle = "Local RAG Lab";
    options.DisplayRequestDuration();
});

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
