using BattleCatCalc.Api.Middleware;
using BattleCatCalc.Core.Interfaces;
using BattleCatCalc.Infrastructure.Fetching;
using BattleCatCalc.Infrastructure.Parsing;
using NLog.Extensions.Logging;

namespace BattleCatCalc.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 日誌（ADR-7）：NLog Provider，程式碼僅依賴 ILogger<T>
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();

        // DI 註冊（ADR-6）
        builder.Services.AddHttpClient<IFetcher, BCDataFetcher>();
        builder.Services.AddSingleton<IParser, BCParser>();
        builder.Services.AddMemoryCache();

        // MVC Controllers
        builder.Services.AddControllers();

        // OpenAPI
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // 全域例外處理 → Problem Details
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // 未來 React build 產出物的靜態檔案
        app.UseStaticFiles();

        // OpenAPI endpoint（開發環境）
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // 健康檢查
        app.MapGet("/api/v1/health", () => Results.Ok(new { status = "healthy" }));

        // Controller 路由
        app.MapControllers();

        app.Run();
    }
}
