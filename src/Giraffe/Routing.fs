namespace Giraffe
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
[<RequireQualifiedAccess>]
module SubRouting =
    open Microsoft.AspNetCore.Http

    [<Literal>]
    let private RouteKey = "giraffe_route"

    let getSavedPartialPath (ctx: HttpContext) =
        LogRiteLogging._logger.LogError(">>> Entering getSavedPartialPath(ctx={Ctx})", ctx)
        LogRiteLogging._logger.LogWarning("<<< Exiting getSavedPartialPath(ctx={Ctx})", ctx)
        match ctx.Items.TryGetValue RouteKey with
        | true, route -> route |> string |> strOption
        | false, _ -> None

    let getNextPartOfPath (ctx: HttpContext) =
        LogRiteLogging._logger.LogError(">>> Entering getNextPartOfPath(ctx={Ctx})", ctx)
        LogRiteLogging._logger.LogWarning("<<< Exiting getNextPartOfPath(ctx={Ctx})", ctx)
        match getSavedPartialPath ctx with
        | Some p when ctx.Request.Path.Value.Contains p -> ctx.Request.Path.Value.[p.Length ..]
        | _ -> ctx.Request.Path.Value

    let routeWithPartialPath (path: string) (handler: HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeWithPartialPath(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting routeWithPartialPath(path={Path})", path)
            task {
                let savedPartialPath = getSavedPartialPath ctx
                ctx.Items.Item RouteKey <- ((savedPartialPath |> Option.defaultValue "") + path)
                let! result = handler next ctx

                match result with
                | Some _ -> ()
                | None ->
                    match savedPartialPath with
                    | Some subPath -> ctx.Items.Item RouteKey <- subPath
                    | None -> ctx.Items.Remove RouteKey |> ignore

                return result
            }

[<AutoOpen>]
module Routing =
    open System
    open System.Collections.Generic
    open System.Text.RegularExpressions
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Primitives
    open Giraffe.FormatExpressions

    /// <summary>
    /// Filters an incoming HTTP request based on the port.
    /// </summary>
    /// <param name="fns">List of port to <see cref="HttpHandler"/> mappings</param>
    /// <param name="next"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routePorts (fns: (int * HttpHandler) list) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routePorts(fns={Fns})", fns)
        fun next ->
            LogRiteLogging._logger.LogWarning("<<< Exiting routePorts(fns={Fns})", fns)
            let portMap = Dictionary<_, _>(fns.Length)
            LogRiteLogging._logger.LogError("routePorts(fns={Fns}): portMap → {PortMap}", fns, portMap)
            fns |> List.iter (fun (p, h) -> portMap.Add(p, h next))

            fun (ctx: HttpContext) ->
                let port = ctx.Request.Host.Port

                if port.HasValue then
                    match portMap.TryGetValue port.Value with
                    | true, func -> func ctx
                    | false, _ -> skipPipeline
                else
                    skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path (case sensitive).
    /// </summary>
    /// <param name="path">Request path.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let route (path: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering route(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting route(path={Path})", path)
            if (SubRouting.getNextPartOfPath ctx).Equals path then
                next ctx
            else
                skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path (case insensitive).
    /// </summary>
    /// <param name="path">Request path.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeCi (path: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeCi(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting routeCi(path={Path})", path)
            if String.Equals(SubRouting.getNextPartOfPath ctx, path, StringComparison.OrdinalIgnoreCase) then
                next ctx
            else
                skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
    /// </summary>
    /// <param name="path">Regex path.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routex (path: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routex(path={Path})", path)
        let pattern = sprintf "^%s$" path
        let regex = Regex(pattern, RegexOptions.Compiled)
        LogRiteLogging._logger.LogError("routex(path={Path}): regex → {Regex}", path, regex)
        LogRiteLogging._logger.LogError("routex(path={Path}): pattern → {Pattern}", path, pattern)

        LogRiteLogging._logger.LogWarning("<<< Exiting routex(path={Path})", path)
        fun (next: HttpFunc) (ctx: Microsoft.AspNetCore.Http.HttpContext) ->
            let result = regex.Match(SubRouting.getNextPartOfPath ctx)
            LogRiteLogging._logger.LogError("routex(path={Path}): result → {Result}", path, result)

            match result.Success with
            | true -> next ctx
            | false -> skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path using Regex (case sensitive).
    ///
    /// If the route matches the incoming HTTP request then the Regex groups will be passed into the supplied `routeHandler`.
    ///
    /// This is similar to routex but also allows to use matched strings as parameters for a controller.
    /// </summary>
    /// <param name="path">Regex path.</param>
    /// <param name="routeHandler">A function which accepts a string sequence of the matched groups and returns a `HttpHandler` function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routexp (path: string) (routeHandler: seq<string> -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routexp(path={Path})", path)
        let pattern = sprintf "^%s$" path
        let regex = Regex(pattern, RegexOptions.Compiled)
        LogRiteLogging._logger.LogError("routexp(path={Path}): regex → {Regex}", path, regex)
        LogRiteLogging._logger.LogError("routexp(path={Path}): pattern → {Pattern}", path, pattern)

        LogRiteLogging._logger.LogWarning("<<< Exiting routexp(path={Path})", path)
        fun (next: HttpFunc) (ctx: Microsoft.AspNetCore.Http.HttpContext) ->
            let result = regex.Match(SubRouting.getNextPartOfPath ctx)
            LogRiteLogging._logger.LogError("routexp(path={Path}): result → {Result}", path, result)

            match result.Success with
            | true ->
                let args = result.Groups |> Seq.map (fun x -> x.Value)
                LogRiteLogging._logger.LogError("routexp(path={Path}): args → {Args}", path, args)
                routeHandler args next ctx
            | false -> skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path using Regex (case insensitive).
    /// </summary>
    /// <param name="path">Regex path.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeCix (path: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeCix(path={Path})", path)
        let pattern = sprintf "^%s$" path
        let regex = Regex(pattern, RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
        LogRiteLogging._logger.LogError("routeCix(path={Path}): regex → {Regex}", path, regex)
        LogRiteLogging._logger.LogError("routeCix(path={Path}): pattern → {Pattern}", path, pattern)

        LogRiteLogging._logger.LogWarning("<<< Exiting routeCix(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let result = regex.Match(SubRouting.getNextPartOfPath ctx)
            LogRiteLogging._logger.LogError("routeCix(path={Path}): result → {Result}", path, result)

            match result.Success with
            | true -> next ctx
            | false -> skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the request path (case sensitive).
    /// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
    ///
    /// Supported format chars**
    ///
    /// %b: bool
    /// %c: char
    /// %s: string
    /// %i: int
    /// %d: int64
    /// %f: float/double
    /// %O: Guid
    /// </summary>
    /// <param name="path">A format string representing the expected request path.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routef (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routef(path={Path})", path)
        validateFormat path

        LogRiteLogging._logger.LogWarning("<<< Exiting routef(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            tryMatchInput path MatchOptions.Exact (SubRouting.getNextPartOfPath ctx)
            |> function
                | None -> skipPipeline
                | Some args -> routeHandler args next ctx

    /// <summary>
    /// Filters an incoming HTTP request based on the request path.
    /// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
    ///
    /// Supported format chars**
    ///
    /// %b: bool
    /// %c: char
    /// %s: string
    /// %i: int
    /// %d: int64
    /// %f: float/double
    /// %O: Guid
    /// </summary>
    /// <param name="path">A format string representing the expected request path.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeCif (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeCif(path={Path})", path)
        validateFormat path

        LogRiteLogging._logger.LogWarning("<<< Exiting routeCif(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            tryMatchInput path MatchOptions.IgnoreCaseExact (SubRouting.getNextPartOfPath ctx)
            |> function
                | None -> skipPipeline
                | Some args -> routeHandler args next ctx

    /// <summary>
    /// Filters an incoming HTTP request based on the request path (case insensitive).
    /// If the route matches the incoming HTTP request then the parameters from the string will be used to create an instance of 'T and subsequently passed into the supplied routeHandler.
    /// </summary>
    /// <param name="route">A string representing the expected request path. Use {propertyName} for reserved parameter names which should map to the properties of type 'T. You can also use valid Regex within the route string.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed parameters and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <typeparam name="'T"></typeparam>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeBind<'T> (route: string) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeBind(route={Route},routeHandler={RouteHandler})", route, routeHandler)
        let pattern = route.Replace("{", "(?<").Replace("}", ">[^/\n]+)") |> sprintf "^%s$"
        let regex = Regex(pattern, RegexOptions.IgnoreCase)
        LogRiteLogging._logger.LogError("routeBind(route={Route},routeHandler={RouteHandler}): regex → {Regex}", route, routeHandler, regex)
        LogRiteLogging._logger.LogError("routeBind(route={Route},routeHandler={RouteHandler}): pattern → {Pattern}", route, routeHandler, pattern)

        LogRiteLogging._logger.LogWarning("<<< Exiting routeBind(route={Route},routeHandler={RouteHandler})", route, routeHandler)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let result = regex.Match(SubRouting.getNextPartOfPath ctx)
            LogRiteLogging._logger.LogError("routeBind(route={Route},routeHandler={RouteHandler}): result → {Result}", route, routeHandler, result)

            match result.Success with
            | true ->
                let groups = result.Groups

                let result =
                    regex.GetGroupNames()
                    |> Array.skip 1
                    |> Array.map (fun n -> n, StringValues groups.[n].Value)
                    |> dict
                    |> ModelParser.tryParse None

                match result with
                | Error _ -> skipPipeline
                | Ok model -> routeHandler model next ctx
            | _ -> skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
    /// </summary>
    /// <param name="subPath">The expected beginning of a request path.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeStartsWith (subPath: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeStartsWith(subPath={SubPath})", subPath)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting routeStartsWith(subPath={SubPath})", subPath)
            if (SubRouting.getNextPartOfPath ctx).StartsWith subPath then
                next ctx
            else
                skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
    /// </summary>
    /// <param name="subPath">The expected beginning of a request path.</param>
    /// <param name="next"></param>
    /// <param name="ctx"></param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeStartsWithCi (subPath: string) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeStartsWithCi(subPath={SubPath})", subPath)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting routeStartsWithCi(subPath={SubPath})", subPath)
            if (SubRouting.getNextPartOfPath ctx).StartsWith(subPath, StringComparison.OrdinalIgnoreCase) then
                next ctx
            else
                skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on the beginning of the request path (case sensitive).
    /// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
    ///
    /// Supported format chars**
    ///
    /// %b: bool
    /// %c: char
    /// %s: string
    /// %i: int
    /// %d: int64
    /// %f: float/double
    /// %O: Guid
    /// </summary>
    /// <param name="path">A format string representing the expected request path.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeStartsWithf (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeStartsWithf(path={Path})", path)
        validateFormat path

        let options =
            {
                MatchOptions.IgnoreCase = false
                MatchMode = StartsWith
            LogRiteLogging._logger.LogWarning("<<< Exiting routeStartsWithf(path={Path})", path)
            LogRiteLogging._logger.LogDebug("routeStartsWithf(path={Path}): MatchOptions.IgnoreCase → {Arg1}", path, MatchOptions.IgnoreCase)
            }

        fun (next: HttpFunc) (ctx: HttpContext) ->
            tryMatchInput path options (SubRouting.getNextPartOfPath ctx)
            |> function
                | None -> skipPipeline
                | Some args -> routeHandler args next ctx

    /// <summary>
    /// Filters an incoming HTTP request based on the beginning of the request path (case insensitive).
    /// If the route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
    ///
    /// Supported format chars**
    ///
    /// %b: bool
    /// %c: char
    /// %s: string
    /// %i: int
    /// %d: int64
    /// %f: float/double
    /// %O: Guid
    /// </summary>
    /// <param name="path">A format string representing the expected request path.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let routeStartsWithCif (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering routeStartsWithCif(path={Path})", path)
        validateFormat path

        let options =
            {
                MatchOptions.IgnoreCase = true
                LogRiteLogging._logger.LogDebug("routeStartsWithCif(path={Path}): MatchOptions.IgnoreCase → {Arg1}", path, MatchOptions.IgnoreCase)
                LogRiteLogging._logger.LogWarning("<<< Exiting routeStartsWithCif(path={Path})", path)
                MatchMode = StartsWith
            }

        fun (next: HttpFunc) (ctx: HttpContext) ->
            tryMatchInput path options (SubRouting.getNextPartOfPath ctx)
            |> function
                | None -> skipPipeline
                | Some args -> routeHandler args next ctx

    /// <summary>
    /// Filters an incoming HTTP request based on a part of the request path (case sensitive).
    /// Subsequent route handlers inside the given handler function should omit the already validated path.
    /// </summary>
    /// <param name="path">A part of an expected request path.</param>
    /// <param name="handler">A Giraffe <see cref="HttpHandler"/> function.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let subRoute (path: string) (handler: HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering subRoute(path={Path})", path)
        routeStartsWith path >=> SubRouting.routeWithPartialPath path handler

    /// <summary>
    /// Filters an incoming HTTP request based on a part of the request path (case insensitive).
    /// Subsequent route handlers inside the given handler function should omit the already validated path.
    /// </summary>
    /// <param name="path">A part of an expected request path.</param>
    /// <param name="handler">A Giraffe <see cref="HttpHandler"/> function.</param>
    /// <param name="next">The next HttpFunc in the Giraffe pipeline.</param>
    /// <param name="ctx">The current http context object.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let subRouteCi (path: string) (handler: HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering subRouteCi(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting subRouteCi(path={Path})", path)
            let nextPartOfPath = SubRouting.getNextPartOfPath ctx

            if nextPartOfPath.StartsWith(path, StringComparison.OrdinalIgnoreCase) then
                let matchedPathFragment = nextPartOfPath.[0 .. path.Length - 1]
                SubRouting.routeWithPartialPath matchedPathFragment handler next ctx
            else
                skipPipeline

    /// <summary>
    /// Filters an incoming HTTP request based on a part of the request path (case sensitive).
    /// If the sub route matches the incoming HTTP request then the arguments from the <see cref="Microsoft.FSharp.Core.PrintfFormat"/> will be automatically resolved and passed into the supplied routeHandler.
    ///
    /// Supported format chars
    ///
    /// %b: bool
    /// %c: char
    /// %s: string
    /// %i: int
    /// %d: int64
    /// %f: float/double
    /// %O: Guid
    ///
    /// Subsequent routing handlers inside the given handler function should omit the already validated path.
    /// </summary>
    /// <param name="path">A format string representing the expected request sub path.</param>
    /// <param name="routeHandler">A function which accepts a tuple 'T of the parsed arguments and returns a <see cref="HttpHandler"/> function which will subsequently deal with the request.</param>
    /// <returns>A Giraffe <see cref="HttpHandler"/> function which can be composed into a bigger web application.</returns>
    let subRoutef (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : HttpHandler =
        LogRiteLogging._logger.LogError(">>> Entering subRoutef(path={Path})", path)
        validateFormat path

        LogRiteLogging._logger.LogWarning("<<< Exiting subRoutef(path={Path})", path)
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let paramCount = (path.Value.Split '/').Length
            LogRiteLogging._logger.LogError("subRoutef(path={Path}): paramCount → {ParamCount}", path, paramCount)
            let subPathParts = (SubRouting.getNextPartOfPath ctx).Split '/'
            LogRiteLogging._logger.LogError("subRoutef(path={Path}): subPathParts → {SubPathParts}", path, subPathParts)

            if paramCount > subPathParts.Length then
                skipPipeline
            else
                let subPath =
                    subPathParts
                    |> Array.take paramCount
                    |> Array.fold
                        (fun state elem ->
                            if String.IsNullOrEmpty elem then
                                state
                            else
                                sprintf "%s/%s" state elem
                        )
                        ""

                tryMatchInput path MatchOptions.Exact subPath
                |> function
                    | None -> skipPipeline
                    | Some args -> SubRouting.routeWithPartialPath subPath (routeHandler args) next ctx
