using System.Text;
using Microsoft.AspNetCore.Http;

namespace Chatbot.Api.Documents;

public class DocumentUploadValidator : IDocumentUploadValidator
{
	private const long MaxUploadSizeBytes = 100 * 1024 * 1024;
	private readonly DocumentUploadTypePolicy _typePolicy = new();
	private readonly DocumentUploadTextInspector _textInspector = new();

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

	private void ValidateUpload(IFormFile file)
	{
		var extension = Path.GetExtension(file.FileName);
		if (_typePolicy.HasDangerousExtension(extension))
		{
			throw new InvalidOperationException("File extension is not supported.");
		}

		using var stream = file.OpenReadStream();
		Span<byte> header = stackalloc byte[512];
		var bytesRead = stream.Read(header);

		if (!_typePolicy.IsSupportedUpload(extension, file.ContentType, header[..bytesRead], _textInspector))
		{
			throw new InvalidOperationException("File type is not supported.");
		}
	}
}