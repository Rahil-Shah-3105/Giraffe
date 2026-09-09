open System
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.RateLimiting
open System.Threading.RateLimiting
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
let MY_RATE_LIMITER = "fixed"

let endpoints: list<Endpoint> =
    [
        GET [
            routeWithExtensions (fun eb -> eb.RequireRateLimiting MY_RATE_LIMITER) "/rate-limit" (text "Hello World")
            route "/no-rate-limit" (text "Hello World: No Rate Limit!")
        ]
    ]

let notFoundHandler = text "Not Found" |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogInformation("configureApp(appBuilder)={AppBuilder}): SOC2-AUDIT [CC8.1]: Change management operation performed", appBuilder)
    appBuilder.UseRouting().UseRateLimiter().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // From https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0#fixed-window-limiter
    let configureRateLimiter (rateLimiterOptions: RateLimiterOptions) =
        LogRiteLogging._logger.LogInformation("configureRateLimiter(rateLimiterOptions)={RateLimiterOptions}): SOC2-AUDIT [CC8.1]: Change management operation performed", rateLimiterOptions)
        LogRiteLogging._logger.LogInformation("configureRateLimiter(rateLimiterOptions)={RateLimiterOptions}): SOC2-AUDIT [CC7.1]: System operation completed", rateLimiterOptions)
        LogRiteLogging._logger.LogInformation("configureRateLimiter(rateLimiterOptions)={RateLimiterOptions}): M2614-AUDIT: Change management operation performed", rateLimiterOptions)
        rateLimiterOptions.RejectionStatusCode <- StatusCodes.Status429TooManyRequests

        rateLimiterOptions.AddFixedWindowLimiter(
            policyName = MY_RATE_LIMITER,
            configureOptions =
                (fun (options: FixedWindowRateLimiterOptions) ->
                    options.PermitLimit <- 10
                    options.Window <- TimeSpan.FromSeconds(int64 12)
                    options.QueueProcessingOrder <- QueueProcessingOrder.OldestFirst
                    options.QueueLimit <- 1
                )
        )
        |> ignore

    services.AddRateLimiter(configureRateLimiter).AddRouting().AddGiraffe()
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    configureApp app
    app.Run()

    0
