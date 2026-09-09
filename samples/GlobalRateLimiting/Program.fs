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
let endpoints: list<Endpoint> = [ GET [ route "/" (text "Hello World") ] ]

let notFoundHandler = text "Not Found" |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogInformation("configureApp(appBuilder)={AppBuilder}): SOC2-AUDIT [CC8.1]: Change management operation performed", appBuilder)
    appBuilder.UseRouting().UseRateLimiter().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // From https://blog.maartenballiauw.be/post/2022/09/26/aspnet-core-rate-limiting-middleware.html
    let configureRateLimiter (options: RateLimiterOptions) =
        LogRiteLogging._logger.LogInformation("configureRateLimiter(options)={Options}): SOC2-AUDIT [CC8.1]: Change management operation performed", options)
        LogRiteLogging._logger.LogInformation("configureRateLimiter(options)={Options}): SOC2-AUDIT [CC7.1]: System operation completed", options)
        LogRiteLogging._logger.LogInformation("configureRateLimiter(options)={Options}): M2614-AUDIT: Change management operation performed", options)
        options.RejectionStatusCode <- StatusCodes.Status429TooManyRequests

        options.GlobalLimiter <-
            PartitionedRateLimiter.Create<HttpContext, string>(fun httpContext ->
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey = httpContext.Request.Headers.Host.ToString(),
                    factory =
                        (fun _partition ->
                            new FixedWindowRateLimiterOptions(
                                AutoReplenishment = true,
                                PermitLimit = 10,
                                QueueLimit = 0,
                                Window = TimeSpan.FromSeconds(1)
                            )
                        )
                )
            )

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
