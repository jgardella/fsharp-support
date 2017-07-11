namespace JetBrains.ReSharper.Plugins.FSharp.Services.Formatter

open System
open System.Linq
open JetBrains.Application.Settings
open JetBrains.Application.UI.Options
open JetBrains.ReSharper.Feature.Services.OptionPages.CodeStyle
open JetBrains.ReSharper.Plugins.FSharp.ProjectModelBase
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.CodeStyle
open JetBrains.ReSharper.Psi.EditorConfig
open JetBrains.ReSharper.Psi.Impl.CodeStyle
open JetBrains.ReSharper.Psi.Format
open JetBrains.ReSharper.Psi.FSharp
open JetBrains.ReSharper.Psi.Util
open JetBrains.ReSharper.Resources.Resources.Icons

[<SettingsKey(typeof<CodeFormattingSettingsKey>, "Code formatting in F#"); EditorConfigKey("fsharp")>]
type FSharpFormatSettingsKey() =
    inherit FormatSettingsKeyBase()

    [<SettingsEntry(true, "Reorder open declarations"); DefaultValue>]
    val mutable ReorderOpenDeclarations : bool
    
    [<SettingsEntry(false, "Semicolon at end of line"); DefaultValue>]
    val mutable SemicolonAtEndOfLine : bool
    
    [<SettingsEntry(true, "Space before argument"); DefaultValue>]
    val mutable SpaceBeforeArgument : bool
    
    [<SettingsEntry(true, "Space before colon"); DefaultValue>]
    val mutable SpaceBeforeColon : bool
    
    [<SettingsEntry(true, "Space after comma"); DefaultValue>]
    val mutable SpaceAfterComma : bool
    
    [<SettingsEntry(true, "Space after semicolon"); DefaultValue>]
    val mutable SpaceAfterSemicolon : bool
    
    [<SettingsEntry(false, "Indent on try with"); DefaultValue>]
    val mutable IndentOnTryWith : bool
    
    [<SettingsEntry(true, "Space around delimiter"); DefaultValue>]
    val mutable SpaceAroundDelimiter : bool
    

[<Language(typeof<FSharpLanguage>)>]
type FSharpDummyCodeFormatter(loggerProvider, settingsOptimization) =
    inherit CodeFormatterBase<FSharpFormatSettingsKey>(loggerProvider, settingsOptimization)

    override x.LanguageType = FSharpLanguage.Instance :> _
    override x.Format(first,last,_,_) = TreeRange(first, last) :> _
    override x.FormatFile(_,_,_) = ()
    override x.CanModifyInsideNodeRange(_,_,_) = false
    override x.CanModifyNode(_,_) = false

    override x.GetMinimalSeparator(_,_) = InvalidOperationException() |> raise
    override x.CreateNewLine(_,_) = InvalidOperationException() |> raise
    override x.CreateSpace(_,_) = InvalidOperationException() |> raise
    override x.FormatInsertedNodes(_,_,_) = InvalidOperationException() |> raise
    override x.FormatInsertedRange(_,_,_) = InvalidOperationException() |> raise 
    override x.FormatReplacedRange(_,_,_) = InvalidOperationException() |> raise
    override x.FormatDeletedNodes(_,_,_) = InvalidOperationException() |> raise
    override x.FormatReplacedNode(_,_) = InvalidOperationException() |> raise


[<CodePreviewPreparatorComponent>]
type FSharpCodePreviewPreparator() = 
    inherit CodePreviewPreparator()

    override x.Language = FSharpLanguage.Instance :> _
    override x.ProjectFileType = FSharpProjectFileType.Instance :> _
    override x.Parse(parser,_) = parser.ParseFile() :> _

[<FormattingSettingsPresentationComponent>]
type FSharpCodeStylePageSchema(lifetime, smartContext, itemViewModelFactory, container, settingsToHide) =
    inherit CodeStylePageSchema<FSharpFormatSettingsKey, FSharpCodePreviewPreparator>(lifetime, smartContext,itemViewModelFactory,
                                                                 container, settingsToHide)

    override x.Language = FSharpLanguage.Instance :> _
    override x.PageName = "Formatting Style"
    override x.Describe() =
      Seq.ofList [ x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.WRAP_LIMIT)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.INDENT_SIZE)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SemicolonAtEndOfLine)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SpaceBeforeArgument)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SpaceBeforeColon)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SpaceAfterComma)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SpaceAfterSemicolon)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.IndentOnTryWith)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.SpaceAroundDelimiter)
                   x.ItemFor(fun (key : FSharpFormatSettingsKey) -> key.ReorderOpenDeclarations) ]


[<OptionsPage("FSharpCodeStylePage", "Formatting Style", typeof<PsiFeaturesUnsortedOptionsThemedIcons.Indent>)>]
type FSharpCodeStylePage(lifetime, smartContext : OptionsSettingsSmartContext, env,
                           schema : FSharpCodeStylePageSchema, preview) =
    inherit CodeStylePage(lifetime, smartContext, env, schema, preview)

    override x.Id = "FSharpIndentStylePage"