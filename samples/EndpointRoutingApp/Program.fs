open System
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
type Car =
    {
        Brand: string
        Color: string
        ReleaseYear: int
    }

let handler1: HttpHandler =
    fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) : HttpHandler =
    LogRiteLogging._logger.LogError(">>> Entering handler2(firstName=<redacted>,age={Age})", age)
    fun (_: HttpFunc) (ctx: HttpContext) ->
        LogRiteLogging._logger.LogWarning("<<< Exiting handler2(firstName=<redacted>,age={Age})", age)
        sprintf "Hello %s, you are %i years old." firstName age |> ctx.WriteTextAsync

let handler3 (a: string, b: string, c: string, d: int) : HttpHandler =
    LogRiteLogging._logger.LogError(">>> Entering handler3(a={A},b={B},c={C},d={D})", a, b, c, d)
    LogRiteLogging._logger.LogWarning("<<< Exiting handler3(a={A},b={B},c={C},d={D})", a, b, c, d)
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "Hello %s %s %s %i" a b c d |> ctx.WriteTextAsync

let handlerNamed (petId: int) : HttpHandler =
    LogRiteLogging._logger.LogError(">>> Entering handlerNamed(petId={PetId})", petId)
    LogRiteLogging._logger.LogWarning("<<< Exiting handlerNamed(petId={PetId})", petId)
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "PetId: %i" petId |> ctx.WriteTextAsync

/// Example request:
///
/// ```bash
/// curl -v localhost:5000/json -X Post -d '{"brand":"Ford", "color":"Black", "releaseYear":2015}'
/// ```
let jsonHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            match! ctx.BindJsonAsync<Car>() with
            | {
                  Brand = _brand
                  Color = _color
                  ReleaseYear = releaseYear
              } when releaseYear >= 1990 -> return! json {| Message = "Valid car" |} next ctx
            | _ -> return! (setStatusCode 400 >=> json {| Message = "Invalid car year" |}) next ctx
        }

let endpoints =
    [
        subRoute "/foo" [ GET [ route "/bar" (text "Aloha!") ] ]
        GET [
            route "/" (text "Hello World")
            routef "/%s/%i" handler2
            routef "/%s/%s/%s/%i" handler3
            routef "/pet/%i:petId" handlerNamed
        ]
        GET_HEAD [
            route "/foo" (text "Bar")
            route "/x" (text "y")
            route "/abc" (text "def")
            route "/123" (text "456")
        ]
        // Not specifying a http verb means it will listen to all verbs
        subRoute "/sub" [ route "/test" handler1 ]
        POST [ route "/json" jsonHandler ]
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogError(">>> Entering configureApp(appBuilder={AppBuilder})", appBuilder)
    LogRiteLogging._logger.LogWarning("<<< Exiting configureApp(appBuilder={AppBuilder})", appBuilder)
    appBuilder.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    LogRiteLogging._logger.LogError(">>> Entering configureServices(services={Services})", services)
    LogRiteLogging._logger.LogWarning("<<< Exiting configureServices(services={Services})", services)
    services.AddRouting().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    LogRiteLogging._logger.LogError(">>> Entering main(args={Args})", args)
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    LogRiteLogging._logger.LogError("main(args={Args}): builder → {Builder}", args, builder)

    let app = builder.Build()
    LogRiteLogging._logger.LogError("main(args={Args}): app → {App}", args, app)

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    LogRiteLogging._logger.LogWarning("<<< Exiting main(args={Args})", args)
    0
