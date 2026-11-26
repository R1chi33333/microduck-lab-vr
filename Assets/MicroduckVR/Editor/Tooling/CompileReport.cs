using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Writes every compiler warning and error from the game's own assemblies to Logs/compile_messages.txt, so a
    /// check (or the editor bridge) can read them without the Console. Microduck > Debug > Full Recompile rebuilds
    /// everything from scratch and refreshes the file.
    /// </summary>
    [InitializeOnLoad]
    public static class CompileReport
    {
        const string LogPath = "Logs/compile_messages.txt";

        static CompileReport()
        {
            CompilationPipeline.compilationStarted -= Started;
            CompilationPipeline.compilationStarted += Started;
            CompilationPipeline.assemblyCompilationFinished -= Finished;
            CompilationPipeline.assemblyCompilationFinished += Finished;
        }

        static void Started(object context)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, "");
        }

        static void Finished(string assembly, CompilerMessage[] messages)
        {
            if (!Path.GetFileName(assembly).StartsWith("MicroduckVR")) return;
            var lines = messages.Select(m => $"{m.type} {Path.GetFileName(assembly)} {m.file}({m.line}): {m.message}").ToList();
            int warnings = messages.Count(m => m.type == CompilerMessageType.Warning), errors = messages.Count(m => m.type == CompilerMessageType.Error);
            lines.Insert(0, $"{Path.GetFileName(assembly)}: {errors} errors, {warnings} warnings");
            File.AppendAllLines(LogPath, lines);
        }

        /// <summary>Rebuild every script assembly from scratch (the report then lists all warnings, not only changed files).</summary>
        [MenuItem("Microduck/Debug/Full Recompile")]
        public static void FullRecompile()
        {
            Debug.Log("[Microduck] full recompile requested");
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
    }
}
