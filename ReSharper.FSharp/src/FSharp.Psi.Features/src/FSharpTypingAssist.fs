module rec JetBrains.ReSharper.Plugins.FSharp.Psi.Features.TypingAssist

open System
open System.Collections.Generic
open JetBrains.Application.UI.ActionSystem.Text
open JetBrains.DocumentModel
open JetBrains.ProjectModel
open JetBrains.ReSharper.Feature.Services.TypingAssist
open JetBrains.ReSharper.Plugins.FSharp.Common.Util
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Parsing
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.CachingLexers
open JetBrains.ReSharper.Psi.CodeStyle
open JetBrains.ReSharper.Psi.Parsing
open JetBrains.TextControl
open JetBrains.Util

[<SolutionComponent>]
type FSharpTypingAssist
        (lifetime, solution, settingsStore, cachingLexerService, commandProcessor, psiServices,
         externalIntellisenseHost, manager: ITypingAssistManager) as this =
    inherit TypingAssistLanguageBase<FSharpLanguage>
        (solution, settingsStore, cachingLexerService, commandProcessor, psiServices, externalIntellisenseHost)

    let indentingTokens =
        [| FSharpTokenType.EQUALS
           FSharpTokenType.LARROW
           FSharpTokenType.RARROW
           FSharpTokenType.LPAREN
           FSharpTokenType.LBRACK
           FSharpTokenType.LBRACK_BAR
           FSharpTokenType.LBRACK_LESS
           FSharpTokenType.LBRACE
           FSharpTokenType.BEGIN
           FSharpTokenType.DO
           FSharpTokenType.THEN
           FSharpTokenType.ELSE
           FSharpTokenType.STRUCT
           FSharpTokenType.CLASS
           FSharpTokenType.INTERFACE
           FSharpTokenType.TRY
           FSharpTokenType.WHEN |]
        |> HashSet

    let allowingNoIndentTokens =
        [| FSharpTokenType.RARROW
           FSharpTokenType.ELSE
           FSharpTokenType.DO |]
        |> HashSet

    let getCachingLexer textControl (lexer: outref<_>) =
        match cachingLexerService.GetCachingLexer(textControl) with
        | null -> false
        | cachingLexer ->
            lexer <- cachingLexer
            true

    let getBaseIndentLine (document: IDocument) initialLine =
        let mutable line = initialLine
        while line > Line.O && document.GetLineText(line).IsWhitespace() do
            line <- line - Line.I

        if document.GetLineText(line).IsWhitespace() then initialLine else line

    let trimTrailingSpacesBeforeOffset (document: IDocument) (offset: outref<int>) =
        let initialOffset = offset
        let buffer = document.Buffer
        while offset > 0 &&
                let c = buffer.[offset - 1]
                c = ' ' || c = '\t' do
            offset <- offset - 1

        if offset <> initialOffset then
            document.DeleteText(TextRange(offset, initialOffset))

    let trimTrailingSpacesBeforeCaret (textControl: ITextControl) =
        let mutable offset = textControl.Caret.Offset()
        trimTrailingSpacesBeforeOffset textControl.Document &offset
        offset

    let insertText (textControl: ITextControl) insertOffset text commandName =
        let inserted =
            this.PsiServices.Transactions.DocumentTransactionManager.DoTransaction(commandName, fun _ ->
                textControl.Document.InsertText(insertOffset, text)
                true)
        if inserted then
            textControl.Caret.MoveTo(insertOffset + text.Length, CaretVisualPlacement.DontScrollIfVisible)
        inserted

    let getLineWhitespaceIndent (textControl: ITextControl) line =
        let document = textControl.Document
        let buffer = document.Buffer
        let startOffset = document.GetLineStartOffset(line)
        let endOffset = document.GetLineEndOffsetNoLineBreak(line)

        let mutable pos = startOffset
        while pos < endOffset && Char.IsWhiteSpace(buffer.[pos]) do
            pos <- pos + 1

        pos - startOffset

    let doDumpIndent (textControl: ITextControl) =
        let caretOffset = textControl.Caret.Offset()
        let line = textControl.Document.GetCoordsByOffset(caretOffset).Line
        let lineIndent = getLineWhitespaceIndent textControl line

        let insertPos = trimTrailingSpacesBeforeCaret textControl
        let text = this.GetNewLineText(textControl) + String(' ', lineIndent)

        insertText textControl insertPos text "Indent on Enter"

    let handleEnter (context: IActionContext) =
        let textControl = context.TextControl

        if this.HandleEnterFindLeftBracket(textControl) then true else
        if this.HandleEnterAddIndent(textControl) then true else

        doDumpIndent textControl

    let isAvailable = Predicate<_>(this.IsAvailable)

    do
        manager.AddActionHandler(lifetime, TextControlActions.ENTER_ACTION_ID, this, Func<_,_>(handleEnter), isAvailable)

    member x.HandleEnterFindLeftBracket(textControl) =
        let mutable lexer = Unchecked.defaultof<_>
        let mutable offset = Unchecked.defaultof<_>

        let isAvailable =
            x.CheckAndDeleteSelectionIfNeeded(textControl, fun selection ->
                offset <- selection.StartOffset
                offset > 0 && getCachingLexer textControl &lexer && lexer.FindTokenAt(offset - 1))

        if not isAvailable then false else

        let document = textControl.Document
        let lineStartOffset = document.GetLineStartOffset(document.GetCoordsByOffset(offset).Line)

        let foundToken = findUnmatchedBracketToLeft lexer offset lineStartOffset
        if not foundToken then false else

        lexer.Advance()
        while lexer.TokenType == FSharpTokenType.WHITESPACE do
            lexer.Advance()

        let insertPos = trimTrailingSpacesBeforeCaret textControl
        let text = this.GetNewLineText(textControl) + String(' ', lexer.TokenStart - lineStartOffset)
        insertText textControl insertPos text "Indent on Enter"

    member x.HandleEnterAddIndent(textControl) =
        let mutable lexer = Unchecked.defaultof<_>

        let isAvailable =
            x.CheckAndDeleteSelectionIfNeeded(textControl, fun selection ->
                let offset = selection.StartOffset
                if offset <= 0 then false else

                if not (getCachingLexer textControl &lexer && lexer.FindTokenAt(offset - 1)) then false else

                while isIgnoredOrNewLine lexer.TokenType do
                    lexer.Advance(-1)

                indentingTokens.Contains(lexer.TokenType))

        if not isAvailable then false else

        match textControl.Document.GetPsiSourceFile(x.Solution) with
        | null -> false
        | sourceFile ->

        let line = textControl.Document.GetCoordsByOffset(lexer.TokenStart).Line
        let indentSize =
            match tryGetNestedIndentBelow cachingLexerService textControl line with
            | Some (Source n | Comments n) -> n
            | _ -> sourceFile.GetFormatterSettings(sourceFile.PrimaryPsiLanguage).INDENT_SIZE

        let insertPos = trimTrailingSpacesBeforeCaret textControl
        let prevIndentSize = getLineWhitespaceIndent textControl line
        let text = this.GetNewLineText(textControl) + String(' ' , prevIndentSize + indentSize) 
        insertText textControl insertPos text "Indent on Enter"

    member x.GetNewLineText(textControl: ITextControl) =
        x.GetNewLineText(textControl.Document.GetPsiSourceFile(x.Solution))

    member x.IsAvailable(context) =
        x.IsActionHandlerAvailabile(context)

    override x.IsSupported(textControl: ITextControl) =
        match textControl.Document.GetPsiSourceFile(x.Solution) with
        | null -> false
        | sourceFile ->

        sourceFile.IsValid() &&
        sourceFile.PrimaryPsiLanguage.Is<FSharpLanguage>() &&
        sourceFile.Properties.ProvidesCodeModel

    interface ITypingHandler with
        member x.QuickCheckAvailability(textControl, sourceFile) =
            sourceFile.PrimaryPsiLanguage.Is<FSharpLanguage>()


