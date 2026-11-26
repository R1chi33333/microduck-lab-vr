using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Lets command-line tooling drive an open editor. Every half second it looks for
    /// Temp/microduck_cmd.txt; each line is one command:
    ///   refresh                       AssetDatabase.Refresh (imports changed files, recompiles)
    ///   method Namespace.Class.Method run a static method with no arguments
    ///   menu Microduck/...            run a menu item
    ///   play / stop                   enter or leave Play mode
    /// Results go to Logs/microduck_cmd.log; compile errors are listed there too.
    /// </summary>
    [InitializeOnLoad]
    public static class RemoteCommands
    {
        static readonly string CmdPath = Path.GetFullPath("Temp/microduck_cmd.txt");
        static readonly string LogPath = Path.GetFullPath("Logs/microduck_cmd.log");
        static double next;

        static RemoteCommands()
        {
            EditorApplication.update += Tick;
            CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
            {
                foreach (var m in msgs)
                    if (m.type == CompilerMessageType.Error) Log("COMPILE ERROR " + m.message);
            };
            CompilationPipeline.compilationFinished += _ => Log("compile finished");
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (msg.Contains("EnvironmentDependencyValues") || msg.Contains("graphicsApiMask")) return;
                if (msg.Contains("[Microduck]")) Log(msg);
                else if (type == LogType.Exception || type == LogType.Error) Log(type + " " + msg + "\n" + stack);
            };
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(CmdPath)) return;
            string[] lines;
            try { lines = File.ReadAllLines(CmdPath); File.Delete(CmdPath); }
            catch (IOException) { return; }
            // a batch carries an id; a batch already run (seen again after a domain reload) is skipped
            if (lines.Length > 0 && lines[0].StartsWith("#id "))
            {
                string id = lines[0].Substring(4).Trim();
                if (SessionState.GetString("microduck_cmd_last", "") == id) return;
                SessionState.SetString("microduck_cmd_last", id);
            }
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                try { Run(line); }
                catch (Exception e) { Log("FAILED " + line + " : " + (e.InnerException ?? e)); }
            }
        }

        static void Run(string line)
        {
            Log("> " + line);
            if (line == "refresh") { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); return; }
            if (line == "play") { EditorApplication.isPlaying = true; return; }
            if (line == "stop") { EditorApplication.isPlaying = false; return; }
            if (line.StartsWith("menu ")) { Log(EditorApplication.ExecuteMenuItem(line.Substring(5)) ? "ok" : "menu not found"); return; }
            if (line.StartsWith("method "))
            {
                string full = line.Substring(7);
                int dot = full.LastIndexOf('.');
                string typeName = full.Substring(0, dot), method = full.Substring(dot + 1);
                Type t = null;
#pragma warning disable UAC0005
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { t = a.GetType(typeName); if (t != null) break; }
#pragma warning restore UAC0005
                if (t == null) { Log("type not found: " + typeName); return; }
                // the overload with no parameters (a plain GetMethod throws when there are several)
                var mi = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (mi == null) { Log("method not found: " + full); return; }
                mi.Invoke(null, null);
                Log("ok");
                return;
            }
            Log("unknown command");
        }

        public static void Log(string s)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss ") + s + Environment.NewLine);
        }
    }
}
