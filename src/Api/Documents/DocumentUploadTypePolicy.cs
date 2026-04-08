namespace Chatbot.Api.Documents;

internal sealed class DocumentUploadTypePolicy
{
	private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".pdf", ".docx", ".xlsx", ".pptx", ".png", ".jpg", ".jpeg"
	};

	private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".txt", ".md", ".html", ".htm", ".csv", ".tsv", ".json", ".xml", ".yaml", ".yml", ".log", ".ini", ".cfg", ".sql"
	};

	private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".exe", ".dll", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1", ".jar", ".js", ".vbs", ".wsf", ".sh"
	};

	private static readonly HashSet<string> BinaryContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"application/pdf",
		"application/vnd.openxmlformats-officedocument.wordprocessingml.document",
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
		"application/vnd.openxmlformats-officedocument.presentationml.presentation",
		"image/png",
		"image/jpeg"
	};

	private static readonly HashSet<string> TextApplicationContentTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		"application/json",
		"application/xml",
		"application/yaml",
		"application/x-yaml",
		"application/sql",
		"text/xml"
	};

	private readonly DocumentUploadSignatureValidator _signatureValidator = new();

	public bool HasDangerousExtension(string extension)
	{
		return !string.IsNullOrWhiteSpace(extension) && DangerousExtensions.Contains(extension);
	}

	public bool IsSupportedUpload(string extension, string? contentType, ReadOnlySpan<byte> header, DocumentUploadTextInspector textInspector)
	{
		if (header.Length == 0)
		{
			return false;
		}

		if (IsBinaryDocument(extension, contentType))
		{
			return _signatureValidator.HasValidSignature(extension, contentType, header);
		}

		return IsKnownTextExtension(extension)
			|| IsTextContentType(contentType)
			|| textInspector.IsTextLike(header);
	}

	private static bool IsBinaryDocument(string extension, string? contentType)
	{
		return BinaryExtensions.Contains(extension)
			|| (!string.IsNullOrWhiteSpace(contentType) && BinaryContentTypes.Contains(contentType));
	}

	private static bool IsKnownTextExtension(string extension)
	{
		return TextExtensions.Contains(extension);
	}

	private static bool IsTextContentType(string? contentType)
	{
		if (string.IsNullOrWhiteSpace(contentType))
		{
			return false;
		}

		return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
			|| TextApplicationContentTypes.Contains(contentType);
	}
}