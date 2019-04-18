namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Daemon.Stages

open System.Collections.Generic
open FSharp.Compiler
open FSharp.Compiler.Range
open FSharpLint.Application
open FSharpLint.Application.ConfigurationManagement
open JetBrains.DocumentModel
open JetBrains.ReSharper.Daemon.UsageChecking
open JetBrains.ReSharper.Feature.Services.Daemon
open JetBrains.ReSharper.Plugins.FSharp.Daemon.Cs.Stages
open JetBrains.ReSharper.Plugins.FSharp.Daemon.Highlightings
open JetBrains.ReSharper.Plugins.FSharp.Daemon.Stages
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Plugins.FSharp.Psi.Util
open JetBrains.ReSharper.Psi
open JetBrains.Util

type LintStageProcess(fsFile: IFSharpFile, daemonProcess, logger : ILogger) =
    inherit FSharpDaemonStageProcessBase(fsFile, daemonProcess)

    let [<Literal>] opName = "LintStageProcess"
    
    let config =
        let (sourceFile:IPsiSourceFile) = daemonProcess.SourceFile
        match ConfigurationManagement.loadConfigurationForProject (sourceFile.GetLocation().FullPath) with
        | ConfigurationResult.Success config -> Some config
        | ConfigurationResult.Failure _ -> None
    
    let getDocumentRange (range: Range.range) =
        let document = daemonProcess.Document
        let startOffset =  document.GetDocumentOffset(range.StartLine - 1, range.StartColumn)
        let endOffset = document.GetDocumentOffset(range.EndLine - 1, range.EndColumn)
        DocumentRange(document, TextRange(startOffset, endOffset))    
    
    override x.Execute(committer) =
        let highlightings = List()
        match fsFile.GetParseAndCheckResults(false, opName) with
        | None -> ()
        | Some results ->
            results.ParseResults.ParseTree
            |> Option.iter (fun parseTree ->
                let lintParams = {
                    OptionalLintParameters.Configuration = config
                    ReceivedWarning = None
                    CancellationToken = None
                    ReportLinterProgress = None
                }
                
                let parsedFileInfo = {
                    ParsedFileInformation.Ast = parseTree
                    Source = fsFile.GetText()
                    TypeCheckResults = Some results.CheckResults
                }
                let lintResult = Lint.lintParsedSource lintParams parsedFileInfo
                
                match lintResult with
                | LintResult.Success lintWarnings ->
                    lintWarnings
                    |> List.iter (fun warning ->
                        let range = getDocumentRange warning.Range
                        let highlighting = LintHighlighting(warning.Info, range)
                        highlightings.Add(HighlightingInfo(range, highlighting)))
                        
                | LintResult.Failure lintFailure -> ())
                
            logger.Info (sprintf "Committing %d lint warnings" highlightings.Count)
            committer.Invoke(DaemonStageResult(highlightings))

[<DaemonStage(StagesBefore = [| typeof<TypeCheckErrorsStage> |], StagesAfter = [| typeof<CollectUsagesStage> |])>]
type LintStage(logger : ILogger) =
    inherit FSharpDaemonStageBase()

    override x.CreateStageProcess(fsFile: IFSharpFile, _, daemonProcess: IDaemonProcess) =
        LintStageProcess(fsFile, daemonProcess, logger) :> _
