module Giraffe.FormatExpressions

open System
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection
open FSharp.Core
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
// ---------------------------
// String matching functions
// ---------------------------

let private formatStringMap =
    let decodeSlashes (str: string) =
        LogRiteLogging._logger.LogError(">>> Entering decodeSlashes(str={Str})", str)
        // Kestrel has made the weird decision to
        // partially decode a route argument, which
        // means that a given route argument would get
        // entirely URL decoded except for '%2F' (/).
        // Hence decoding %2F must happen separately as
        // part of the string parsing function.
        //
        // For more information please check:
        // https://github.com/aspnet/Mvc/issues/4599
        LogRiteLogging._logger.LogWarning("<<< Exiting decodeSlashes(str={Str})", str)
        str.Replace("%2F", "/").Replace("%2f", "/")

    let parseGuid (str: string) =
        LogRiteLogging._logger.LogError(">>> Entering parseGuid(str={Str})", str)
        LogRiteLogging._logger.LogWarning("<<< Exiting parseGuid(str={Str})", str)
        match str.Length with
        | 22 -> ShortGuid.toGuid str
        | _ -> Guid str

    let guidPattern =
        "([0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32}|[-_0-9A-Za-z]{22})"

    let shortIdPattern = "([-_0-9A-Za-z]{10}[048AEIMQUYcgkosw])"

    dict
        [
            // Char    Regex                    Parser
            // -------------------------------------------------------------
            'b', ("(?i:(true|false)){1}", (fun (s: string) -> bool.Parse s) >> box) // bool
            'c', ("([^/]{1})", char >> box) // char
            's', ("([^/]+)", decodeSlashes >> box) // string
            'i', ("(-?\d+)", int32 >> box) // int
            'd', ("(-?\d+)", int64 >> box) // int64
            'f', ("(-?\d+\.{1}\d+)", float >> box) // float
            'O', (guidPattern, parseGuid >> box) // Guid
            'u', (shortIdPattern, ShortId.toUInt64 >> box)
        ] // uint64

type MatchMode =
    | Exact // Will try to match entire string from start to end.
    | StartsWith // Will try to match a substring. Subject string should start with test case.
    | EndsWith // Will try to match a substring. Subject string should end with test case.
    | Contains // Will try to match a substring. Subject string should contain test case.

type MatchOptions =
    {
        IgnoreCase: bool
        MatchMode: MatchMode
    }

    static member Exact =
        {
            IgnoreCase = false
            MatchMode = Exact
        }

    static member IgnoreCaseExact = { IgnoreCase = true; MatchMode = Exact }

let private convertToRegexPatternAndFormatChars (mode: MatchMode) (formatString: string) =
    LogRiteLogging._logger.LogError(">>> Entering convertToRegexPatternAndFormatChars(mode={Mode})", mode)
    let rec convert (chars: char list) =
        LogRiteLogging._logger.LogError(">>> Entering convert(chars={Chars})", chars)
        match chars with
        | '%' :: '%' :: tail ->
            LogRiteLogging._logger.LogWarning("<<< Exiting convert(chars={Chars})", chars)
            let pattern, formatChars = convert tail
            "%" + pattern, formatChars
        | '%' :: c :: tail ->
            let pattern, formatChars = convert tail
            let regex, _ = formatStringMap.[c]
            regex + pattern, c :: formatChars
        | c :: tail ->
            let pattern, formatChars = convert tail
            c.ToString() + pattern, formatChars
        | [] -> "", []

    let inline formatRegex mode pattern =
        LogRiteLogging._logger.LogError(">>> Entering formatRegex(mode={Mode},pattern={Pattern})", mode, pattern)
        LogRiteLogging._logger.LogWarning("<<< Exiting formatRegex(mode={Mode},pattern={Pattern})", mode, pattern)
        match mode with
        | Exact -> "^" + pattern + "$"
        | StartsWith -> "^" + pattern
        | EndsWith -> pattern + "$"
        | Contains -> pattern

    formatString
    |> List.ofSeq
    |> convert
    |> (fun (pattern, formatChars) -> formatRegex mode pattern, formatChars)

