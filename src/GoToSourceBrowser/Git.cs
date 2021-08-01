using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GoToSourceBrowser
{
    internal interface IGit
    {
        ISet<string> GetRemoteRepositories(string pathInRepo);
    }

    public static class ProcessRunner
    {
        public static string Exec(string file, string args, string workingDir)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = file;
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.WorkingDirectory = workingDir;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode == 0)
                    return process.StandardOutput.ReadToEnd();
            }
            return null;
        }
    }

    internal sealed class Git : IGit
    {
        private readonly Func<string, string, string, string> exec;

        public Git(Func<string, string, string, string> exec = null)
        {
            this.exec = exec ?? ProcessRunner.Exec;
        }

        public ISet<string> GetRemoteRepositories(string pathInRepo)
        {
            Log.Debug($"Getting remotes for {pathInRepo}...");
            try
            {
                var stdout = exec("git", "remote -v", Path.GetDirectoryName(pathInRepo) ?? ".");
                if (stdout != null)
                    return stdout
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line =>
                            line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault()?.Split(' ').FirstOrDefault())
                        .Where(x => x != null)
                        .Distinct()
                        // Fix not supported URI Scheme for ssh
                        .Select(x => Path.GetFileNameWithoutExtension(x.TrimEnd('/').Split('/').Last()))
                        .ToHashSet();
            }
            catch (Win32Exception e)
            {
                Log.Warning(e, "'git remote -v' failed.");
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get git remotes.");
            }
            return new HashSet<string>();
        }
    }
}
