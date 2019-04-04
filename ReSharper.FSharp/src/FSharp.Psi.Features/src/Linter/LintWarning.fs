namespace JetBrains.ReSharper.Plugins.FSharp.Services.Linter

open JetBrains.DocumentModel
open JetBrains.ReSharper.Feature.Services.Daemon

[<StaticSeverityHighlighting(Severity.WARNING, HighlightingGroupIds.CodeStyleIssues)>]
type LintWarning (document : IDocument, lintWarning : FSharpLint.Application.LintWarning.Warning) =
    let toolTip = lintWarning.Info
    let range = DocumentRange(document, lintWarning.Range.StartRange.FileIndex)
    
    interface IHighlighting with
        
        member __.IsValid () = true
        
        member __.ToolTip = toolTip
        
        member __.ErrorStripeToolTip = toolTip
        
        member __.CalculateRange () = range
