using NuciDAL.DataObjects;

namespace GptMemoryStore.DataAccess.DataObjects
{
    public class GptMemoryDataObject : EntityBase
    {
        public string Date { get; set; }

        public string CreatedTimestamp { get; set; }

        public string UpdatedTimestamp { get; set; }

        public string Content { get; set; }

        public string Source { get; set; }

        public decimal Confidence { get; set; }
    }
}
