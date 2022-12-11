using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.Concurrent;

namespace PublicApi
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();
             //   string solutionPath = (args.Length>0 ? args[0] : null) ?? @"C:\gh\microsoft-identity-abstractions-for-dotnet\Microsoft.Identity.Abstractions.sln";
            string solutionPath = @"C:\gh\microsoft-identity-web\Microsoft.Identity.Web.sln";

            var msWorkspace = MSBuildWorkspace.Create();

            Console.WriteLine("Reading solution");
            var solution = await msWorkspace.OpenSolutionAsync(solutionPath);
            Console.WriteLine("Done");

            Console.WriteLine("Parsing");
            var compilations = await Task.WhenAll(solution.Projects.Where(p => p.FilePath!.Contains("src")).Select(p => p.GetCompilationAsync()));
            Console.WriteLine("Done");

#pragma warning disable RS1024 // Symbols should be compared for equality. Here we want to compare the references.
            ConcurrentDictionary<IAssemblySymbol, Project> projectOfCompilation = new ConcurrentDictionary<IAssemblySymbol, Project>();
#pragma warning restore RS1024 // Symbols should be compared for equality
            foreach (Project p in solution.Projects.Where(p => p.FilePath!.Contains("src")))
            {

                // MULTIPLE PROJECTS FOR ASSEMBLY? (MicrosoftGraphBeta)

                projectOfCompilation.TryAdd(p.GetCompilationAsync().Result!.Assembly, p);
            }

            var types = compilations
              .SelectMany(compilation => compilation!.SyntaxTrees.Select(tree => new { Compilation = compilation!, SyntaxTree = tree }))
              .Select(data => data.Compilation.GetSemanticModel(data.SyntaxTree))
              .SelectMany(
                  semanticModel => semanticModel
                      .SyntaxTree
                      .GetRoot()
                      .DescendantNodes()
                      .OfType<BaseTypeDeclarationSyntax>()
                      .Select(t => semanticModel.GetDeclaredSymbol(t))
                      .OfType<ITypeSymbol>()
                      )
                      .ToList()
                      .OrderBy(t => t.ToDisplayString());

            var typesByFullName = types.GroupBy(t => t.ToDisplayString());


            foreach (var g in typesByFullName)
            {
                IEnumerable<string> projectStrings = g
                    .Where(t=>IsPublicApi(t))
                    .Select(t => GetTargetFramework(t, projectOfCompilation))
                    .Distinct()
                    .ToArray();
                foreach (var type in g.Where(t => IsPublicApi(t)).Take(1))
                {
                    Console.WriteLine();
                    Console.WriteLine($"// {type.ContainingAssembly.Name} ({string.Join(", ", projectStrings)})");

                    // Write the attributes
                    WriteAttributes(type.GetAttributes());
                    Console.WriteLine($"{type.DeclaredAccessibility.ToString().ToLower()} {type.ToDisplayString(format)}");

                    /// Interfaces have no base type
                    List<ITypeSymbol> bases = new List<ITypeSymbol>();
                    if (type.BaseType != null && type.BaseType.ToDisplayString() != "object")
                    {
                        bases.Add(type.BaseType);
                    }
                    if (type.Interfaces != null && type.Interfaces.Any())
                    {
                        bases.AddRange(type.Interfaces);
                    }
                    if (bases.Count > 0)
                    {
                        Console.WriteLine($" : {string.Join(", ", bases.Select(i => DisplaySimplifiedName(format, i).Replace("class ", string.Empty).Replace("interface ", string.Empty)))}");
                    }

                    Console.WriteLine("{");
                    foreach (ISymbol child in type.GetMembers().Where(m => IsPublicApi(m)).OrderBy(c => c.ToDisplayString()))
                    {
                        WriteAttributes(child.GetAttributes(), " ");
                        Console.Write($" {child.DeclaredAccessibility.ToString().ToLower()} ");

                        if (type.BaseType == null || type.BaseType.ToDisplayString() != "System.Enum")
                        {
                            if (child.IsStatic) { Console.Write("static "); }
                            if (child.IsSealed) { Console.Write("sealed "); }
                            if (child.IsAbstract) { Console.Write("abstract "); }
                            if (child.IsVirtual) { Console.Write("virtual "); }
                            if (child.IsOverride) { Console.Write("override "); }
                        }

                        IMethodSymbol? method = child as IMethodSymbol;
                        if (method != null)
                        {
                            if (method.MethodKind != MethodKind.Constructor && method.MethodKind != MethodKind.StaticConstructor)
                            {
                                Console.Write($"{DisplaySimplifiedName(format, method.ReturnType, method)} ");
                            }
                            Console.WriteLine($"{DisplaySimplifiedName(format, method)};");
                        }

                        IPropertySymbol? property = child as IPropertySymbol;
                        if (property != null)
                        {
                            Console.Write($"{DisplaySimplifiedName(format, property.Type, property)} ");

                            Console.Write($"{DisplaySimplifiedName(format, property)} ");
                            Console.Write("{");
                            if (property.GetMethod != null && IsVisibleInPublicApi(property.GetMethod)) { Console.Write("set; "); }
                            if (property.SetMethod != null && IsVisibleInPublicApi(property.SetMethod)) { Console.Write("get; "); }
                            Console.WriteLine("}");
                        }

                        IFieldSymbol? field = child as IFieldSymbol;
                        if (field != null)
                        {
                            Console.Write($"{DisplaySimplifiedName(format, field)}");
                            if (field.HasConstantValue)
                            {
                                Console.WriteLine($" = {field.ConstantValue};");
                            }
                            else
                            {
                                Console.WriteLine(";");
                            }
                        }
                    }
                    Console.WriteLine("}");
                }
            }
        }

        private static void WriteAttributes(ICollection<AttributeData>? attributes, string margin="")
        {
            if (attributes != null && attributes.Count > 0)
            {
                Console.WriteLine(margin+"[" + string.Join("]\n[", attributes
                    .Select(a => a.ToString()
                            .Replace("Attribute(", string.Empty)
                            .Replace("Attribute]", string.Empty)
                            .Replace("System.", string.Empty))
                            .ToArray()) + "]");
            }
        }

        private static string GetTargetFramework(ITypeSymbol t, ConcurrentDictionary<IAssemblySymbol, Project> projectOfCompilation)
        {
            string targetFramework = projectOfCompilation[t.ContainingAssembly].Name.Substring(t.ContainingAssembly.Name.Length).Trim(')', '(', ' ');
            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                targetFramework = Path.GetFileName(Path.GetDirectoryName(projectOfCompilation[t.ContainingAssembly].OutputFilePath));
            }
            return targetFramework;
        }

        private static string DisplaySimplifiedName(SymbolDisplayFormat format, ISymbol symbol, ISymbol? type = null)
        {
            type = type ?? symbol;
            if (type != null && type.ContainingType!=null)
            {
                return symbol.ToDisplayString().Replace(type.ContainingType.ToDisplayString(format) + ".", string.Empty).Replace(type.ContainingNamespace.ToDisplayString() + ".", string.Empty);
            }
            else
                return symbol.ToDisplayString(format);
        }

        private static bool IsPublicApi(ISymbol symbol)
        {
            IMethodSymbol? method = symbol as IMethodSymbol;
            if (method != null)
            {
                if (method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.PropertySet)
                {
                    return false;
                }
            }
            return IsVisibleInPublicApi(symbol);

        }
        private static bool IsVisibleInPublicApi(ISymbol m)
        {
            return (m.DeclaredAccessibility == Accessibility.Public || m.DeclaredAccessibility == Accessibility.Protected)
                && !m.IsImplicitlyDeclared;
        }

        private static SymbolDisplayFormat format = new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeExplicitInterface|SymbolDisplayMemberOptions.IncludeRef,
            SymbolDisplayDelegateStyle.NameAndSignature,
            SymbolDisplayExtensionMethodStyle.StaticMethod,
            SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeDefaultValue,
            SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            SymbolDisplayLocalOptions.IncludeRef | SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue,
            SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword,
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral
            );
        private static SymbolDisplayFormat format2 = new SymbolDisplayFormat(SymbolDisplayGlobalNamespaceStyle.Omitted,
            SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
            SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeModifiers,
            SymbolDisplayDelegateStyle.NameAndSignature,
            SymbolDisplayExtensionMethodStyle.StaticMethod,
            SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeDefaultValue,
            SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
            SymbolDisplayLocalOptions.IncludeRef | SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue,
            SymbolDisplayKindOptions.IncludeTypeKeyword | SymbolDisplayKindOptions.IncludeMemberKeyword,
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNotNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral
            );

    }
}