namespace JetBrains.ReSharper.Plugins.FSharp.Services.Linter

open JetBrains.ReSharper.Feature.Services.Daemon
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open FSharpLint.Application

[<ElementProblemAnalyzerAttribute(typeof<IFSharpFile>, HighlightingTypes=[|typeof<LintWarning>|])>]
type LintAnalyzer () =
    inherit ElementProblemAnalyzer<IFSharpFile> ()

    override __.Run (file : IFSharpFile, data : ElementProblemAnalyzerData, consumer : IHighlightingConsumer) =
        file.ParseResults
        |> Option.bind (fun parseResults -> parseResults.ParseTree)
        |> Option.iter (fun parsedFile ->
            let lintParams = {
                OptionalLintParameters.Configuration = None
                ReceivedWarning = None
                CancellationToken = None
                ReportLinterProgress = None
            }
            
            let parsedFileInfo = {
                ParsedFileInformation.Ast = parsedFile
                Source = file.GetText()
                TypeCheckResults = None
            }
            let lintResult = Lint.lintParsedSource lintParams parsedFileInfo
            
            match lintResult with
            | LintResult.Success lintWarnings ->
                lintWarnings
                |> List.map (fun warning -> LintWarning(file.GetSourceFile().Document, warning))
                |> List.iter consumer.AddHighlighting
            | LintResult.Failure lintFailure ->
                // log failure info
                ()
            )
