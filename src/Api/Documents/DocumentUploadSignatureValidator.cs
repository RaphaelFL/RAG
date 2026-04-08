namespace Chatbot.Api.Documents;

internal sealed class DocumentUploadSignatureValidator
{
	public bool HasValidSignature(string extension, string? contentType, ReadOnlySpan<byte> header)
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
}