type LineIndent =
    // Code indent, as seen by compiler.
    | Source of int

    // Fallback indent when no code is present on line. Used to guess the desired indentation.
    | Comments of int

let getLineIndent (cachingLexerService: CachingLexerService) (textControl: ITextControl) (line: Line) =
    let document = textControl.Document
    if line >= document.GetLineCount() then None else

    let startOffset = document.GetLineStartOffset(line)
    let endOffset = document.GetLineEndOffsetNoLineBreak(line)

    match cachingLexerService.GetCachingLexer(textControl) with
    | null -> None
    | lexer ->

    if not (lexer.FindTokenAt(startOffset)) then None else

    let mutable commentOffset = None
    while lexer.TokenType != null && lexer.TokenStart < endOffset && isIgnored lexer.TokenType do
        if commentOffset.IsNone && lexer.TokenType.IsComment then
            commentOffset <- Some (Comments (lexer.TokenStart - startOffset))
        lexer.Advance()

    match lexer.TokenType with
    | null -> commentOffset
    | tokenType ->

    if isIgnoredOrNewLine tokenType then None else
    Some (Source (lexer.TokenStart - startOffset))

let tryGetNestedIndentBelow cachingLexerService textControl line =
    match getLineIndent cachingLexerService textControl line with
    | None | Some (Comments _) -> None
    | Some (Source currentIndent) ->

    let linesCount = textControl.Document.GetLineCount()

    let rec getIndent firstFoundCommentIndent line =
        if line >= linesCount then firstFoundCommentIndent else

        let indent = getLineIndent cachingLexerService textControl line
        match indent, firstFoundCommentIndent with
        | Some (Source n) as indent, _ ->
            if n > currentIndent then indent else firstFoundCommentIndent

        | Some (Comments _), None -> getIndent indent line.Next
        | _ -> getIndent firstFoundCommentIndent line.Next

    getIndent None line.Next

