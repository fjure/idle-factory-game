// T410 measurement helper (Feature 004, research R12). NOT part of the game: copy it into a throwaway
// Assets/TempB1Probe/Editor/ folder (scripts/f004-b1/run-build.ps1 does this and removes it afterwards).
// It is editor-only, adds nothing to the player and changes no setting. It records
//   1. per shader the variant count Unity hands to the compiler after its own stripping (IPreprocessShaders), and
//   2. a text dump of the latest BuildReport (output size, files by role, packed assets by type, textures, shaders).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.B1Probe
{
    public sealed class ShaderVariantCounter : IPreprocessShaders
    {
        public int callbackOrder => int.MaxValue;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            var path = Environment.GetEnvironmentVariable("B1_SHADER_LOG");
            if (string.IsNullOrEmpty(path)) return;
            File.AppendAllText(path, string.Format(CultureInfo.InvariantCulture, "{0}\t{1}\t{2}\t{3}\n",
                shader.name, snippet.passType, snippet.shaderType, data.Count));
        }
    }

    public static class B1ReportDump
    {
        public static void DumpLatest()
        {
            var output = Environment.GetEnvironmentVariable("B1_REPORT_OUT");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("B1_REPORT_OUT not set");
            var report = BuildReport.GetLatestReport();
            var sb = new StringBuilder();
            var s = report.summary;
            sb.AppendLine("platform\t" + s.platform);
            sb.AppendLine("result\t" + s.result);
            sb.AppendLine("options\t" + s.options);
            sb.AppendLine("totalSize\t" + s.totalSize);
            sb.AppendLine("totalTime\t" + s.totalTime);
            sb.AppendLine("outputPath\t" + s.outputPath);
            sb.AppendLine("errors\t" + s.totalErrors + "\twarnings\t" + s.totalWarnings);
            sb.AppendLine("[files by role]");
            foreach (var g in report.GetFiles().GroupBy(f => f.role).OrderByDescending(g => g.Sum(f => (long)f.size)))
                sb.AppendLine(g.Key + "\t" + g.Count() + "\t" + g.Sum(f => (long)f.size));
            sb.AppendLine("[packed assets by type]");
            var contents = new List<PackedAssetInfo>();
            foreach (var pa in report.packedAssets) contents.AddRange(pa.contents);
            foreach (var g in contents.GroupBy(c => c.type == null ? "?" : c.type.Name).OrderByDescending(g => g.Sum(c => (long)c.packedSize)))
                sb.AppendLine(g.Key + "\t" + g.Count() + "\t" + g.Sum(c => (long)c.packedSize));
            sb.AppendLine("[textures]");
            foreach (var c in contents.Where(c => c.type == typeof(Texture2D) || (c.type != null && c.type.Name.StartsWith("Texture", StringComparison.Ordinal))).OrderByDescending(c => c.packedSize))
                sb.AppendLine(c.type.Name + "\t" + c.packedSize + "\t" + c.sourceAssetPath);
            sb.AppendLine("[shaders]");
            foreach (var c in contents.Where(c => c.type == typeof(Shader)).OrderByDescending(c => c.packedSize))
                sb.AppendLine("Shader\t" + c.packedSize + "\t" + c.sourceAssetPath);
            File.WriteAllText(output, sb.ToString());
            Debug.Log("[B1Probe] report dumped to " + output);
        }
    }
}
