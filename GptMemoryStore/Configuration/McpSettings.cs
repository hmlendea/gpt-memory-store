namespace GptMemoryStore.Configuration
{
    public sealed class McpSettings
    {
        public bool IsEnabled { get; set; }

        public string Authority { get; set; } = string.Empty;

        public string Resource { get; set; } = string.Empty;

        public string RequiredScope { get; set; } = "memories";
    }
}
