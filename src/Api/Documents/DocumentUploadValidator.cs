using System.Text;
using Microsoft.AspNetCore.Http;

namespace Chatbot.Api.Documents;

public class DocumentUploadValidator : IDocumentUploadValidator
{
	private const long MaxUploadSizeBytes = 100 * 1024 * 1024;

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

	public DocumentUploadValidationFailure? Validate(IFormFile file)
	{
		if (file.Length == 0)
		{
			return new DocumentUploadValidationFailure
			{
				StatusCode = StatusCodes.Status400BadRequest,
				Code = "empty_file",
				Message = "File is empty"
			};
		}

		if (file.Length > MaxUploadSizeBytes)
		{
			return new DocumentUploadValidationFailure
			{
				StatusCode = StatusCodes.Status413PayloadTooLarge,
				Code = "file_too_large",
				Message = "File exceeds maximum size of 100MB"
			};
		}

		try
		{
			ValidateUpload(file);
			return null;
		}
		catch (InvalidOperationException ex)
		{
			return new DocumentUploadValidationFailure
			{
				StatusCode = StatusCodes.Status400BadRequest,
				Code = "invalid_file",
				Message = ex.Message
			};
		}
	}

	private static void ValidateUpload(IFormFile file)
	{
		var extension = Path.GetExtension(file.FileName);
		if (HasDangerousExtension(extension))
		{
			throw new InvalidOperationException("File extension is not supported.");
		}

		using var stream = file.OpenReadStream();
		Span<byte> header = stackalloc byte[512];
		var bytesRead = stream.Read(header);

		if (!IsSupportedUpload(extension, file.ContentType, header[..bytesRead]))
		{
			throw new InvalidOperationException("File type is not supported.");
		}
	}

	private static bool IsSupportedUpload(string extension, string? contentType, ReadOnlySpan<byte> header)
	{
		if (header.Length == 0)
		{
			return false;
		}

		if (IsBinaryDocument(extension, contentType))
		{
			return HasValidSignature(extension, contentType, header);
		}

		return IsKnownTextExtension(extension)
			|| IsTextContentType(contentType)
			|| IsTextLike(header);
	}

	private static bool HasValidSignature(string extension, string? contentType, ReadOnlySpan<byte> header)
	{
		if (header.Length == 0)
		{
			return false;
		}

		if (MatchesBinaryType(extension, contentType, ".pdf", "application/pdf"))
		{
			return header.Length >= 4 && header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;
		}

		if (MatchesBinaryType(extension, contentType, ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"))
		{
			return header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B;
		}

		if (MatchesBinaryType(extension, contentType, ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"))
		{
			return header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B;
		}

		if (MatchesBinaryType(extension, contentType, ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"))
		{
			return header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B;
		}

		if (MatchesBinaryType(extension, contentType, ".png", "image/png"))
		{
			return header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
		}

		if (MatchesBinaryType(extension, contentType, ".jpg", "image/jpeg") || MatchesBinaryType(extension, contentType, ".jpeg", "image/jpeg"))
		{
			return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
		}

		return false;
	}

	private static bool MatchesBinaryType(string extension, string? contentType, string expectedExtension, string expectedContentType)
	{
		return string.Equals(extension, expectedExtension, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase);
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

	private static bool HasDangerousExtension(string extension)
	{
		return !string.IsNullOrWhiteSpace(extension) && DangerousExtensions.Contains(extension);
	}

	private static bool IsTextLike(ReadOnlySpan<byte> header)
	{
		if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
		{
			return true;
		}

		if (header.Length >= 2)
		{
			var hasUtf16Bom = (header[0] == 0xFF && header[1] == 0xFE) || (header[0] == 0xFE && header[1] == 0xFF);
			if (hasUtf16Bom)
			{
				return true;
			}
		}

		var printable = 0;
		var alphaNumeric = 0;

		foreach (var value in header)
		{
			if (value == 0)
			{
				return false;
			}

			if (value is 9 or 10 or 13 || value is >= 32 and <= 126 || value >= 160)
			{
				printable++;
			}

			if ((value is >= (byte)'0' and <= (byte)'9')
				|| (value is >= (byte)'A' and <= (byte)'Z')
				|| (value is >= (byte)'a' and <= (byte)'z'))
			{
				alphaNumeric++;
			}
		}

		var preview = Encoding.UTF8.GetString(header);
		if (string.IsNullOrWhiteSpace(preview))
		{
			return false;
		}

		var printableRatio = printable / (double)header.Length;
		return printableRatio >= 0.85 && (alphaNumeric > 0 || printable == header.Length);
	}
}