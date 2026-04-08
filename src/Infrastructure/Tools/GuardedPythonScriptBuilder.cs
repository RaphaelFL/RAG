using System.Text;

namespace Chatbot.Infrastructure.Tools;

internal sealed class GuardedPythonScriptBuilder
{
    public string Build(string code, IReadOnlyCollection<string> copiedArtifacts, string executionDirectory)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Auto-generated guarded execution wrapper");
        builder.AppendLine($"INPUT_ARTIFACTS = [{string.Join(", ", copiedArtifacts.Select(path => $"r'''{path.Replace("'", "''", StringComparison.Ordinal)}'''"))}]");
        builder.AppendLine($"OUTPUT_DIR = r'''{executionDirectory.Replace("'", "''", StringComparison.Ordinal)}''' ");
        builder.AppendLine();
        builder.Append(code.Trim());
        builder.AppendLine();
        return builder.ToString();
    }
}