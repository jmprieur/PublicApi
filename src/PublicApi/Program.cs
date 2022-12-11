using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Reflection;

namespace PublicApi
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            MSBuildLocator.RegisterDefaults();
            string solutionPath = (args.Length>0 ? args[0] : null) ?? @"C:\gh\microsoft-identity-abstractions-for-dotnet\Microsoft.Identity.Abstractions.sln";
            //         string solutionPath = @"C:\gh\microsoft-identity-web\Microsoft.Identity.Web.sln";

            var msWorkspace = MSBuildWorkspace.Create();

            Console.WriteLine("Reading solution");
            var solution = await msWorkspace.OpenSolutionAsync(solutionPath);
            Console.WriteLine("Done");

            Console.WriteLine("Parsing");
            var compilations = await Task.WhenAll(solution.Projects.Where(p => p.FilePath!.Contains("src")).Select(x => x.GetCompilationAsync()));
            Console.WriteLine("Done");

            // Reference: https://codereview.stackexchange.com/questions/84932/using-roslyn-to-find-interfaces-within-a-solution
            var typesByFullName = compilations
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

            foreach (ITypeSymbol type in typesByFullName.Where(t => IsPublicApi(t)))
            {
                Console.WriteLine($"{type.DeclaredAccessibility.ToString().ToLower()} {type.TypeKind.ToString().ToLower()} {type.ToDisplayString()}");
                Console.WriteLine("{");
                foreach (ISymbol child in type.GetMembers().Where(m => IsPublicApi(m)).OrderBy(c => c.ToDisplayString()))
                {
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
                            Console.Write($"{DisplaySimplifiedName(method.ReturnType, method)} ");
                        }
                        Console.WriteLine($"{DisplaySimplifiedName(method)};");
                    }

                    IPropertySymbol? property = child as IPropertySymbol;
                    if (property != null)
                    {
                        Console.Write($"{DisplaySimplifiedName(property.Type, property)} ");

                        Console.Write($"{DisplaySimplifiedName(property)} ");
                        Console.Write("{");
                        if (property.GetMethod != null && IsVisibleInPublicApi(property.GetMethod)) { Console.Write("set; "); }
                        if (property.SetMethod != null && IsVisibleInPublicApi(property.SetMethod)) { Console.Write("get; "); }
                        Console.WriteLine("}");
                    }

                    IFieldSymbol? field = child as IFieldSymbol;
                    if (field != null)
                    {
                        Console.Write($"{DisplaySimplifiedName(field)}");
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

        private static string DisplaySimplifiedName(ISymbol symbol, ISymbol? type = null)
        {
            type = type ?? symbol;
            if (type != null)
            {
                return symbol.ToDisplayString().Replace(type.ContainingType.ToDisplayString() + ".", string.Empty).Replace(type.ContainingNamespace.ToDisplayString() + ".", string.Empty);
            }
            else
                return symbol.ToDisplayString();
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
    }
}