﻿using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Moq;
using ScriptCs.Contracts;
using ScriptCs.Engine.Roslyn;
using Should;
using Xunit;
using Ploeh.AutoFixture.Xunit2;

namespace ScriptCs.Tests
{
    public class CSharpScriptEngineTests
    {
        public class TheExecuteMethod
        {
            private IConsole _console = new Mock<IConsole>().Object;
            private IObjectSerializer _serializer = new Mock<IObjectSerializer>().Object;
            private Printers _printers;

            public TheExecuteMethod()
            {
                _printers =  new Printers(_serializer);
            }

            [Theory, ScriptCsAutoData]
            public void ShouldCreateScriptHostWithContexts(
                [Frozen] Mock<IScriptHostFactory> scriptHostFactory,
                [Frozen] Mock<IScriptPack> scriptPack,
                ScriptPackSession scriptPackSession,
                [NoAutoProperties] CSharpScriptEngine engine)
            {
                // Arrange
                const string Code = "var a = 0;";

                scriptHostFactory.Setup(f => f.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()))
                    .Returns<IScriptPackManager, string[]>((p, q) => new ScriptHost(p, new ScriptEnvironment(q, _console, _printers)));

                scriptPack.Setup(p => p.Initialize(It.IsAny<IScriptPackSession>()));
                scriptPack.Setup(p => p.GetContext()).Returns((IScriptPackContext)null);

                // Act
                engine.Execute(Code, new string[0], new AssemblyReferences(), Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                scriptHostFactory.Verify(f => f.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()));
            }

            [Theory, ScriptCsAutoData]
            public void ShouldReuseExistingSessionIfProvided(
                [Frozen] Mock<IScriptHostFactory> scriptHostFactory,
                [NoAutoProperties] CSharpTestScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var a = 0;";

                scriptHostFactory.Setup(f => f.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()))
                    .Returns<IScriptPackManager, string[]>((p, q) => new ScriptHost(p, new ScriptEnvironment(q, _console, _printers)));

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;

                // Act
                engine.Execute(Code, new string[0], new AssemblyReferences(), Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                engine.Session.ShouldEqual(session.Session);
            }

            [Fact]
            public void ShouldCreateNewSessionIfNotProvided()
            {
                var scriptHostFactory = new Mock<IScriptHostFactory>();
                scriptHostFactory.Setup(f => f.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()))
    .Returns<IScriptPackManager, string[]>((p, q) => new ScriptHost(p, new ScriptEnvironment(q, _console, _printers)));

                // Arrange
                var engine = new CSharpTestScriptEngine(scriptHostFactory.Object, new TestLogProvider());
                const string Code = "var a = 0;";

                // Act
                engine.Execute(Code, new string[0], new AssemblyReferences(), Enumerable.Empty<string>(), new ScriptPackSession(new IScriptPack[0], new string[0]));

                // Assert
                engine.Session.ShouldNotBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldAddNewReferencesIfTheyAreProvided(
                [NoAutoProperties] CSharpTestScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var a = 0;";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                ((SessionState<ScriptState>)scriptPackSession.State[CommonScriptEngine.SessionKey]).References.Paths.Count().ShouldEqual(2);
            }

