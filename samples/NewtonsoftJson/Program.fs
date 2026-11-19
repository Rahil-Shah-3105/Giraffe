open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting
open Newtonsoft.Json
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
[<RequireQualifiedAccess>]
module NewtonsoftJson =
    open System.IO
    open System.Text
    open System.Threading.Tasks
    open Microsoft.IO
    open Newtonsoft.Json.Serialization

    type Serializer(settings: JsonSerializerSettings, rmsManager: RecyclableMemoryStreamManager) =
        let serializer = JsonSerializer.Create settings
        let utf8EncodingWithoutBom = UTF8Encoding(false)

        static member DefaultSettings =
            JsonSerializerSettings(ContractResolver = CamelCasePropertyNamesContractResolver())

        interface Json.ISerializer with
            member __.SerializeToString(x: 'T) =
                LogRiteLogging._logger.LogError(">>> Entering SerializeToString(x={X})", x)
                LogRiteLogging._logger.LogWarning("<<< Exiting SerializeToString(x={X})", x)
                JsonConvert.SerializeObject(x, settings)

            member __.SerializeToBytes(x: 'T) =
                LogRiteLogging._logger.LogError(">>> Entering SerializeToBytes(x={X})", x)
                LogRiteLogging._logger.LogWarning("<<< Exiting SerializeToBytes(x={X})", x)
                JsonConvert.SerializeObject(x, settings) |> Encoding.UTF8.GetBytes

            member __.SerializeToStreamAsync (x: 'T) (stream: Stream) =
                LogRiteLogging._logger.LogError(">>> Entering SerializeToStreamAsync(x={X})", x)
                LogRiteLogging._logger.LogError("SerializeToStreamAsync(x={X}): streamWriter → {StreamWriter}", x, streamWriter)
                LogRiteLogging._logger.LogError("SerializeToStreamAsync(x={X}): memoryStream → {MemoryStream}", x, memoryStream)
                LogRiteLogging._logger.LogWarning("<<< Exiting SerializeToStreamAsync(x={X})", x)
                task {
                    use memoryStream = rmsManager.GetStream("giraffe-json-serialize-to-stream")
                    use streamWriter = new StreamWriter(memoryStream, utf8EncodingWithoutBom)
                    use jsonTextWriter = new JsonTextWriter(streamWriter)
                    serializer.Serialize(jsonTextWriter, x)
                    jsonTextWriter.Flush()
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    do! memoryStream.CopyToAsync(stream, 65536)
                }
                :> Task

            member __.Deserialize<'T>(json: string) : 'T =
                LogRiteLogging._logger.LogError(">>> Entering Deserialize(string={String})", string)
                LogRiteLogging._logger.LogWarning("<<< Exiting Deserialize(string={String})", string)
                match JsonConvert.DeserializeObject<'T>(json, settings) with
                | null -> failwith "Deserialized object is null"
                | (notNull: 'T) -> notNull

            member __.Deserialize<'T>(bytes: byte array) : 'T =
                let json = Encoding.UTF8.GetString bytes

                match JsonConvert.DeserializeObject<'T>(json, settings) with
                | null -> failwith "Deserialized object is null"
                | (notNull: 'T) -> notNull

            member __.DeserializeAsync<'T>(stream: Stream) : Task<'T> =
                LogRiteLogging._logger.LogError(">>> Entering DeserializeAsync(Stream={Stream})", Stream)
                LogRiteLogging._logger.LogError("DeserializeAsync(Stream={Stream}): streamReader → {StreamReader}", Stream, streamReader)
                LogRiteLogging._logger.LogError("DeserializeAsync(Stream={Stream}): memoryStream → {MemoryStream}", Stream, memoryStream)
                LogRiteLogging._logger.LogWarning("<<< Exiting DeserializeAsync(Stream={Stream})", Stream)
                task {
                    use memoryStream = rmsManager.GetStream("giraffe-json-deserialize")
                    do! stream.CopyToAsync(memoryStream)
                    memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
                    use streamReader = new StreamReader(memoryStream)
                    use jsonTextReader = new JsonTextReader(streamReader)

                    return
                        match serializer.Deserialize<'T>(jsonTextReader) with
                        | null -> failwith "Deserialized object is null"
                        | (notNull: 'T) -> notNull
                }

type JsonResponse = { Foo: string; Bar: string; Age: int }

let endpoints: Endpoint list =
    [ GET [ route "/json" (json { Foo = "john"; Bar = "doe"; Age = 30 }) ] ]

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let configureServices (services: IServiceCollection) =
    LogRiteLogging._logger.LogError(">>> Entering configureServices(services={Services})", services)
    services
        .AddSingleton<Json.ISerializer>(fun serviceProvider ->
            LogRiteLogging._logger.LogWarning("<<< Exiting configureServices(services={Services})", services)
            let rmsManager =
                serviceProvider.GetService<Microsoft.IO.RecyclableMemoryStreamManager>()
                |> function
                    | null -> Microsoft.IO.RecyclableMemoryStreamManager()
                    | notNull -> notNull

            NewtonsoftJson.Serializer(JsonSerializerSettings(), rmsManager) :> Json.ISerializer
        )
        .AddRouting()
        .AddResponseCaching()
        .AddGiraffe()
    |> ignore

let configureApp (appBuilder: IApplicationBuilder) =
    LogRiteLogging._logger.LogError(">>> Entering configureApp(appBuilder={AppBuilder})", appBuilder)
    LogRiteLogging._logger.LogWarning("<<< Exiting configureApp(appBuilder={AppBuilder})", appBuilder)
    appBuilder.UseRouting().UseResponseCaching().UseEndpoints(_.MapGiraffeEndpoints(endpoints)).UseGiraffe
        notFoundHandler

[<EntryPoint>]
let main (args: string array) : int =
    LogRiteLogging._logger.LogError(">>> Entering main(args={Args})", args)
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    LogRiteLogging._logger.LogError("main(args={Args}): builder → {Builder}", args, builder)

    let app = builder.Build()
    LogRiteLogging._logger.LogError("main(args={Args}): app → {App}", args, app)

    configureApp app
    app.Run()

    LogRiteLogging._logger.LogWarning("<<< Exiting main(args={Args})", args)
    0