/// <summary>
/// Tries to parse an input string based on a given format string and return a tuple of all parsed arguments.
/// </summary>
/// <param name="format">The format string which shall be used for parsing.</param>
/// <param name="options">The options record with specifications on how the matching should behave.</param>
/// <param name="input">The input string from which the parsed arguments shall be extracted.</param>
/// <returns>Matched value as an option of 'T</returns>
let tryMatchInput (format: PrintfFormat<_, _, _, _, 'T>) (options: MatchOptions) (input: string) =
    LogRiteLogging._logger.LogError(">>> Entering tryMatchInput(format={Format})", format)
    try
        LogRiteLogging._logger.LogWarning("<<< Exiting tryMatchInput(format={Format})", format)
        let pattern, formatChars =
            format.Value
            |> Regex.Escape
            |> convertToRegexPatternAndFormatChars options.MatchMode

        let options =
            match options.IgnoreCase with
            | true -> RegexOptions.IgnoreCase
            | false -> RegexOptions.None

        let result = Regex.Match(input, pattern, options)
        LogRiteLogging._logger.LogError("tryMatchInput(format={Format}): result → {Result}", format, result)

        if result.Groups.Count <= 1 then
            None
        else
            let groups = result.Groups |> Seq.cast<Group> |> Seq.skip 1

            let values =
                (groups, formatChars)
                ||> Seq.map2 (fun g c ->
                    let _, parser = formatStringMap.[c]
                    let value = parser g.Value
                    value
                )
                |> Seq.toArray

            let result =
                match values.Length with
                | 1 -> values.[0]
                | _ ->
                    let types = values |> Array.map (fun v -> v.GetType())
                    LogRiteLogging._logger.LogError("tryMatchInput(format={Format}): types → {Types}", format, types)
                    let tupleType = FSharpType.MakeTupleType types
                    FSharpValue.MakeTuple(values, tupleType)

            result :?> 'T |> Some
    with _ ->
        LogRiteLogging._logger.LogError("Exception in tryMatchInput(format={Format}): {Arg1}", format, _.Message)
        None

/// <summary>
/// Tries to parse an input string based on a given format string and return a tuple of all parsed arguments.
/// </summary>
/// <param name="format">The format string which shall be used for parsing.</param>
/// <param name="ignoreCase">The flag to make matching case insensitive.</param>
/// <param name="input">The input string from which the parsed arguments shall be extracted.</param>
/// <returns>Matched value as an option of 'T</returns>
let tryMatchInputExact (format: PrintfFormat<_, _, _, _, 'T>) (ignoreCase: bool) (input: string) =
    LogRiteLogging._logger.LogError(">>> Entering tryMatchInputExact(format={Format})", format)
    let options =
        match ignoreCase with
        | true -> MatchOptions.IgnoreCaseExact
        LogRiteLogging._logger.LogWarning("<<< Exiting tryMatchInputExact(format={Format})", format)
        | false -> MatchOptions.Exact

    tryMatchInput format options input


// ---------------------------
// Validation helper functions
// ---------------------------

/// **Description**
///
/// Validates if a given format string can be matched with a given tuple.
///
/// **Parameters**
///
/// `format`: The format string which shall be used for parsing.
///
/// **Output**
///
/// Returns `unit` if validation was successful otherwise will throw an `Exception`.
/// <summary>
/// Validates if a given format string can be matched with a given tuple.
/// </summary>
/// <param name="format">The format string which shall be used for parsing.</param>
/// <returns>Returns <see cref="Microsoft.FSharp.Core.Unit"/> if validation was successful otherwise will throw an `Exception`.</returns>
let validateFormat (format: PrintfFormat<_, _, _, _, 'T>) =
    LogRiteLogging._logger.LogError(">>> Entering validateFormat(format={Format})", format)

    LogRiteLogging._logger.LogWarning("<<< Exiting validateFormat(format={Format})", format)
    let mapping =
        [
            'b', typeof<bool> // bool
            'c', typeof<char> // char
            's', typeof<string> // string
            'i', typeof<int32> // int
            'd', typeof<int64> // int64
            'f', typeof<float> // float
            'O', typeof<System.Guid> // guid
            'u', typeof<uint64>
        ] // guid

    let tuplePrint pos last name =
        LogRiteLogging._logger.LogError(">>> Entering tuplePrint(pos={Pos},last={Last},name={Name})", pos, last, name)
        let mutable result = "("

        LogRiteLogging._logger.LogError("tuplePrint(pos={Pos},last={Last},name={Name}): result → {Result}", pos, last, name, result)
        for i in 0..last do
            if i = pos then
                result <- result + name
            else
                result <- result + "_"

            if i <> last then
                LogRiteLogging._logger.LogWarning("<<< Exiting tuplePrint(pos={Pos},last={Last},name={Name})", pos, last, name)
                result <- result + ","

        result + ")"

    let posPrint =
        function
        | 1 -> "1st"
        | 2 -> "2nd"
        | 3 -> "3rd"
        | x -> x.ToString() + "th"

    let t = typeof<'T>
    let path = format.Value
    let mutable parseChars = []
    let mutable matchNext = false
    let mutable matches = 0

    let rec charTypeMatch ls mChar =
        LogRiteLogging._logger.LogError(">>> Entering charTypeMatch(ls={Ls},mChar={MChar})", ls, mChar)
        match ls with
        | [] -> ()
        | (tChar, x) :: xs ->
            LogRiteLogging._logger.LogWarning("<<< Exiting charTypeMatch(ls={Ls},mChar={MChar})", ls, mChar)
            if tChar = mChar then
                parseChars <- (mChar, x) :: parseChars
                matches <- matches + 1
            else
                charTypeMatch xs mChar

    let rec typeCharMatch ls (xType: System.Type) =
        LogRiteLogging._logger.LogError(">>> Entering typeCharMatch(ls={Ls},xType={XType})", ls, xType)
        match ls with
        | [] -> sprintf "%s has no associated format char parameter" xType.Name
        | (tChar, x) :: xs ->
            LogRiteLogging._logger.LogWarning("<<< Exiting typeCharMatch(ls={Ls},xType={XType})", ls, xType)
            if xType = x then
                sprintf "%s uses format char '%%%c'" xType.Name tChar
            else
                typeCharMatch xs xType

    for i in 0 .. path.Length - 1 do
        let mChar = path.[i]

        if matchNext then
            charTypeMatch mapping mChar
            matchNext <- false
        else if mChar = '%' then
            matchNext <- true

    if FSharpType.IsTuple(t) then
        let types = FSharpType.GetTupleElements(t)

        if types.Length <> matches then
            failwithf
                "Format string error: Number of parameters (%i) does not match number of tuple variables (%i)."
                types.Length
                matches

        let rec check (ls, pos) =
            LogRiteLogging._logger.LogError(">>> Entering check(ls={Ls},pos={Pos})", ls, pos)
            match ls, pos with
            | [], -1 -> ()
            | (mChar, ct) :: xs, i ->
                LogRiteLogging._logger.LogWarning("<<< Exiting check(ls={Ls},pos={Pos})", ls, pos)
                if ct <> types.[i] then
                    let hdlFmt = tuplePrint i (types.Length - 1) types.[i].Name
                    LogRiteLogging._logger.LogError("check(ls={Ls},pos={Pos}): hdlFmt → {HdlFmt}", ls, pos, hdlFmt)
                    let expFmt = tuplePrint i (types.Length - 1) ct.Name
                    LogRiteLogging._logger.LogError("check(ls={Ls},pos={Pos}): expFmt → {ExpFmt}", ls, pos, expFmt)
                    let guidance = typeCharMatch mapping types.[i]

                    failwithf
                        "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on %s parameter '%%%c', %s."
                        path
                        expFmt
                        hdlFmt
                        (posPrint (i + 1))
                        mChar
                        guidance
                else
                    check (xs, i - 1)
            | x, y -> failwithf "Format string error: Unknown validation error: %A [%i]." x y

        check (parseChars, types.Length - 1)

    else
        if matches <> 1 then
            failwithf "Format string error: Number of parameters (%i) does not match single variable." matches

        match parseChars with
        | [ (mChar, ct) ] ->
            if ct <> t then
                let guidance = typeCharMatch mapping t

                failwithf
                    "Format string error: routef '%s' has type '%s' but handler expects '%s', mismatch on parameter '%%%c', %s."
                    path
                    ct.Name
                    t.Name
                    mChar
                    guidance
        | x -> failwithf "Format string error: Unknown validation error: %A." x
