using System.Text;

namespace Chatbot.Api.Documents;

internal sealed class DocumentUploadTextInspector
{
	public bool IsTextLike(ReadOnlySpan<byte> header)
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