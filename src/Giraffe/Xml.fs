namespace Giraffe
open Microsoft.Extensions.Logging

module internal LogRiteLogging = begin let _logger : Microsoft.Extensions.Logging.ILogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance end
[<RequireQualifiedAccess>]
module Xml =
    /// <summary>
    /// Interface defining XML serialization methods.
    /// Use this interface to customize XML serialization in Giraffe.
    /// </summary>
    type ISerializer =
        abstract member Serialize: obj -> byte array
        abstract member Deserialize<'T> : string -> 'T

[<RequireQualifiedAccess>]
module SystemXml =
    open Microsoft.IO
    open System.Text
    open System.IO
    open System.Xml
    open System.Xml.Serialization

    /// <summary>
    /// Default XML serializer in Giraffe.
    /// Serializes objects to UTF8 encoded indented XML code.
    /// </summary>
    type Serializer(settings: XmlWriterSettings, rmsManager: RecyclableMemoryStreamManager) =

        new(settings: XmlWriterSettings) = Serializer(settings, recyclableMemoryStreamManager.Value)

        static member DefaultSettings =
            XmlWriterSettings(Encoding = Encoding.UTF8, Indent = true, OmitXmlDeclaration = false)

        interface Xml.ISerializer with
            member __.Serialize(o: obj) =
                LogRiteLogging._logger.LogError(">>> Entering Serialize(o={O})", o)
                use stream =
                    if rmsManager.Settings.ThrowExceptionOnToArray then
                        new MemoryStream()
                    else
                        rmsManager.GetStream("giraffe-xml-serialize")

                use writer = XmlWriter.Create(stream, settings)
                LogRiteLogging._logger.LogError("Serialize(o={O}): writer → {Writer}", o, writer)
                let serializer = XmlSerializer(o.GetType())
                LogRiteLogging._logger.LogError("Serialize(o={O}): serializer → {Serializer}", o, serializer)
                serializer.Serialize(writer, o)
                LogRiteLogging._logger.LogWarning("<<< Exiting Serialize(o={O})", o)
                stream.ToArray()

            member __.Deserialize<'T>(xml: string) =
                LogRiteLogging._logger.LogError(">>> Entering Deserialize(string={String})", string)
                let serializer = XmlSerializer(typeof<'T>)
                use stringReader = new StringReader(xml)
                LogRiteLogging._logger.LogError("Deserialize(string={String}): stringReader → {StringReader}", string, stringReader)
                LogRiteLogging._logger.LogError("Deserialize(string={String}): serializer → {Serializer}", string, serializer)
                // Secure XML parsing: disable DTD processing and external entities to prevent XXE attacks
                let xmlReaderSettings =
                    new XmlReaderSettings(
                        DtdProcessing = DtdProcessing.Prohibit,
                        XmlResolver = null,
                        MaxCharactersFromEntities = 1024L * 1024L
                    ) // 1MB limit

                use xmlReader = XmlReader.Create(stringReader, xmlReaderSettings)
                LogRiteLogging._logger.LogError("Deserialize(string={String}): xmlReader → {XmlReader}", string, xmlReader)
                LogRiteLogging._logger.LogWarning("<<< Exiting Deserialize(string={String})", string)
                serializer.Deserialize xmlReader :?> 'T
