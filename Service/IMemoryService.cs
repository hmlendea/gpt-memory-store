using System.Collections.Generic;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Service
{
    public interface IMemoryService
    {
        public void Create(GptMemory memory);

        public GptMemory Get(string id);

        public IEnumerable<GptMemory> Get();

        public void Update(GptMemory memory);

        public void Delete(string id);
    }
}
