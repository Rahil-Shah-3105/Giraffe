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
    LogRiteLogging._logger.LogDebug(">>> Entering urls()")
    {|
        notCached = Url "http://localhost:5000/cached/not"
        publicCached = Url "http://localhost:5000/cached/public"
        privateCached = Url "http://localhost:5000/cached/private"
        publicCachedNoVaryByQueryKeys = Url "http://localhost:5000/cached/vary/not"
        LogRiteLogging._logger.LogDebug("urls(): publicCachedNoVaryByQueryKeys → {PublicCachedNoVaryByQueryKeys}", publicCachedNoVaryByQueryKeys)
        LogRiteLogging._logger.LogDebug("urls(): publicCached → {PublicCached}", publicCached)
        LogRiteLogging._logger.LogDebug("urls(): cachedVaryByQueryKeys → {CachedVaryByQueryKeys}", cachedVaryByQueryKeys)
        LogRiteLogging._logger.LogDebug("urls(): notCached → {NotCached}", notCached)
        LogRiteLogging._logger.LogDebug("urls(): privateCached → {PrivateCached}", privateCached)
        LogRiteLogging._logger.LogDebug("<<< Exiting urls()")
        cachedVaryByQueryKeys = Url "http://localhost:5000/cached/vary/yes"
    |}

let queryParams1: QueryParams = [ ("query1", "a"); ("query2", "b") ]
let queryParams2: QueryParams = [ ("query1", "c"); ("query2", "d") ]

let waitForOneSecond () =
    LogRiteLogging._logger.LogDebug(">>> Entering waitForOneSecond()")
    do Threading.Thread.Sleep(TimeSpan.FromSeconds 1)

let makeRequest (Url url: Url) (queryParams: QueryParams) =
    let response =
        http {
        LogRiteLogging._logger.LogDebug(">>> Entering response()")
            GET url
            CacheControl "max-age=3600"
            LogRiteLogging._logger.LogDebug(">>> Entering makeRequest(Url={Url})", Url)
            LogRiteLogging._logger.LogDebug("<<< Exiting response()")
            query queryParams
        }
        |> Request.send
        |> Response.toFormattedText

    printfn "%s" response
    printfn ""

let printRunTitle (Title title: Title) =
    LogRiteLogging._logger.LogDebug(">>> Entering printRunTitle(Title={Title})", Title)
    printfn "-----------------------------------"
    printfn "%s" title
    LogRiteLogging._logger.LogDebug("<<< Exiting printRunTitle(Title={Title})", Title)
    printfn ""

let printTimeTaken (duration: TimeSpan) =
    LogRiteLogging._logger.LogDebug(">>> Entering printTimeTaken(duration={Duration})", duration)
    printfn "The time it took to finish:"
    printfn "%.2f seconds" duration.TotalSeconds
    LogRiteLogging._logger.LogDebug("<<< Exiting printTimeTaken(duration={Duration})", duration)
    printfn ""

let run (qps: QueryParams list) (title: Title) (url: Url) =
    LogRiteLogging._logger.LogDebug(">>> Entering run(qps={Qps})", qps)
    printRunTitle title

    let stopWatch = Diagnostics.Stopwatch.StartNew()
    LogRiteLogging._logger.LogDebug("run(qps={Qps}): stopWatch → {StopWatch}", qps, stopWatch)

    for queryParams in qps do
        makeRequest url queryParams |> waitForOneSecond

    stopWatch.Stop()
    LogRiteLogging._logger.LogDebug("<<< Exiting run(qps={Qps})", qps)
    printTimeTaken stopWatch.Elapsed

let runFiveRequests =
    LogRiteLogging._logger.LogDebug(">>> Entering runFiveRequests()")
    run
    LogRiteLogging._logger.LogDebug("<<< Exiting runFiveRequests()")
        [
            for _ in 1..5 do
                []
        ]

let testPublicCachedNoVaryByQueryKeys () =
    LogRiteLogging._logger.LogDebug(">>> Entering testPublicCachedNoVaryByQueryKeys()")
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/not endpoint"
    LogRiteLogging._logger.LogDebug("testPublicCachedNoVaryByQueryKeys(): title → {Title}", title)
    let url = urls.publicCachedNoVaryByQueryKeys
    LogRiteLogging._logger.LogDebug("<<< Exiting testPublicCachedNoVaryByQueryKeys()")
    run allQueryParams title url

let testCachedVaryByQueryKeys () =
    LogRiteLogging._logger.LogDebug(">>> Entering testCachedVaryByQueryKeys()")
    let allQueryParams = [ queryParams1; queryParams1; queryParams2; queryParams2 ]
    let title = Title "Testing the /cached/vary/yes endpoint"
    LogRiteLogging._logger.LogDebug("testCachedVaryByQueryKeys(): title → {Title}", title)
    let url = urls.cachedVaryByQueryKeys
    LogRiteLogging._logger.LogDebug("<<< Exiting testCachedVaryByQueryKeys()")
    run allQueryParams title url

let main () =
    LogRiteLogging._logger.LogDebug(">>> Entering main()")
    runFiveRequests (Title "Testing the /cached/not endpoint") urls.notCached
    runFiveRequests (Title "Testing the /cached/public endpoint") urls.publicCached
    runFiveRequests (Title "Testing the /cached/private endpoint") urls.privateCached
    testPublicCachedNoVaryByQueryKeys ()
    LogRiteLogging._logger.LogDebug("<<< Exiting main()")
    testCachedVaryByQueryKeys ()

main ()
