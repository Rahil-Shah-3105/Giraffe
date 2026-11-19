namespace Giraffe.EndpointRouting

open System
open System.Net
open System.Threading.Tasks
open System.Runtime.CompilerServices
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open Microsoft.FSharp.Reflection
open FSharp.Core
open Giraffe
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
module RouteTemplateBuilder =
    // We use this regex route constraint to be compatible with Giraffe's default router,
    // which supports ShortGuid's.
    // More information on ASP.NET route constraints:
    // https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-8.0#route-constraints
    let private guidPattern =
        "^[0-9A-Fa-f]{{8}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{4}}-[0-9A-Fa-f]{{12}}$|^[0-9A-Fa-f]{{32}}$|^[-_0-9A-Za-z]{{22}}$"

    let private shortIdPattern = "([-_0-9A-Za-z]{{10}}[048AEIMQUYcgkosw])"

    let private getConstraint (i: int) (c: char) (name: string option) =
        LogRiteLogging._logger.LogError(">>> Entering getConstraint(i={I})", i)
        let name = Option.defaultValue (sprintf "%c%i" c i) name
        LogRiteLogging._logger.LogError("getConstraint(i={I}): name → {Name}", i, name)

        LogRiteLogging._logger.LogWarning("<<< Exiting getConstraint(i={I})", i)
        match c with
        | 'b' -> name, sprintf "{%s:bool}" name // bool
        | 'c' -> name, sprintf "{%s:length(1)}" name // char
        | 's' -> name, sprintf "{%s}" name // string
        | 'i' -> name, sprintf "{%s:int}" name // int
        | 'd' -> name, sprintf "{%s:long}" name // int64
        | 'f' -> name, sprintf "{%s:double}" name // float
        | 'O' -> name, sprintf "{%s:regex(%s)}" name guidPattern // Guid
        | 'u' -> name, sprintf "{%s:regex(%s)}" name shortIdPattern // uint64
        | _ -> failwithf "%c is not a supported route format character." c

    let convertToRouteTemplate (path: PrintfFormat<_, _, _, _, 'T>) =
        LogRiteLogging._logger.LogError(">>> Entering convertToRouteTemplate(path={Path})", path)
        let rec convert (i: int) (chars: char list) =
            LogRiteLogging._logger.LogError(">>> Entering convert(i={I})", i)
            match chars with
            | '%' :: '%' :: tail ->
                LogRiteLogging._logger.LogWarning("<<< Exiting convert(i={I})", i)
                let template, mappings = convert i tail
                "%" + template, mappings
            | '%' :: c :: tail ->
                match tail with
                | ':' :: stail ->
                    let splitIndex = stail |> List.tryFindIndex (fun c -> c = '/')
                    LogRiteLogging._logger.LogError("convert(i={I}): splitIndex → {SplitIndex}", i, splitIndex)

                    match splitIndex with
                    | Some splitIndex ->
                        let name, newTail = stail |> List.splitAt splitIndex

                        let placeholderName, placeholderTemplate =
                            getConstraint i c (Some(System.String.Concat(Array.ofList (name))))

                        let template, mappings = convert (i + 1) newTail
                        placeholderTemplate + template, (placeholderName, c) :: mappings
                    | None ->
                        let placeholderName, placeholderTemplate =
                            getConstraint i c (Some(System.String.Concat(Array.ofList (stail))))

                        let template, mappings = convert (i + 1) []
                        placeholderTemplate + template, (placeholderName, c) :: mappings
                | _ ->
                    let placeholderName, placeholderTemplate = getConstraint i c None
                    let template, mappings = convert (i + 1) tail
                    placeholderTemplate + template, (placeholderName, c) :: mappings
            | c :: tail ->
                let template, mappings = convert i tail
                c.ToString() + template, mappings
            | [] -> "", []

        path.Value |> List.ofSeq |> convert 0

module private RequestDelegateBuilder =

    let private tryGetParser (c: char) =
        LogRiteLogging._logger.LogError(">>> Entering tryGetParser(c={C})", c)
        let decodeSlashes (s: string) =
            LogRiteLogging._logger.LogError(">>> Entering decodeSlashes(s={S})", s)
            LogRiteLogging._logger.LogWarning("<<< Exiting decodeSlashes(s={S})", s)
            s.Replace("%2F", "/").Replace("%2f", "/")

        let parseGuid (s: string) =
            LogRiteLogging._logger.LogError(">>> Entering parseGuid(s={S})", s)
            LogRiteLogging._logger.LogWarning("<<< Exiting parseGuid(s={S})", s)
            match s.Length with
            | 22 -> ShortGuid.toGuid s
            | _ -> Guid s

        match c with
        | 's' -> Some(decodeSlashes >> box)
        | 'i' -> Some(int >> box)
        | 'b' -> Some(bool.Parse >> box)
        | 'c' -> Some(char >> box)
        | 'd' -> Some(int64 >> box)
        | 'f' -> Some(float >> box)
        | 'O' -> Some(parseGuid >> box)
        | 'u' -> Some(ShortId.toUInt64 >> box)
        | _ -> None

    let private convertToTuple (mappings: (string * char) list) (routeData: RouteData) =
        LogRiteLogging._logger.LogError(">>> Entering convertToTuple(mappings={Mappings})", mappings)
        let values =
            mappings
            |> List.map (fun (placeholderName, formatChar) ->
                let routeValue = routeData.Values.[placeholderName]

                match tryGetParser formatChar with
                | Some parseFn -> parseFn (routeValue.ToString())
                | None -> routeValue
            )
            |> List.toArray

        let result =
            match values.Length with
            | 1 -> values.[0]
            | _ ->
                let types = values |> Array.map (fun v -> v.GetType())
                LogRiteLogging._logger.LogError("convertToTuple(mappings={Mappings}): types → {Types}", mappings, types)
                let tupleType = FSharpType.MakeTupleType types
                LogRiteLogging._logger.LogWarning("<<< Exiting convertToTuple(mappings={Mappings})", mappings)
                FSharpValue.MakeTuple(values, tupleType)

        result

    let private wrapDelegate f = new RequestDelegate(f)

    let private handleResult (result: HttpContext option) (ctx: HttpContext) =
        LogRiteLogging._logger.LogError(">>> Entering handleResult(result={Result})", result)
        LogRiteLogging._logger.LogWarning("<<< Exiting handleResult(result={Result})", result)
        match result with
        | None -> ctx.SetStatusCode(int HttpStatusCode.UnprocessableEntity)
        | Some _ -> ()

    let createRequestDelegate (handler: HttpHandler) =
        LogRiteLogging._logger.LogError(">>> Entering createRequestDelegate(handler={Handler})", handler)
        let func: HttpFunc = handler earlyReturn

        LogRiteLogging._logger.LogWarning("<<< Exiting createRequestDelegate(handler={Handler})", handler)
        fun (ctx: HttpContext) ->
            task {
                let! result = func ctx
                return handleResult result ctx
            }
            :> Task
        |> wrapDelegate

    let createTokenizedRequestDelegate (mappings: (string * char) list) (tokenizedHandler: 'T -> HttpHandler) =
        LogRiteLogging._logger.LogError(">>> Entering createTokenizedRequestDelegate(mappings={Mappings})", mappings)
        fun (ctx: HttpContext) ->
            task {
                let tuple = ctx.GetRouteData() |> convertToTuple mappings :?> 'T
                let! result = tokenizedHandler tuple earlyReturn ctx
                return handleResult result
            }
            LogRiteLogging._logger.LogError("createTokenizedRequestDelegate(mappings={Mappings}): tuple → {Tuple}", mappings, tuple)
            LogRiteLogging._logger.LogWarning("<<< Exiting createTokenizedRequestDelegate(mappings={Mappings})", mappings)
            :> Task
        |> wrapDelegate

// ---------------------------
// Overriding Router Handlers
// ---------------------------

[<AutoOpen>]
module Routers =
    open System.Collections.Generic
    open Microsoft.Extensions.Primitives

    type HttpVerb =
        | GET
        | POST
        | PUT
        | PATCH
        | QUERY
        | DELETE
        | HEAD
        | OPTIONS
        | TRACE
        | CONNECT
        | NotSpecified

        override this.ToString() =
            match this with
            | GET -> "GET"
            | POST -> "POST"
            | PUT -> "PUT"
            | PATCH -> "PATCH"
            | QUERY -> "QUERY"
            | DELETE -> "DELETE"
            | HEAD -> "HEAD"
            | OPTIONS -> "OPTIONS"
            | TRACE -> "TRACE"
            | CONNECT -> "CONNECT"
            | _ -> ""

    type RouteTemplate = string
    type RouteTemplateMappings = list<string * char>
    type ConfigureEndpoint = IEndpointConventionBuilder -> IEndpointConventionBuilder

    type Endpoint =
        | SimpleEndpoint of HttpVerb * RouteTemplate * HttpHandler * ConfigureEndpoint
        | TemplateEndpoint of
            HttpVerb *
            RouteTemplate *
            RouteTemplateMappings *
            (obj -> HttpHandler) *
            ConfigureEndpoint
        | NestedEndpoint of RouteTemplate * Endpoint list * ConfigureEndpoint
        | MultiEndpoint of Endpoint list

    let rec private applyHttpVerbToEndpoint (verb: HttpVerb) (endpoint: Endpoint) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering applyHttpVerbToEndpoint(verb={Verb})", verb)
        match endpoint with
        | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
            LogRiteLogging._logger.LogWarning("<<< Exiting applyHttpVerbToEndpoint(verb={Verb})", verb)
            SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata)
        | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
            TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata)
        | NestedEndpoint(routeTemplate, endpoints, metadata) ->
            NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
        | MultiEndpoint endpoints -> endpoints |> List.map (applyHttpVerbToEndpoint verb) |> MultiEndpoint

    let rec private applyHttpVerbToEndpoints (verb: HttpVerb) (endpoints: Endpoint list) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering applyHttpVerbToEndpoints(verb={Verb})", verb)
        endpoints
        |> List.map (fun endpoint ->
            LogRiteLogging._logger.LogWarning("<<< Exiting applyHttpVerbToEndpoints(verb={Verb})", verb)
            match endpoint with
            | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
                SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata)
            | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
                TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata)
            | NestedEndpoint(routeTemplate, endpoints, metadata) ->
                NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
            | MultiEndpoint endpoints -> applyHttpVerbToEndpoints verb endpoints
        )
        |> MultiEndpoint

    let rec private applyHttpVerbsToEndpoints (verbs: HttpVerb list) (endpoints: Endpoint list) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering applyHttpVerbsToEndpoints(verbs={Verbs})", verbs)
        endpoints
        |> List.map (fun endpoint ->
            LogRiteLogging._logger.LogWarning("<<< Exiting applyHttpVerbsToEndpoints(verbs={Verbs})", verbs)
            match endpoint with
            | SimpleEndpoint(_, routeTemplate, requestDelegate, metadata) ->
                verbs
                |> List.map (fun verb -> SimpleEndpoint(verb, routeTemplate, requestDelegate, metadata))
                |> MultiEndpoint
            | TemplateEndpoint(_, routeTemplate, mappings, requestDelegate, metadata) ->
                verbs
                |> List.map (fun verb -> TemplateEndpoint(verb, routeTemplate, mappings, requestDelegate, metadata))
                |> MultiEndpoint
            | NestedEndpoint(routeTemplate, endpoints, metadata) ->
                verbs
                |> List.map (fun verb ->
                    NestedEndpoint(routeTemplate, endpoints |> List.map (applyHttpVerbToEndpoint verb), metadata)
                )
                |> MultiEndpoint
            | MultiEndpoint endpoints ->
                verbs
                |> List.map (fun verb -> applyHttpVerbToEndpoints verb endpoints)
                |> MultiEndpoint
        )
        |> MultiEndpoint

    let GET_HEAD = applyHttpVerbsToEndpoints [ GET; HEAD ]

    let GET = applyHttpVerbToEndpoints GET
    let POST = applyHttpVerbToEndpoints POST
    let PUT = applyHttpVerbToEndpoints PUT
    let PATCH = applyHttpVerbToEndpoints PATCH
    let QUERY = applyHttpVerbToEndpoints QUERY
    let DELETE = applyHttpVerbToEndpoints DELETE
    let HEAD = applyHttpVerbToEndpoints HEAD
    let OPTIONS = applyHttpVerbToEndpoints OPTIONS
    let TRACE = applyHttpVerbToEndpoints TRACE
    let CONNECT = applyHttpVerbToEndpoints CONNECT

    let routeWithExtensions (configureEndpoint: ConfigureEndpoint) (path: string) (handler: HttpHandler) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering routeWithExtensions(configureEndpoint={ConfigureEndpoint})", configureEndpoint)
        LogRiteLogging._logger.LogWarning("<<< Exiting routeWithExtensions(configureEndpoint={ConfigureEndpoint})", configureEndpoint)
        SimpleEndpoint(HttpVerb.NotSpecified, path, handler, configureEndpoint)

    let route (path: string) (handler: HttpHandler) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering route(path={Path})", path)
        LogRiteLogging._logger.LogWarning("<<< Exiting route(path={Path})", path)
        routeWithExtensions (id) (path) (handler)

    let routefWithExtensions
        (configureEndpoint: ConfigureEndpoint)
        (path: PrintfFormat<_, _, _, _, 'T>)
        (routeHandler: 'T -> HttpHandler)
        : Endpoint =
        let template, mappings = RouteTemplateBuilder.convertToRouteTemplate path

        let boxedHandler (o: obj) =
            LogRiteLogging._logger.LogError(">>> Entering boxedHandler(o={O})", o)
            let t = o :?> 'T
            LogRiteLogging._logger.LogError("boxedHandler(o={O}): t → {T}", o, t)
            LogRiteLogging._logger.LogWarning("<<< Exiting boxedHandler(o={O})", o)
            routeHandler t

        TemplateEndpoint(HttpVerb.NotSpecified, template, mappings, boxedHandler, configureEndpoint)

    let routef (path: PrintfFormat<_, _, _, _, 'T>) (routeHandler: 'T -> HttpHandler) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering routef(path={Path})", path)
        LogRiteLogging._logger.LogWarning("<<< Exiting routef(path={Path})", path)
        routefWithExtensions (id) (path) (routeHandler)

    let routeBindWithExtensions<'T>
        (configureEndpoint: ConfigureEndpoint)
        (path: string)
        (routeHandler: 'T -> HttpHandler)
        : Endpoint =

        let bindRouteHandler (handler: 'T -> HttpHandler) : HttpHandler =
            LogRiteLogging._logger.LogError(">>> Entering bindRouteHandler(handler={Handler})", handler)
            fun next ctx ->
                LogRiteLogging._logger.LogWarning("<<< Exiting bindRouteHandler(handler={Handler})", handler)
                let routeData =
                    ctx.GetRouteData().Values
                    |> Seq.map (fun kvp -> KeyValuePair(kvp.Key, StringValues(kvp.Value :?> string)))
                    |> Dictionary<string, StringValues>

                match ModelParser.tryParse<'T> None routeData with
                | Ok model -> handler model next ctx
                | Error _ -> skipPipeline

        let newHandler = (bindRouteHandler routeHandler)

        SimpleEndpoint(HttpVerb.NotSpecified, path, newHandler, configureEndpoint)

    let routeBind<'T> (path: string) (routeHandler: 'T -> HttpHandler) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering routeBind(path={Path},routeHandler={RouteHandler})", path, routeHandler)
        LogRiteLogging._logger.LogWarning("<<< Exiting routeBind(path={Path},routeHandler={RouteHandler})", path, routeHandler)
        routeBindWithExtensions<'T> (id) (path) (routeHandler)

    let subRouteWithExtensions
        (configureEndpoint: ConfigureEndpoint)
        (path: string)
        (endpoints: Endpoint list)
        : Endpoint =
        NestedEndpoint(path, endpoints, configureEndpoint)

    let subRoute (path: string) (endpoints: Endpoint list) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering subRoute(path={Path})", path)
        LogRiteLogging._logger.LogWarning("<<< Exiting subRoute(path={Path})", path)
        subRouteWithExtensions (id) (path) (endpoints)

    let rec applyBefore (httpHandler: HttpHandler) (endpoint: Endpoint) =
        LogRiteLogging._logger.LogError(">>> Entering applyBefore(httpHandler={HttpHandler})", httpHandler)
        LogRiteLogging._logger.LogWarning("<<< Exiting applyBefore(httpHandler={HttpHandler})", httpHandler)
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, httpHandler >=> h, ce)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, (fun (o: obj) -> httpHandler >=> h o), ce)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, List.map (applyBefore httpHandler) lst, ce)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (applyBefore httpHandler) lst)

    let rec applyAfter (httpHandler: HttpHandler) (endpoint: Endpoint) =
        LogRiteLogging._logger.LogError(">>> Entering applyAfter(httpHandler={HttpHandler})", httpHandler)
        LogRiteLogging._logger.LogWarning("<<< Exiting applyAfter(httpHandler={HttpHandler})", httpHandler)
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, h >=> httpHandler, ce)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, (fun (o: obj) -> h o >=> httpHandler), ce)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, List.map (applyAfter httpHandler) lst, ce)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (applyAfter httpHandler) lst)

    let rec configureEndpoint (f: ConfigureEndpoint) (endpoint: Endpoint) =
        LogRiteLogging._logger.LogError(">>> Entering configureEndpoint(f={F})", f)
        LogRiteLogging._logger.LogWarning("<<< Exiting configureEndpoint(f={F})", f)
        match endpoint with
        | SimpleEndpoint(v, p, h, ce) -> SimpleEndpoint(v, p, h, ce >> f)
        | TemplateEndpoint(v, p, m, h, ce) -> TemplateEndpoint(v, p, m, h, ce >> f)
        | NestedEndpoint(t, lst, ce) -> NestedEndpoint(t, lst, ce >> f)
        | MultiEndpoint(lst) -> MultiEndpoint(List.map (configureEndpoint f) lst)

    let addMetadata (metadata: obj) (endpoint: Endpoint) : Endpoint =
        LogRiteLogging._logger.LogError(">>> Entering addMetadata(metadata={Metadata})", metadata)
        LogRiteLogging._logger.LogWarning("<<< Exiting addMetadata(metadata={Metadata})", metadata)
        endpoint |> configureEndpoint _.WithMetadata(metadata)

