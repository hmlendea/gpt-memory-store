using NuciLog.Core;

namespace GptMemoryStore.Logging
{
    public sealed class GptMemoryStoreOperation : Operation
    {
        private GptMemoryStoreOperation(string name) : base(name)
        {
        }

        public static Operation CreateMemory => new GptMemoryStoreOperation(nameof(CreateMemory));

        public static Operation GetMemories => new GptMemoryStoreOperation(nameof(GetMemories));

        public static Operation GetMemory => new GptMemoryStoreOperation(nameof(GetMemory));

        public static Operation UpdateMemory => new GptMemoryStoreOperation(nameof(UpdateMemory));

        public static Operation DeleteMemory => new GptMemoryStoreOperation(nameof(DeleteMemory));
    }
}
