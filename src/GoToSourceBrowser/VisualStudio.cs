using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Serilog;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

namespace GoToSourceBrowser
{
    internal sealed class CodeElementInfo
    {
        public CodeElementInfo(vsCMElement kind, string fullName, string name)
        {
            Kind = kind;
            FullName = fullName;
            Name = name;
        }

        public vsCMElement Kind { get; }
        public string FullName { get; }
        public string Name { get; }
    }

    internal interface IVisualStudio
    {
        CodeElementInfo GetCurrentCodeElement();
    }

    internal sealed class VisualStudio : IVisualStudio
    {
        internal const string CSharpProjectKind = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private static readonly ImmutableArray<vsCMElement> scopes = GetScopes();
        private readonly DTE dte;

        private static ImmutableArray<vsCMElement> GetScopes()
        {
            return ImmutableArray.Create(
                // attribute before everything
                vsCMElement.vsCMElementAttribute,
                // parameter before function but not supported by Source Browser
                //vsCMElement.vsCMElementParameter,

                vsCMElement.vsCMElementFunction,
                vsCMElement.vsCMElementVariable,
                vsCMElement.vsCMElementProperty,
                vsCMElement.vsCMElementInterface,
                vsCMElement.vsCMElementDelegate,
                vsCMElement.vsCMElementEnum,
                vsCMElement.vsCMElementStruct,

                // C++
                //vsCMElement.vsCMElementUnion,

                // VB
                //vsCMElement.vsCMElementLocalDeclStmt, ?
                //vsCMElement.vsCMElementFunctionInvokeStmt, ?
                //vsCMElement.vsCMElementPropertySetStmt,
                //vsCMElement.vsCMElementAssignmentStmt,
                //vsCMElement.vsCMElementInheritsStmt,
                //vsCMElement.vsCMElementImplementsStmt,
                //vsCMElement.vsCMElementOptionStmt,
                //vsCMElement.vsCMElementVBAttributeStmt,
                //vsCMElement.vsCMElementVBAttributeGroup,
                //vsCMElement.vsCMElementEventsDeclaration,

                // C++
                //vsCMElement.vsCMElementUDTDecl,
                //vsCMElement.vsCMElementDeclareDecl,
                //vsCMElement.vsCMElementDefineStmt,
                //vsCMElement.vsCMElementTypeDef,
                //vsCMElement.vsCMElementIncludeStmt,
                //vsCMElement.vsCMElementUsingStmt,
                //vsCMElement.vsCMElementMacro,
                //vsCMElement.vsCMElementMap,
                //vsCMElement.vsCMElementIDLImport,
                //vsCMElement.vsCMElementIDLImportLib,
                //vsCMElement.vsCMElementIDLCoClass,
                //vsCMElement.vsCMElementIDLLibrary,

                // C# using but namespace not available
                //vsCMElement.vsCMElementImportStmt,

                // C++
                //vsCMElement.vsCMElementMapEntry, ?
                //vsCMElement.vsCMElementVCBase,
                vsCMElement.vsCMElementEvent,

                // Class after child elements
                vsCMElement.vsCMElementClass,
                // Namespace after class
                vsCMElement.vsCMElementNamespace,

                vsCMElement.vsCMElementModule,

                // Moved last (fallback)
                vsCMElement.vsCMElementOther
            );
        }

        public CodeElementInfo GetCurrentCodeElement()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // https://www.mztools.com/articles/2006/MZ2006009.aspx

                var activeDocument = dte.ActiveDocument;
                if (activeDocument == null)
                {
                    Log.Verbose("No active document");
                    return null;
                }
                var project = activeDocument.ProjectItem?.ContainingProject;
                if (project == null || project.Kind != CSharpProjectKind)
                {
                    Log.Verbose($"Not a C# project ('{project?.Kind}' is not '{CSharpProjectKind}').");
                    return null;
                }
                var projectItem = activeDocument.ProjectItem;
                var fileCodeModel = projectItem.FileCodeModel as FileCodeModel2;
                if (fileCodeModel == null)
                {
                    Log.Verbose($"{nameof(FileCodeModel2)} is null.");
                    return null;
                }
                var selection = (TextSelection)activeDocument.Selection;
                var activePoint = selection?.ActivePoint;
                if (activePoint == null)
                {
                    Log.Verbose($"{nameof(VirtualPoint)} is null.");
                    return null;
                }
                var elements = scopes.Select(TryGetCodeElement).Where(x => x != null).ToList();
                Log.Verbose($"Found {elements.Count} element{(elements.Count > 1 ? "s" : "")}");
                foreach (var element in elements)
                    Log.Verbose($"- {element.FullName ?? element.Name}: {element.Kind}");
                return elements.FirstOrDefault();

                CodeElementInfo TryGetCodeElement(vsCMElement scope)
                {
                    try
                    {
                        var element = fileCodeModel.CodeElementFromPoint(activePoint, scope);
                        string name = null;
                        try
                        {
                            name = element.Name;
                        }
                        catch (COMException)
                        {
                        }
                        string fullName = null;
                        try
                        {
                            fullName = element.FullName;
                        }
                        catch (COMException)
                        {
                        }
                        var kind = vsCMElement.vsCMElementOther;
                        try
                        {
                            kind = element.Kind;
                        }
                        catch (COMException)
                        {
                        }
                        return new CodeElementInfo(kind, fullName, name);
                    }
                    catch (COMException)
                    {
                        return null;
                    }
                }
            }
            catch (COMException e)
            {
                Log.Error(e, $"Error in {nameof(GetCurrentCodeElement)}.");
                return null;
            }
            catch (ArgumentException)
            {
                // Might happen for field attributes
                return null;
            }
        }

        public VisualStudio(DTE dte)
        {
            this.dte = dte;
        }
    }
}
