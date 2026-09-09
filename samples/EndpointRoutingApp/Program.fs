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
    LogRiteLogging._logger.LogWarning("ENTERING: handler1() method")
    fun (_: HttpFunc) (ctx: HttpContext) -> ctx.WriteTextAsync "Hello World"

let handler2 (firstName: string, age: int) : HttpHandler =
    LogRiteLogging._logger.LogWarning("ENTERING: handler2() method")
    fun (_: HttpFunc) (ctx: HttpContext) ->
        sprintf "Hello %s, you are %i years old." firstName age |> ctx.WriteTextAsync

let handler3 (a: string, b: string, c: string, d: int) : HttpHandler =
    LogRiteLogging._logger.LogWarning("ENTERING: handler3() method")
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "Hello %s %s %s %i" a b c d |> ctx.WriteTextAsync

let handlerNamed (petId: int) : HttpHandler =
    LogRiteLogging._logger.LogWarning("ENTERING: handlerNamed() method")
    fun (_: HttpFunc) (ctx: HttpContext) -> sprintf "PetId: %i" petId |> ctx.WriteTextAsync

/// Example request:
///
/// ```bash
/// curl -v localhost:5000/json -X Post -d '{"brand":"Ford", "color":"Black", "releaseYear":2015}'
/// ```
let jsonHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
        LogRiteLogging._logger.LogInformation("ENTERING: jsonHandler() method")
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
    LogRiteLogging._logger.LogInformation("ENTERING: endpoints() method")
    ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogWarning("ENTERING: configureApp() method")
    appBuilder.UseRouting().UseGiraffe(endpoints).UseGiraffe(notFoundHandler)

let configureServices (services: IServiceCollection) =
    LogRiteLogging._logger.LogWarning("ENTERING: configureServices() method")
    services.AddRouting().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    LogRiteLogging._logger.LogInformation("ENTERING: main() method")
    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    0