// ---------------------------
// Middleware Extension Methods
// ---------------------------

[<Extension>]
type EndpointRouteBuilderExtensions() =

    [<Extension>]
    static member private MapSingleEndpoint
        (builder: IEndpointRouteBuilder, singleEndpoint: HttpVerb * RouteTemplate * RequestDelegate * ConfigureEndpoint)
        =

        let verb, routeTemplate, requestDelegate, configureEndpoint = singleEndpoint

        match verb with
        | NotSpecified -> builder.Map(routeTemplate, requestDelegate) |> configureEndpoint
        | _ ->
            builder.MapMethods(routeTemplate, [ verb.ToString() ], requestDelegate)
            |> configureEndpoint
        |> ignore


    [<Extension>]
    static member private MapMultiEndpoint
        (builder: IEndpointRouteBuilder, multiEndpoint: RouteTemplate * Endpoint list * ConfigureEndpoint)
        =

        let subRouteTemplate, endpoints, configureEndpoint = multiEndpoint
        let routeTemplate = sprintf "%s%s" subRouteTemplate

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, routeTemplate t, d, configureEndpoint >> ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, routeTemplate t, d, configureEndpoint >> ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(routeTemplate t, e, configureEndpoint >> ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint(subRouteTemplate, el, configureEndpoint)
        )

    [<Extension>]
    static member private MapNestedEndpoint
        (builder: IEndpointRouteBuilder, nestedEndpoint: RouteTemplate * Endpoint list * ConfigureEndpoint)
        =

        let subRouteTemplate, endpoints, parentConfigureEndpoint = nestedEndpoint
        let routeTemplate = sprintf "%s%s" subRouteTemplate

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, routeTemplate t, d, parentConfigureEndpoint >> ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, routeTemplate t, d, parentConfigureEndpoint >> ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(routeTemplate t, e, parentConfigureEndpoint >> ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint(subRouteTemplate, el, parentConfigureEndpoint)
        )

    [<Extension>]
    static member MapGiraffeEndpoints(builder: IEndpointRouteBuilder, endpoints: Endpoint list) =

        endpoints
        |> List.iter (fun endpoint ->
            match endpoint with
            | SimpleEndpoint(v, t, h, ce) ->
                let d = RequestDelegateBuilder.createRequestDelegate h
                builder.MapSingleEndpoint(v, t, d, ce)
            | TemplateEndpoint(v, t, m, h, ce) ->
                let d = RequestDelegateBuilder.createTokenizedRequestDelegate m h
                builder.MapSingleEndpoint(v, t, d, ce)
            | NestedEndpoint(t, e, ce) -> builder.MapNestedEndpoint(t, e, ce)
            | MultiEndpoint(el) -> builder.MapMultiEndpoint("", el, id)
        )

[<Extension>]
type ApplicationBuilderExtensions() =
    /// <summary>
    /// Uses ASP.NET Core's Endpoint Routing middleware to register Giraffe endpoints.
    /// </summary>
    [<Extension>]
    static member UseGiraffe(builder: IApplicationBuilder, endpoints: Endpoint list) =

        builder.UseEndpoints(fun e -> e.MapGiraffeEndpoints(endpoints))
