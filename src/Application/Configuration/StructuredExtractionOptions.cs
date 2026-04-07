namespace Chatbot.Application.Configuration;

public sealed class StructuredExtractionOptions
{
    public bool EnableForms { get; set; } = true;
    public bool EnableTables { get; set; } = true;
    public bool EnableSpreadsheetStructure { get; set; } = true;
    public bool EnablePresentationStructure { get; set; } = true;
}