            [Theory, ScriptCsAutoData]
            public void ShouldReturnAScriptResult(
                [NoAutoProperties] CSharpTestScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                var code = string.Empty;

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.ShouldBeType<ScriptResult>();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldReturnCompileExceptionIfCodeDoesNotCompile(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "this shold not compile";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.CompileExceptionInfo.ShouldNotBeNull();
            }

            [Theory(Skip = "this feature is not supported in Roslyn 1.0.0-rc2: see https://github.com/dotnet/roslyn/issues/1012"), ScriptCsAutoData]
            public void ShouldReturnInvalidNamespacesIfCS0241Encountered(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;

                // Act
                var result = engine.Execute(string.Empty, new string[0], new AssemblyReferences(), new[] { "foo" }, scriptPackSession);

                // Assert
                result.CompileExceptionInfo.ShouldNotBeNull();
                result.InvalidNamespaces.ShouldContain("foo");
            }

            //todo: filip: this test will not even compile as it used to do reflection old roslyn assemblies!
            //[Theory, ScriptCsAutoData]
            //public void ShouldRemoveInvalidNamespacesFromSessionStateAndEngine(
            //    [Frozen] Mock<IScriptHostFactory> scriptHostFactory,
            //    [NoAutoProperties] CSharpScriptEngine engine,
            //    ScriptPackSession scriptPackSession)
            //{
            //    var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
            //    scriptPackSession.State[CommonScriptEngine.SessionKey] = session;

            //    // Act
            //    engine.Execute(string.Empty, new string[0], new AssemblyReferences(), new[] { "System", "foo" }, scriptPackSession);

            //    // Assert
            //    session.Namespaces.ShouldNotBeEmpty();
            //    session.Namespaces.ShouldNotContain("foo");
            //    var pendingNamespacesField = session.Session.GetType().GetField("pendingNamespaces", BindingFlags.Instance | BindingFlags.NonPublic);
            //    var pendingNamespacesValue = (ReadOnlyArray<string>)pendingNamespacesField.GetValue(session.Session);
            //    pendingNamespacesValue.AsEnumerable().ShouldNotBeEmpty();
            //    pendingNamespacesValue.AsEnumerable().ShouldNotContain("foo");
            //}

            [Theory, ScriptCsAutoData]
            public void ShouldNotReturnCompileExceptionIfCodeDoesCompile(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var theNumber = 42; //this should compile";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.CompileExceptionInfo.ShouldBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldReturnExecuteExceptionIfCodeExecutionThrowsException(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "throw new System.Exception(); //this should throw an Exception";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.ExecuteExceptionInfo.ShouldNotBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldNotReturnExecuteExceptionIfCodeExecutionDoesNotThrowAnException(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var theNumber = 42; //this should not throw an Exception";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.ExecuteExceptionInfo.ShouldBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldReturnReturnValueIfCodeExecutionReturnsValue(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                const string Code = "\"Hello\" //this should return \"Hello\"";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.ReturnValue.ShouldEqual("Hello");
            }

            [Theory, ScriptCsAutoData]
            public void ShouldNotReturnReturnValueIfCodeExecutionDoesNotReturnValue(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var theNumber = 42; //this should not return a value";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.ReturnValue.ShouldBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldNotMarkSubmissionsAsIncompleteWhenRunningScript(
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "class test {";

                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });
                engine.FileName = "test.csx";

                // Act
                var result = engine.Execute(
                    Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.IsCompleteSubmission.ShouldBeTrue();
                result.CompileExceptionInfo.ShouldNotBeNull();
            }

            [Theory, ScriptCsAutoData]
            public void ShouldCompileWhenUsingClassesFromAPassedAssemblyInstance(
                [Frozen] Mock<IScriptHostFactory> scriptHostFactory,
                [Frozen] ScriptPackSession scriptPackSession)
            {
                // Arrange
                const string Code = "var x = new ScriptCs.Tests.TestMarkerClass();";

                scriptHostFactory.Setup(f => f.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()))
                    .Returns<IScriptPackManager, string[]>((p, q) => new ScriptHost(p, new ScriptEnvironment(q, _console, _printers)));

                var engine = new CSharpScriptEngine(scriptHostFactory.Object, new TestLogProvider());
                var session = new SessionState<ScriptState> { Session = CSharpScript.RunAsync("").GetAwaiter().GetResult() };
                scriptPackSession.State[CommonScriptEngine.SessionKey] = session;
                var refs = new AssemblyReferences(new[] { Assembly.GetExecutingAssembly() }, new[] { "System", "System.Runtime" });

                // Act
                var result = engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                result.CompileExceptionInfo.ShouldBeNull();
                result.ExecuteExceptionInfo.ShouldBeNull();
            }


            [Theory, ScriptCsAutoData]
            public void ShouldInitializeScriptLibraryWrapperHost(
                [Frozen] Mock<IScriptHostFactory> scriptHostFactory,
                Mock<IScriptPackManager> manager,
                [NoAutoProperties] CSharpScriptEngine engine,
                ScriptPackSession scriptPackSession
                )
            {
                // Arrange
                const string Code = "var theNumber = 42; //this should compile";

                var refs = new AssemblyReferences(new[] { "System", "System.Runtime" });

                scriptHostFactory.Setup(s => s.CreateScriptHost(It.IsAny<IScriptPackManager>(), It.IsAny<string[]>()))
                    .Returns(new ScriptHost(manager.Object, null));

                // Act
                engine.Execute(Code, new string[0], refs, Enumerable.Empty<string>(), scriptPackSession);

                // Assert
                ScriptLibraryWrapper.ScriptHost.ShouldNotEqual(null);
            }
        }

        public class CSharpTestScriptEngine : CSharpScriptEngine
        {
            public CSharpTestScriptEngine(IScriptHostFactory scriptHostFactory, TestLogProvider logProvider)
                : base(scriptHostFactory, logProvider)
            {
            }

            public ScriptState Session { get; private set; }

            protected override ScriptResult Execute(string code, object globals, SessionState<ScriptState> sessionState)
            {
                base.Execute(code, globals, sessionState);
                Session = sessionState.Session;
                return ScriptResult.Empty;
            }
        }
    }

    public class TestMarkerClass { }
}
