using NuciLog.Core;

namespace GptMemoryStore.Logging
{
    public sealed class MyOperation : Operation
    {
        MyOperation(string name) : base(name) { }

        public static Operation CreateMemory => new MyOperation(nameof(CreateMemory));
        public static Operation GetMemories => new MyOperation(nameof(GetMemories));
        public static Operation GetMemory => new MyOperation(nameof(GetMemory));
        public static Operation UpdateMemory => new MyOperation(nameof(UpdateMemory));
        public static Operation DeleteMemory => new MyOperation(nameof(DeleteMemory));
    }
}
