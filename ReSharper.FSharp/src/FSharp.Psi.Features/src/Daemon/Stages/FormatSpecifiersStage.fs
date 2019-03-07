namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Stages

open System.Collections.Generic
open JetBrains.ReSharper.Daemon.Impl
open JetBrains.ReSharper.Plugins.FSharp.Common.Util
open JetBrains.ReSharper.Plugins.FSharp.Daemon.Cs.Stages
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Feature.Services.Daemon

type FormatSpecifiersStageProcess(fsFile: IFSharpFile, daemonProcess) =
    inherit FSharpDaemonStageProcessBase(fsFile, daemonProcess)

    override x.Execute(committer) =
        match fsFile.GetParseAndCheckResults(false) with
        | None -> ()
        | Some results ->

        let highlightings = List()
        let document = daemonProcess.Document
        for range, _ in results.CheckResults.GetFormatSpecifierLocationsAndArity() do
            let documentRange = range.ToDocumentRange(document)
            highlightings.Add(HighlightingInfo(documentRange, FormatStringItemHighlighting(documentRange)))

        committer.Invoke(DaemonStageResult(highlightings))    


[<DaemonStage(StagesBefore = [| typeof<HighlightIdentifiersStage> |], StagesAfter = [| typeof<UnusedOpensStage> |])>]
type FormatSpecifiersStage(daemonProcess, errors) =
    inherit FSharpDaemonStageBase()

    override x.CreateStageProcess(fsFile: IFSharpFile, _, daemonProcess: IDaemonProcess) =
        FormatSpecifiersStageProcess(fsFile, daemonProcess) :> _
