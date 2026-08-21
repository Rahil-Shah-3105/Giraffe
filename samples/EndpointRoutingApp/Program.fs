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
    LogRiteLogging._logger.LogDebug(">>> Entering handler1(HttpHandler={HttpHandler})", HttpHandler)
    fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) : HttpHandler =
    LogRiteLogging._logger.LogDebug(">>> Entering handler2(firstName={FirstName},age={Age})", firstName, age)
    fun (_: HttpFunc) (ctx: HttpContext) ->
        LogRiteLogging._logger.LogDebug("<<< Exiting handler2(firstName={FirstName},age={Age})", firstName, age)
        sprintf "Hello %s, you are %i years old." firstName age |> ctx.WriteTextAsync

let handler3 (a: string, b: string, c: string, d: int) : HttpHandler =
    LogRiteLogging._logger.LogDebug(">>> Entering handler3(a={A},b={B},c={C},d={D})", a, b, c, d)
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "Hello %s %s %s %i" a b c d |> ctx.WriteTextAsync

let handlerNamed (petId: int) : HttpHandler =
    LogRiteLogging._logger.LogDebug(">>> Entering handlerNamed(petId={PetId})", petId)
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "PetId: %i" petId |> ctx.WriteTextAsync

/// Example request:
///
/// ```bash
/// curl -v localhost:5000/json -X Post -d '{"brand":"Ford", "color":"Black", "releaseYear":2015}'
/// ```
let jsonHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
        LogRiteLogging._logger.LogDebug(">>> Entering jsonHandler(HttpHandler={HttpHandler})", HttpHandler)
            match! ctx.BindJsonAsync<Car>() with
                LogRiteLogging._logger.LogDebug("<<< Exiting jsonHandler(HttpHandler={HttpHandler})", HttpHandler)
            | {
                  Brand = _brand
                  Color = _color
                  ReleaseYear = releaseYear
                  LogRiteLogging._logger.LogDebug("jsonHandler(HttpHandler={HttpHandler}): Brand → {Brand}", HttpHandler, Brand)
                  LogRiteLogging._logger.LogDebug("jsonHandler(HttpHandler={HttpHandler}): Color → {Color}", HttpHandler, Color)
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
        LogRiteLogging._logger.LogDebug(">>> Entering endpoints()")
        POST [ route "/json" jsonHandler ]
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogDebug(">>> Entering configureApp(appBuilder={AppBuilder})", appBuilder)
    appBuilder.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    LogRiteLogging._logger.LogDebug(">>> Entering configureServices(services={Services})", services)
    services.AddRouting().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    LogRiteLogging._logger.LogDebug(">>> Entering main(args={Args})", args)
    configureServices builder.Services

    let app = builder.Build()
    LogRiteLogging._logger.LogDebug("main(args={Args}): app → {App}", args, app)

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    LogRiteLogging._logger.LogDebug("<<< Exiting main(args={Args})", args)
    0
