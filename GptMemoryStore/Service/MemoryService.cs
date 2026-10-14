using System;
using System.Collections.Generic;
using System.Linq;

using NuciDAL.Repositories;

using NuciLog.Core;

using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Logging;
using GptMemoryStore.Service.Mapping;
using GptMemoryStore.Service.Models;

namespace GptMemoryStore.Service
{
    public sealed class MemoryService(
        IFileRepository<GptMemoryDataObject> repository,
        ILogger logger) : IMemoryService
    {
        public void Create(GptMemory memory)
        {
            IEnumerable<LogInfo> logInfos =
            [
                new LogInfo(GptMemoryStoreLogInfoKey.Id, memory.Id)
            ];

            logger.Info(
                GptMemoryStoreOperation.CreateMemory,
                OperationStatus.Started,
                logInfos,
                new LogInfo(GptMemoryStoreLogInfoKey.Content, memory.Content),
                new LogInfo(GptMemoryStoreLogInfoKey.Source, memory.Source),
                new LogInfo(GptMemoryStoreLogInfoKey.Confidence, memory.Confidence));

            try
            {
                repository.Add(memory.ToDataObject());
                repository.SaveChanges();

                logger.Debug(
                    GptMemoryStoreOperation.CreateMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception)
            {
                logger.Error(
                    GptMemoryStoreOperation.CreateMemory,
                    OperationStatus.Failure,
                    logInfos);

                throw;
            }
        }

        public GptMemory Get(string id)
        {
            IEnumerable<LogInfo> logInfos =
            [
                new LogInfo(GptMemoryStoreLogInfoKey.Id, id)
            ];

            logger.Info(
                GptMemoryStoreOperation.GetMemory,
                OperationStatus.Started,
                logInfos);

            try
            {
                GptMemory memory = repository.Get(id).ToDomainModel();

                logger.Debug(
                    GptMemoryStoreOperation.GetMemory,
                    OperationStatus.Success,
                    logInfos,
                    new LogInfo(GptMemoryStoreLogInfoKey.Content, memory.Content),
                    new LogInfo(GptMemoryStoreLogInfoKey.Source, memory.Source),
                    new LogInfo(GptMemoryStoreLogInfoKey.Confidence, memory.Confidence));

                return memory;
            }
            catch (Exception)
            {
                logger.Error(
                    GptMemoryStoreOperation.GetMemory,
                    OperationStatus.Failure,
                    new LogInfo(GptMemoryStoreLogInfoKey.Id, id));

                throw;
            }
        }

        public IEnumerable<GptMemory> Get()
        {
            logger.Info(
                GptMemoryStoreOperation.GetMemories,
                OperationStatus.Started);

            try
            {
                IEnumerable<GptMemory> memories = repository.GetAll().ToDomainModels();

                logger.Debug(
                    GptMemoryStoreOperation.GetMemories,
                    OperationStatus.Success,
                    new LogInfo(GptMemoryStoreLogInfoKey.Count, memories.Count()));

                return memories;
            }
            catch (Exception)
            {
                logger.Error(
                    GptMemoryStoreOperation.GetMemories,
                    OperationStatus.Failure);

                throw;
            }
        }

        public void Update(GptMemory memory)
        {
            IEnumerable<LogInfo> logInfos =
            [
                new LogInfo(GptMemoryStoreLogInfoKey.Id, memory.Id)
            ];

            logger.Info(
                GptMemoryStoreOperation.UpdateMemory,
                OperationStatus.Started,
                logInfos,
                new LogInfo(GptMemoryStoreLogInfoKey.Content, memory.Content),
                new LogInfo(GptMemoryStoreLogInfoKey.Source, memory.Source),
                new LogInfo(GptMemoryStoreLogInfoKey.Confidence, memory.Confidence));

            try
            {
                memory.CreatedDateTime = repository.Get(memory.Id).ToDomainModel().CreatedDateTime;

                repository.Update(memory.ToDataObject());
                repository.SaveChanges();

                logger.Debug(
                    GptMemoryStoreOperation.UpdateMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception)
            {
                logger.Error(
                    GptMemoryStoreOperation.UpdateMemory,
                    OperationStatus.Failure,
                    logInfos);

                throw;
            }
        }

        public void Delete(string id)
        {
            IEnumerable<LogInfo> logInfos =
            [
                new LogInfo(GptMemoryStoreLogInfoKey.Id, id)
            ];

            logger.Info(
                GptMemoryStoreOperation.DeleteMemory,
                OperationStatus.Started,
                logInfos);

            try
            {
                repository.Remove(id);
                repository.SaveChanges();

                logger.Debug(
                    GptMemoryStoreOperation.DeleteMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception)
            {
                logger.Error(
                    GptMemoryStoreOperation.DeleteMemory,
                    OperationStatus.Failure,
                    logInfos);

                throw;
            }
        }
    }
}
