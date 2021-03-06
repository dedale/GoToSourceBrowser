using NUnit.Framework;
using System;
using System.ComponentModel;
using System.IO;

namespace GoToSourceBrowser.Tests
{
    [TestFixture]
    internal sealed class ProcessRunnerTests
    {
        [Test]
        public void Test_OK()
        {
            var tempPath = Path.GetTempPath();
            var stdout = ProcessRunner.Exec("cmd", "/c echo in %cd% folder", tempPath);
            // \f generated by cls in Command Processor AutoRun
            stdout = stdout.TrimStart('\f').TrimEnd('\r', '\n');
            Assert.That(stdout, Is.EqualTo($"in {tempPath.TrimEnd(Path.DirectorySeparatorChar)} folder"));
        }

        [Test]
        public void Test_Error()
        {
            var stdout = ProcessRunner.Exec("cmd", "/c exit /B 128", null);
            Assert.IsNull(stdout);
        }

        [Test]
        public void Test_Exception()
        {
            Assert.Throws<Win32Exception>(() => ProcessRunner.Exec($"{Guid.NewGuid()}.exe", null, null));
        }
    }

    [TestFixture]
    internal sealed class GitTests
    {
        [TestCase("git@github.com:dedale/Repo")]
        [TestCase("git@github.com:dedale/Repo.git")]
        [TestCase("https://github.com/dedale/Repo")]
        [TestCase("https://github.com/dedale/Repo.git")]
        [TestCase("https://github.com/dedale/Repo/")]
        [TestCase("https://github.enterprise.com/org/Repo")]
        public void Test_OK(string remote)
        {
            Func<string, string, string, string> exec = (file, args, dir) => $"origin\t{remote} (fetch)\norigin\t{remote} (push)";
            var remotes = new Git(exec).GetRemoteRepositories(Path.GetTempPath());
            CollectionAssert.AreEqual(remotes, new[] { "Repo" });
        }

        [Test]
        public void Test_Error()
        {
            Func<string, string, string, string> exec = (file, args, dir) => null;
            var remotes = new Git(exec).GetRemoteRepositories(Path.GetTempPath());
            CollectionAssert.IsEmpty(remotes);
        }

        [Test]
        public void Test_Exception()
        {
            Func<string, string, string, string> exec = (file, args, dir) => throw new Win32Exception();
            var remotes = new Git(exec).GetRemoteRepositories(Path.GetTempPath());
            CollectionAssert.IsEmpty(remotes);
        }
    }
}
