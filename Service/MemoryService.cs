using System;
using System.Collections.Generic;
using System.Linq;
using GptMemoryStore.DataAccess.DataObjects;
using GptMemoryStore.Logging;
using GptMemoryStore.Service.Mapping;
using GptMemoryStore.Service.Models;
using NuciDAL.Repositories;
using NuciLog.Core;

namespace GptMemoryStore.Service
{
    public sealed class MemoryService(
        IFileRepository<GptMemoryDataObject> repository,
        ILogger logger) : IMemoryService
    {
        public void Create(GptMemory memory)
        {
            List<LogInfo> logInfos =
            [
                new LogInfo(MyLogInfoKey.Id, memory.Id)
            ];

            logger.Info(
                MyOperation.CreateMemory,
                OperationStatus.Started,
                logInfos,
                new LogInfo(MyLogInfoKey.Content, memory.Content),
                new LogInfo(MyLogInfoKey.Source, memory.Source),
                new LogInfo(MyLogInfoKey.Confidence, memory.Confidence));

            try
            {
                repository.Add(memory.ToDataObject());
                repository.SaveChanges();

                logger.Debug(
                    MyOperation.CreateMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.CreateMemory,
                    OperationStatus.Failure,
                    exception,
                    logInfos);

                throw;
            }
        }

        public GptMemory Get(string id)
        {
            List<LogInfo> logInfos =
            [
                new LogInfo(MyLogInfoKey.Id, id)
            ];

            logger.Info(
                MyOperation.GetMemory,
                OperationStatus.Started,
                logInfos);

            try
            {
                GptMemory memory = repository.Get(id).ToDomainModel();

                logger.Debug(
                    MyOperation.GetMemory,
                    OperationStatus.Success,
                    logInfos,
                    new LogInfo(MyLogInfoKey.Content, memory.Content),
                    new LogInfo(MyLogInfoKey.Source, memory.Source),
                    new LogInfo(MyLogInfoKey.Confidence, memory.Confidence));

                return memory;
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.GetMemory,
                    OperationStatus.Failure,
                    exception,
                    new LogInfo(MyLogInfoKey.Id, id));

                throw;
            }
        }

        public IEnumerable<GptMemory> Get()
        {
            logger.Info(
                MyOperation.GetMemories,
                OperationStatus.Started);

            try
            {
                IEnumerable<GptMemory> memories = repository.GetAll().ToDomainModels();

                logger.Debug(
                    MyOperation.GetMemories,
                    OperationStatus.Success,
                    new LogInfo(MyLogInfoKey.Count, memories.Count()));

                return memories;
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.GetMemories,
                    OperationStatus.Failure,
                    exception);

                throw;
            }
        }

        public void Update(GptMemory memory)
        {
            List<LogInfo> logInfos =
            [
                new LogInfo(MyLogInfoKey.Id, memory.Id)
            ];

            logger.Info(
                MyOperation.UpdateMemory,
                OperationStatus.Started,
                logInfos,
                new LogInfo(MyLogInfoKey.Content, memory.Content),
                new LogInfo(MyLogInfoKey.Source, memory.Source),
                new LogInfo(MyLogInfoKey.Confidence, memory.Confidence));

            try
            {
                memory.CreatedDateTime = repository.Get(memory.Id).ToDomainModel().CreatedDateTime;

                repository.Update(memory.ToDataObject());
                repository.SaveChanges();

                logger.Debug(
                    MyOperation.UpdateMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.UpdateMemory,
                    OperationStatus.Failure,
                    exception,
                    logInfos);

                throw;
            }
        }

        public void Delete(string id)
        {
            List<LogInfo> logInfos =
            [
                new LogInfo(MyLogInfoKey.Id, id)
            ];

            logger.Info(
                MyOperation.DeleteMemory,
                OperationStatus.Started,
                logInfos);

            try
            {
                repository.Remove(id);
                repository.SaveChanges();

                logger.Debug(
                    MyOperation.DeleteMemory,
                    OperationStatus.Success,
                    logInfos);
            }
            catch (Exception exception)
            {
                logger.Error(
                    MyOperation.DeleteMemory,
                    OperationStatus.Failure,
                    exception,
                    logInfos);

                throw;
            }
        }
    }
}
