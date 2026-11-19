#r "nuget: FsHttp, 11.0.0"

open System
open FsHttp
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
// Uncomment if you don't want FsHttp debug logs
// Fsi.disableDebugLogs()

type QueryParams = (string * obj) list
type Url = Url of string
type Title = Title of string

let urls =
    {|
        notCached = Url "http://localhost:5000/cached/not"
        publicCached = Url "http://localhost:5000/cached/public"
        privateCached = Url "http://localhost:5000/cached/private"
        publicCachedNoVaryByQueryKeys = Url "http://localhost:5000/cached/vary/not"
        cachedVaryByQueryKeys = Url "http://localhost:5000/cached/vary/yes"
    |}

let queryParams1: QueryParams = [ ("query1", "a"); ("query2", "b") ]
let queryParams2: QueryParams = [ ("query1", "c"); ("query2", "d") ]

let waitForOneSecond () =
    LogRiteLogging._logger.LogError(">>> Entering waitForOneSecond()")
    do Threading.Thread.Sleep(TimeSpan.FromSeconds 1)

let makeRequest (Url url: Url) (queryParams: QueryParams) =
    LogRiteLogging._logger.LogError(">>> Entering makeRequest(Url={Url})", Url)
    let response =
        http {
            GET url
            CacheControl "max-age=3600"
            query queryParams
        }
        |> Request.send
        |> Response.toFormattedText

    printfn "%s" response
    LogRiteLogging._logger.LogWarning("<<< Exiting makeRequest(Url={Url})", Url)
    printfn ""

let printRunTitle (Title title: Title) =
    LogRiteLogging._logger.LogError(">>> Entering printRunTitle(Title={Title})", Title)
    printfn "-----------------------------------"
    printfn "%s" title
    LogRiteLogging._logger.LogWarning("<<< Exiting printRunTitle(Title={Title})", Title)
    printfn ""

let printTimeTaken (duration: TimeSpan) =
    LogRiteLogging._logger.LogError(">>> Entering printTimeTaken(duration={Duration})", duration)
    printfn "The time it took to finish:"
    printfn "%.2f seconds" duration.TotalSeconds
    LogRiteLogging._logger.LogWarning("<<< Exiting printTimeTaken(duration={Duration})", duration)
    printfn ""

let run (qps: QueryParams list) (title: Title) (url: Url) =
    LogRiteLogging._logger.LogError(">>> Entering run(qps={Qps})", qps)
    printRunTitle title

    let stopWatch = Diagnostics.Stopwatch.StartNew()
    LogRiteLogging._logger.LogError("run(qps={Qps}): stopWatch → {StopWatch}", qps, stopWatch)

    for queryParams in qps do
        makeRequest url queryParams |> waitForOneSecond

    stopWatch.Stop()
    LogRiteLogging._logger.LogWarning("<<< Exiting run(qps={Qps})", qps)
    printTimeTaken stopWatch.Elapsed

let runFiveRequests =
    run
        [
            for _ in 1..5 do
                []
        ]

let testPublicCachedNoVaryByQueryKeys () =
    LogRiteLogging._logger.LogError(">>> Entering testPublicCachedNoVaryByQueryKeys()")
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/not endpoint"
    LogRiteLogging._logger.LogError("testPublicCachedNoVaryByQueryKeys(): title → {Title}", title)
    let url = urls.publicCachedNoVaryByQueryKeys
    LogRiteLogging._logger.LogWarning("<<< Exiting testPublicCachedNoVaryByQueryKeys()")
    run allQueryParams title url

let testCachedVaryByQueryKeys () =
    LogRiteLogging._logger.LogError(">>> Entering testCachedVaryByQueryKeys()")
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/yes endpoint"
    LogRiteLogging._logger.LogError("testCachedVaryByQueryKeys(): title → {Title}", title)
    let url = urls.cachedVaryByQueryKeys
    LogRiteLogging._logger.LogWarning("<<< Exiting testCachedVaryByQueryKeys()")
    run allQueryParams title url

let main () =
    LogRiteLogging._logger.LogError(">>> Entering main()")
    runFiveRequests (Title "Testing the /cached/not endpoint") urls.notCached
    runFiveRequests (Title "Testing the /cached/public endpoint") urls.publicCached
    runFiveRequests (Title "Testing the /cached/private endpoint") urls.privateCached
    testPublicCachedNoVaryByQueryKeys ()
    LogRiteLogging._logger.LogWarning("<<< Exiting main()")
    testCachedVaryByQueryKeys ()

main ()
