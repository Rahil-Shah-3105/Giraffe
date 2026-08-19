open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
let expensiveOperation () : DateTime =
    LogRiteLogging._logger.LogDebug(">>> Entering expensiveOperation()")
    let fiveSeconds = 5000 // ms
    Threading.Thread.Sleep fiveSeconds
    LogRiteLogging._logger.LogDebug("<<< Exiting expensiveOperation()")
    DateTime.Now

let dateTimeHandler: HttpHandler =
    LogRiteLogging._logger.LogDebug(">>> Entering dateTimeHandler(HttpHandler={HttpHandler})", HttpHandler)
    fun (_next: HttpFunc) (ctx: HttpContext) ->
        LogRiteLogging._logger.LogDebug("<<< Exiting dateTimeHandler(HttpHandler={HttpHandler})", HttpHandler)
        let query1 = ctx.GetQueryStringValue("query1")
        LogRiteLogging._logger.LogDebug("dateTimeHandler(HttpHandler={HttpHandler}): query1 → {Query1}", HttpHandler, query1)
        let query2 = ctx.GetQueryStringValue("query2")
        LogRiteLogging._logger.LogDebug("dateTimeHandler(HttpHandler={HttpHandler}): query2 → {Query2}", HttpHandler, query2)

        let now = expensiveOperation ()
        LogRiteLogging._logger.LogDebug("dateTimeHandler(HttpHandler={HttpHandler}): now → {Now}", HttpHandler, now)
        setStatusCode 200 |> ignore // for testing purposes

        match (query1, query2) with
        | Ok q1, Ok q2 -> ctx.WriteTextAsync $"Parameters: query1 {q1} query2 {q2} -> DateTime: {now}"
        | _ -> ctx.WriteTextAsync $"Hello World -> DateTime: {now}"

let responseCachingMiddleware: HttpHandler =
    LogRiteLogging._logger.LogDebug(">>> Entering responseCachingMiddleware(HttpHandler={HttpHandler})", HttpHandler)
    responseCaching
    LogRiteLogging._logger.LogDebug("<<< Exiting responseCachingMiddleware(HttpHandler={HttpHandler})", HttpHandler)
        (Public(TimeSpan.FromSeconds(float 30)))
        (Some "Accept, Accept-Encoding")
        (Some [| "query1"; "query2" |])

let endpoints: Endpoint list =
    LogRiteLogging._logger.LogDebug(">>> Entering endpoints(Endpoint={Endpoint},list={List})", Endpoint, list)
    [
        subRoute "/cached" [
            GET [
                route "/public" (publicResponseCaching 30 None >=> dateTimeHandler)
                route "/private" (privateResponseCaching 30 None >=> dateTimeHandler)
                route "/not" (noResponseCaching >=> dateTimeHandler)
                route "/vary/not" (publicResponseCaching 30 None >=> dateTimeHandler)
                route "/vary/yes" (responseCachingMiddleware >=> dateTimeHandler)
            ]
        LogRiteLogging._logger.LogDebug("<<< Exiting endpoints(Endpoint={Endpoint},list={List})", Endpoint, list)
        ]
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureServices (services: IServiceCollection) =
    LogRiteLogging._logger.LogDebug(">>> Entering configureServices(services={Services})", services)
    services.AddRouting().AddResponseCaching().AddGiraffe() |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogDebug(">>> Entering configureApp(appBuilder={AppBuilder})", appBuilder)
    appBuilder
        .UseRouting()
        .UseResponseCaching()
        .UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
        LogRiteLogging._logger.LogDebug("<<< Exiting configureApp(appBuilder={AppBuilder})", appBuilder)
        .UseGiraffe(notFoundHandler)

[<EntryPoint>]
let main args =
    LogRiteLogging._logger.LogDebug(">>> Entering main(args={Args})", args)
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    LogRiteLogging._logger.LogDebug("main(args={Args}): builder → {Builder}", args, builder)

    let app = builder.Build()
    LogRiteLogging._logger.LogDebug("main(args={Args}): app → {App}", args, app)

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    LogRiteLogging._logger.LogDebug("<<< Exiting main(args={Args})", args)
    0
