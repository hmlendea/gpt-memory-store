using NuciLog.Core;

namespace GptMemoryStore.Logging
{
    public sealed class MyLogInfoKey : LogInfoKey
    {
        MyLogInfoKey(string name) : base(name) { }

        public static LogInfoKey Id => new MyLogInfoKey(nameof(Id));
        public static LogInfoKey Content => new MyLogInfoKey(nameof(Content));
        public static LogInfoKey Count => new MyLogInfoKey(nameof(Count));
        public static LogInfoKey NewContent => new MyLogInfoKey(nameof(NewContent));
        public static LogInfoKey OldContent => new MyLogInfoKey(nameof(OldContent));
        public static LogInfoKey Source => new MyLogInfoKey(nameof(Source));
        public static LogInfoKey Confidence => new MyLogInfoKey(nameof(Confidence));
    }
}
