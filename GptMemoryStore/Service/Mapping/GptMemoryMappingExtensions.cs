using System;
using System.Collections.Generic;
using System.Linq;

using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Service.Mapping
{
    static class GptMemoryMappingExtensions
    {
        internal static GptMemory ToDomainModel(this GptMemoryDataObject dataObject) => new()
        {
            Id = dataObject.Id,
            CreatedDateTime = DateTimeOffset.Parse(dataObject.CreatedTimestamp),
            UpdatedDateTime = ParseNullableTimestamp(dataObject.UpdatedTimestamp),
            Content = dataObject.Content,
            Source = dataObject.Source,
            Confidence = dataObject.Confidence
        };

        internal static GptMemoryDataObject ToDataObject(this GptMemory domainModel) => new()
        {
            Id = domainModel.Id,
            CreatedTimestamp = domainModel.CreatedDateTime.ToString(DateTimeFormat),
            UpdatedTimestamp = domainModel.UpdatedDateTime?.ToString(DateTimeFormat),
            Content = domainModel.Content,
            Source = domainModel.Source,
            Confidence = domainModel.Confidence
        };

        internal static IEnumerable<GptMemory> ToDomainModels(this IEnumerable<GptMemoryDataObject> dataObjects)
            => dataObjects.Select(dataObject => dataObject.ToDomainModel());

        internal static IEnumerable<GptMemoryDataObject> ToDataObjects(this IEnumerable<GptMemory> domainModels)
            => domainModels.Select(domainModel => domainModel.ToDataObject());

        private static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        private static DateTimeOffset? ParseNullableTimestamp(string timestamp)
        {
            if (timestamp is null)
            {
                return null;
            }

            return DateTimeOffset.Parse(timestamp);
        }
    }
}
