namespace rec JetBrains.ReSharper.Plugins.FSharp.Psi.Features.Refactorings.Rename

open JetBrains.Application
open JetBrains.ReSharper.Feature.Services.Refactorings.Specific.Rename
open JetBrains.ReSharper.Plugins.FSharp.Psi
open JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.DeclaredElement.CompilerGenerated
open JetBrains.ReSharper.Plugins.FSharp.Psi.Tree
open JetBrains.ReSharper.Psi
open JetBrains.ReSharper.Psi.ExtensionsAPI
open JetBrains.ReSharper.Psi.Naming.Impl
open JetBrains.ReSharper.Refactorings.Rename
open JetBrains.Util
open JetBrains.Util.Logging
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.PrettyNaming

[<AutoOpen>]
module Util =
    type IRenameWorkflow with
        member x.RenameWorkflow =
            match x with
            | :? RenameWorkflow as workflow -> workflow
            | _ -> failwithf "Got workflow: %O" x

        member x.RenameDataModel =
            x.RenameWorkflow.DataModel

        member x.FSharpChangeNameKind =
            match x.RenameWorkflow.DataModel.Model with
            | :? FSharpCustomRenameModel as fsRenameModel -> fsRenameModel.ChangeNameKind
            | model ->

            let logger = Logger.GetLogger<FSharpCustomRenameModel>()
            logger.Warn(sprintf "Got custom rename model %O, workflow: %O" model x.RenameWorkflow)
            ChangeNameKind.SourceName

    type RenameDataModel with
        member x.FSharpRenameModel =
            x.Model :?> FSharpCustomRenameModel


type FSharpCustomRenameModel(declaredElement, reference, lifetime, changeNameKind: ChangeNameKind) =
    inherit ClrCustomRenameModel(declaredElement, reference, lifetime)

    member x.ChangeNameKind = changeNameKind


type FSharpAtomicRename(declaredElement, newName, doNotShowBindingConflicts) =
    inherit AtomicRename(declaredElement, newName, doNotShowBindingConflicts)

    override x.SetName(declaration, renameRefactoring) =
        match declaration with
        | :? IFSharpDeclaration as fsDeclaration ->
            fsDeclaration.SetName(x.NewName, renameRefactoring.Workflow.FSharpChangeNameKind)
        | declaration -> failwithf "Got declaration: %O" declaration


[<Language(typeof<FSharpLanguage>)>]
type FSharpRenameHelper() =
    inherit RenameHelperBase()

    override x.IsLanguageSupported = true

    override x.IsCheckResolvedTo(newReference, newDeclaredElement) =
        // To prevent FCS checking changed projects assume the new reference is resolved to the new element.
        // Binding non-F# elements isn't supported yet so assume it's not resolved.
        newDeclaredElement.PresentationLanguage.Is<FSharpLanguage>()

    override x.IsLocalRename(element: IDeclaredElement) =
        match element with
        | :? ILongIdentPat as longIdentPat -> longIdentPat.IsDeclaration
        | _ -> element :? IFSharpLocalDeclaration

    override x.CheckLocalRenameSameDocument(element: IDeclaredElement) = x.IsLocalRename(element)

    override x.GetSecondaryElements(element: IDeclaredElement) =
        match element with
        | :? ILocalNamedPat as localNamedPat ->
            let mutable pat = localNamedPat :> ISynPat
            while (pat.Parent :? ISynPat) && not (pat.Parent :? ILongIdentPat && (pat.Parent :?> ISynPat).IsDeclaration) do
                pat <- pat.Parent :?> ISynPat

            pat.Declarations
            |> Seq.cast<IDeclaredElement>
            |> Seq.filter (fun decl -> decl != element && decl.ShortName = element.ShortName)

        | _ -> EmptyArray.Instance :> _
    
    override x.GetOptionsModel(declaredElement, reference, lifetime) =
        FSharpCustomRenameModel(declaredElement, reference, lifetime, (* todo *) ChangeNameKind.SourceName) :> _

    override x.GetInitialPage(workflow) =
        let dataModel = workflow.RenameDataModel
        match dataModel.InitialDeclaredElement with
        | null -> null
        | element ->

        dataModel.InitialName <-
            match element.As<IFSharpDeclaredElement>() with
            | null -> element.ShortName
            | fsDeclaredElement ->

            match workflow.FSharpChangeNameKind with
            | ChangeNameKind.SourceName
            | ChangeNameKind.UseSingleName -> fsDeclaredElement.SourceName
            | _ -> fsDeclaredElement.ShortName

        null


[<ShellFeaturePart>]
type FSharpAtomicRenamesFactory() =
    inherit AtomicRenamesFactory()

    override x.IsApplicable(element: IDeclaredElement) =
        element.PresentationLanguage.Is<FSharpLanguage>()

    override x.CheckRenameAvailability(element: IDeclaredElement) =
        match element with
        | :? FSharpGeneratedMemberBase -> RenameAvailabilityCheckResult.CanNotBeRenamed
        | :? ISynPat as pat when not pat.IsDeclaration -> RenameAvailabilityCheckResult.CanNotBeRenamed

        | _ ->

        match element.ShortName with
        | SharedImplUtil.MISSING_DECLARATION_NAME -> RenameAvailabilityCheckResult.CanNotBeRenamed
        | name when IsActivePatternName name -> RenameAvailabilityCheckResult.CanNotBeRenamed
        | _ -> RenameAvailabilityCheckResult.CanBeRenamed

    override x.CreateAtomicRenames(declaredElement, newName, doNotAddBindingConflicts) =
        [| FSharpAtomicRename(declaredElement, newName, doNotAddBindingConflicts) :> AtomicRenameBase |] :> _


[<Language(typeof<FSharpLanguage>)>]
type FSharpNamingService(language: FSharpLanguage) =
    inherit NamingLanguageServiceBase(language)

    override x.MangleNameIfNecessary(name, _) =
        Keywords.QuoteIdentifierIfNeeded name
