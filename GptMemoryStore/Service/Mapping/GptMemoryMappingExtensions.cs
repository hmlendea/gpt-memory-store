using System;
using System.Collections.Generic;
using System.Linq;
using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Service.Mapping
{
    /// <summary>
    /// GptMemory mapping extensions for converting between data objects and domain models.
    /// </summary>
    static class GptMemoryMappingExtensions
    {
        static string DateTimeFormat => "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";

        /// <summary>
        /// Converts the data object into a domain model.
        /// </summary>
        /// <returns>The domain model.</returns>
        /// <param name="dataObject">The data object.</param>
        internal static GptMemory ToDomainModel(this GptMemoryDataObject dataObject) => new()
        {
            Id = dataObject.Id,
            CreatedDateTime = DateTimeOffset.Parse(dataObject.CreatedTimestamp),
            UpdatedDateTime = dataObject.UpdatedTimestamp != null ? DateTimeOffset.Parse(dataObject.UpdatedTimestamp) : null,
            Content = dataObject.Content,
            Source = dataObject.Source,
            Confidence = dataObject.Confidence
        };

        /// <summary>
        /// Converts the domain model into a data object.
        /// </summary>
        /// <returns>The data object.</returns>
        /// <param name="domainModel">The domain model.</param>
        internal static GptMemoryDataObject ToDataObject(this GptMemory domainModel) => new()
        {
            Id = domainModel.Id,
            CreatedTimestamp = domainModel.CreatedDateTime.ToString(DateTimeFormat),
            UpdatedTimestamp = domainModel.UpdatedDateTime?.ToString(DateTimeFormat),
            Content = domainModel.Content,
            Source = domainModel.Source,
            Confidence = domainModel.Confidence
        };

        /// <summary>
        /// Converts the data objects into domain models.
        /// </summary>
        /// <returns>The domain models.</returns>
        /// <param name="dataObjects">The data objects.</param>
        internal static IEnumerable<GptMemory> ToDomainModels(this IEnumerable<GptMemoryDataObject> dataObjects)
            => dataObjects.Select(dataObject => dataObject.ToDomainModel());

        /// <summary>
        /// Converts the domain models into data objects.
        /// </summary>
        /// <returns>The data objects.</returns>
        /// <param name="domainModels">The domain models.</param>
        internal static IEnumerable<GptMemoryDataObject> ToDataObjects(this IEnumerable<GptMemory> domainModels)
            => domainModels.Select(domainModel => domainModel.ToDataObject());
    }
}
