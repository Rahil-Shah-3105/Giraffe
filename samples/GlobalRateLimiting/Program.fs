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
    LogRiteLogging._logger.LogWarning("ENTERING: configureApp() method")
    appBuilder.UseRouting().UseRateLimiter().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    // From https://blog.maartenballiauw.be/post/2022/09/26/aspnet-core-rate-limiting-middleware.html
    let configureRateLimiter (options: RateLimiterOptions) =
        LogRiteLogging._logger.LogWarning("ENTERING: configureRateLimiter() method")
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
    LogRiteLogging._logger.LogInformation("ENTERING: main() method")
    configureServices builder.Services

    let app = builder.Build()

    configureApp app
    app.Run()

    0
