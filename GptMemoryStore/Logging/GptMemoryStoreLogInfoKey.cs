using NuciLog.Core;

namespace GptMemoryStore.Logging
{
    public sealed class GptMemoryStoreLogInfoKey : LogInfoKey
    {
        private GptMemoryStoreLogInfoKey(string name) : base(name) { }

        public static LogInfoKey Id => new GptMemoryStoreLogInfoKey(nameof(Id));

        public static LogInfoKey Content => new GptMemoryStoreLogInfoKey(nameof(Content));

        public static LogInfoKey Count => new GptMemoryStoreLogInfoKey(nameof(Count));

        public static LogInfoKey NewContent => new GptMemoryStoreLogInfoKey(nameof(NewContent));

        public static LogInfoKey OldContent => new GptMemoryStoreLogInfoKey(nameof(OldContent));

        public static LogInfoKey Source => new GptMemoryStoreLogInfoKey(nameof(Source));

        public static LogInfoKey Confidence => new GptMemoryStoreLogInfoKey(nameof(Confidence));
    }
}
