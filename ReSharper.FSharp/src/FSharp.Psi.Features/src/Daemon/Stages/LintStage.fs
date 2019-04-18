namespace JetBrains.ReSharper.Plugins.FSharp.Daemon.Stages

open FSharp.Compiler.ErrorLogger
open FSharpLint.Application
open JetBrains.DocumentModel
open JetBrains.ReSharper.Feature.Services.Daemon
open JetBrains.ReSharper.Plugins.FSharp.Daemon.Cs.Stages
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree

open JetBrains.Util

[<StaticSeverityHighlighting(Severity.WARNING, HighlightingGroupIds.CodeStyleIssues)>]
type LintWarning (range, lintWarning : LintWarning.Warning) =
    let toolTip = lintWarning.Info
    let range = range
    
    interface IHighlighting with
        
        member __.IsValid () = true
        
        member __.ToolTip = toolTip
        
        member __.ErrorStripeToolTip = toolTip
        
        member __.CalculateRange () = range

type LintStageProcess(fsFile: IFSharpFile, daemonProcess, logger: ILogger) =
    inherit ErrorsStageProcessBase(fsFile, daemonProcess)

    let [<Literal>] opName = "LintStageProcess"

    override x.Execute(committer) =
        match fsFile.GetParseAndCheckResults(false, opName) with
        | None -> ()
        | Some results ->
            results.ParseResults.ParseTree
            |> Option.iter (fun parseTree ->
                let lintParams = {
                    OptionalLintParameters.Configuration = None
                    ReceivedWarning = None
                    CancellationToken = None
                    ReportLinterProgress = None
                }
                
                let parsedFileInfo = {
                    ParsedFileInformation.Ast = parseTree
                    Source = fsFile.GetText()
                    TypeCheckResults = None
                }
                let lintResult = Lint.lintParsedSource lintParams parsedFileInfo
                
                match lintResult with
                | LintResult.Success lintWarnings ->
                    let highlightings = 
                        lintWarnings
                        |> List.map (fun warning ->
                            let document = fsFile.GetSourceFile().Document
                            let range = DocumentRange(document, warning.Range.StartRange.FileIndex)
                            let highlighting = LintWarning(range, warning)
                            HighlightingInfo(range, highlighting))
                        
                    committer.Invoke(DaemonStageResult(highlightings))
                | LintResult.Failure lintFailure ->
                    // log failure info
                    ())

[<DaemonStage(StagesBefore = [| typeof<TypeCheckErrorsStage> |], StagesAfter = [| |])>]
type LintStage(logger: ILogger) =
    inherit FSharpDaemonStageBase()

    override x.CreateStageProcess(fsFile, _, daemonProcess) =
        LintStageProcess(fsFile, daemonProcess, logger) :> _
