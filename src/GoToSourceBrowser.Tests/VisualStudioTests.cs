using EnvDTE;
using EnvDTE80;
using FluentAssertions;
using Microsoft.VisualStudio.Shell;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread

namespace GoToSourceBrowser.Tests
{
    /// Hack to simulate that we are on UI thread
    internal static class ThreadHelper_uiThreadDispatcher
    {
        private static readonly FieldInfo field = GetField();

        private static FieldInfo GetField()
        {
            var field = typeof(ThreadHelper).GetField("uiThreadDispatcher", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field);
            return field;
        }

        public static void Setup()
        {
            Assert.IsNull(field.GetValue(null));
            field.SetValue(null, Dispatcher.CurrentDispatcher);
        }

        public static void TearDown()
        {
            Assert.IsNotNull(field.GetValue(null));
            field.SetValue(null, null);
        }
    }

    [TestFixture]
    internal sealed class VisualStudioTests : IDisposable
    {
        public VisualStudioTests()
        {
            ThreadHelper_uiThreadDispatcher.Setup();
        }
        public void Dispose()
        {
            ThreadHelper_uiThreadDispatcher.TearDown();
        }

        private static Dictionary<vsCMElement, Mock<CodeElement>> GetMocks(params vsCMElement[] scopes)
        {
            Mock<CodeElement> codeElement;
            var mocks = new Dictionary<vsCMElement, Mock<CodeElement>>();
            foreach (var scope in scopes)
            {
                switch (scope)
                {
                    case vsCMElement.vsCMElementNamespace:
                        AddMock(scope, "Namespace");
                        break;
                    case vsCMElement.vsCMElementClass:
                        AddMock(scope, "Namespace.Class");
                        break;
                    case vsCMElement.vsCMElementInterface:
                        AddMock(scope, "Namespace.IInterface");
                        break;
                    case vsCMElement.vsCMElementFunction:
                        AddMock(scope, "Namespace.Class.Method");
                        break;
                    case vsCMElement.vsCMElementParameter:
                        AddMock(scope, "arg");
                        break;
                    case vsCMElement.vsCMElementProperty:
                        AddMock(scope, "Namespace.Class.Property");
                        break;
                    case vsCMElement.vsCMElementImportStmt:
                        codeElement = new Mock<CodeElement>(MockBehavior.Strict);
                        codeElement.Setup(x => x.Kind).Returns(scope);
                        codeElement.Setup(x => x.FullName).Throws<COMException>();
                        codeElement.Setup(x => x.Name).Throws<COMException>();
                        mocks.Add(scope, codeElement);
                        break;
                    case vsCMElement.vsCMElementAttribute:
                        AddMock(scope, "OtherNamespace.Attribute");
                        break;
                    default:
                        throw new NotImplementedException($"Scope '{scope}' not implemented");
                }
            }
            return mocks;

            void AddMock(vsCMElement scope, string fullName)
            {
                codeElement = new Mock<CodeElement>(MockBehavior.Strict);
                codeElement.Setup(x => x.Kind).Returns(scope);
                codeElement.Setup(x => x.FullName).Returns(fullName);
                codeElement.Setup(x => x.Name).Returns(fullName.Split('.').Last());
                mocks.Add(scope, codeElement);
            }
        }
        private static void Test_CodeElements(Dictionary<vsCMElement, Mock<CodeElement>> mocks, CodeElementInfo expected)
        {
            var virtualPoint = new Mock<VirtualPoint>(MockBehavior.Strict);
            var selection = new Mock<TextSelection>(MockBehavior.Strict);
            selection.Setup(x => x.ActivePoint).Returns(virtualPoint.Object);
            var fileCodeModel = new Mock<FileCodeModel2>(MockBehavior.Strict);
            var scopes = new[]
            {
                vsCMElement.vsCMElementAttribute,
                vsCMElement.vsCMElementFunction,
                vsCMElement.vsCMElementVariable,
                vsCMElement.vsCMElementProperty,
                vsCMElement.vsCMElementInterface,
                vsCMElement.vsCMElementDelegate,
                vsCMElement.vsCMElementEnum,
                vsCMElement.vsCMElementStruct,
                vsCMElement.vsCMElementEvent,
                vsCMElement.vsCMElementClass,
                vsCMElement.vsCMElementNamespace,
                vsCMElement.vsCMElementModule,
                vsCMElement.vsCMElementOther
            };
            Mock<CodeElement> codeElement = null;
            foreach (var scope in scopes)
            {
                if (mocks.TryGetValue(scope, out var mock))
                    fileCodeModel.Setup(x => x.CodeElementFromPoint(virtualPoint.Object, scope)).Returns(mock.Object);
                else
                    fileCodeModel.Setup(x => x.CodeElementFromPoint(virtualPoint.Object, scope)).Throws<COMException>();
            }
            var project = new Mock<Project>(MockBehavior.Strict);
            project.Setup(x => x.Kind).Returns(VisualStudio.CSharpProjectKind);
            var projectItem = new Mock<ProjectItem>(MockBehavior.Strict);
            projectItem.Setup(x => x.ContainingProject).Returns(project.Object);
            projectItem.Setup(x => x.FileCodeModel).Returns(fileCodeModel.Object);
            var document = new Mock<Document>(MockBehavior.Strict);
            document.Setup(x => x.ProjectItem).Returns(projectItem.Object);
            document.Setup(x => x.Selection).Returns(selection.Object);
            var dte = new Mock<DTE>(MockBehavior.Strict);
            dte.Setup(x => x.ActiveDocument).Returns(document.Object);
            var visualStudio = new VisualStudio(dte.Object);
            var element = visualStudio.GetCurrentCodeElement();
            element.Should().BeEquivalentTo(expected);
            dte.VerifyAll();
            document.VerifyAll();
            projectItem.VerifyAll();
            project.VerifyAll();
            fileCodeModel.VerifyAll();
            selection.VerifyAll();
            virtualPoint.VerifyAll();
            codeElement?.VerifyAll();
        }

        [Test]
        public void Test_Namespace()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementNamespace, "Namespace", "Namespace");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_Class()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementClass);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementClass, "Namespace.Class", "Class");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_Method()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementClass, vsCMElement.vsCMElementFunction);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementFunction, "Namespace.Class.Method", "Method");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_Property()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementClass, vsCMElement.vsCMElementProperty);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementProperty, "Namespace.Class.Property", "Property");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_MethodParameter()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementClass, vsCMElement.vsCMElementFunction, vsCMElement.vsCMElementParameter);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementFunction, "Namespace.Class.Method", "Method");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_Interface()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementInterface);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementInterface, "Namespace.IInterface", "IInterface");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_MethodAttribute()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementNamespace, vsCMElement.vsCMElementClass, vsCMElement.vsCMElementFunction, vsCMElement.vsCMElementAttribute);
            var expected = new CodeElementInfo(vsCMElement.vsCMElementAttribute, "OtherNamespace.Attribute", "Attribute");
            Test_CodeElements(mocks, expected);
        }
        [Test]
        public void Test_Using()
        {
            var mocks = GetMocks(vsCMElement.vsCMElementImportStmt);
            Test_CodeElements(mocks, null);
        }
    }
}
