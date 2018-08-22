﻿using System;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Plugins.FSharp.Common.Naming;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.DeclaredElement;
using JetBrains.ReSharper.Plugins.FSharp.Psi.Tree;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;
using Microsoft.FSharp.Compiler.SourceCodeServices;

namespace JetBrains.ReSharper.Plugins.FSharp.Psi.Impl.Tree
{
  internal partial class MemberDeclaration : IFunctionDeclaration
  {
    IFunction IFunctionDeclaration.DeclaredElement => base.DeclaredElement as IFunction;
    protected override FSharpName GetFSharpName() => Identifier.GetFSharpName(Attributes);

    public override TreeTextRange GetNameRange()
    {
      return Identifier.GetNameRange();
    }

    protected override IDeclaredElement CreateDeclaredElement()
    {
      if (!(GetFSharpSymbol() is FSharpMemberOrFunctionOrValue mfv)) return null;

      if (mfv.IsProperty)
        return new FSharpProperty<MemberDeclaration>(this, mfv);

      var property = mfv.AccessorProperty?.Value;
      if (property != null)
      {
        var cliEvent = property.EventForFSharpProperty?.Value;
        return cliEvent != null
          ? (ITypeMember) new FSharpCliEvent<MemberDeclaration>(this, cliEvent)
          : new FSharpProperty<MemberDeclaration>(this, property);
      }

      var compiledName = mfv.CompiledName;
      var typeDeclaration = GetContainingTypeDeclaration() as IFSharpTypeDeclaration;
      if (!mfv.IsInstanceMember && compiledName.StartsWith("op_", StringComparison.Ordinal))
      {
        switch (compiledName)
        {
          case StandardOperatorNames.Explicit:
            return new FSharpConversionOperator<MemberDeclaration>(this, mfv, typeDeclaration, true);
          case StandardOperatorNames.Implicit:
            return new FSharpConversionOperator<MemberDeclaration>(this, mfv, typeDeclaration, false);
        }

        return new FSharpSignOperator<MemberDeclaration>(this, mfv, null);
      }
      return new FSharpMethod<MemberDeclaration>(this, mfv, typeDeclaration);
    }

    public override void SetName(string name) =>
      Identifier.ReplaceIdentifier(name);
  }
}