let findUnmatchedBracketToLeft (lexer: CachingLexer) offset minOffset =
    if lexer.TokenEnd > offset then
        lexer.Advance(-1)

    let matcher = FSharpBracketMatcher()
    let mutable foundToken = false

    while not foundToken && lexer.TokenStart >= minOffset do
        matcher.ProceedStack(lexer.TokenType) |> ignore
        if matcher.IsStackEmpty() || not FSharpTokenType.LeftBraces.[lexer.TokenType] then
            lexer.Advance(-1)
        else
            foundToken <- true
    foundToken

let isIgnored (tokenType: TokenNodeType) =
    tokenType == FSharpTokenType.WHITESPACE || tokenType.IsComment

let isIgnoredOrNewLine tokenType =
    isIgnored tokenType || tokenType == FSharpTokenType.NEW_LINE

let matchingBrackets =
    [| Pair.Of(FSharpTokenType.LPAREN, FSharpTokenType.RPAREN)
       Pair.Of(FSharpTokenType.LBRACK, FSharpTokenType.RBRACK)
       Pair.Of(FSharpTokenType.LBRACE, FSharpTokenType.RBRACE) 
       Pair.Of(FSharpTokenType.LBRACK_BAR, FSharpTokenType.BAR_RBRACK) 
       Pair.Of(FSharpTokenType.LBRACK_LESS, FSharpTokenType.GREATER_RBRACK)
       Pair.Of(FSharpTokenType.BEGIN, FSharpTokenType.END)
       Pair.Of(FSharpTokenType.CLASS, FSharpTokenType.END)
       Pair.Of(FSharpTokenType.STRUCT, FSharpTokenType.END)
       Pair.Of(FSharpTokenType.INTERFACE, FSharpTokenType.END)
       Pair.Of(FSharpTokenType.WITH, FSharpTokenType.END)
       Pair.Of(FSharpTokenType.DO, FSharpTokenType.DONE) |]

type FSharpBracketMatcher() =
    inherit BracketMatcher(matchingBrackets)
