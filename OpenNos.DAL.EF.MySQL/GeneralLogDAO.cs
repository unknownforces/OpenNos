﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using AutoMapper;

using OpenNos.DAL.EF.MySQL.Helpers;
using OpenNos.DAL.Interface;
using OpenNos.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenNos.DAL.EF.MySQL
{
    public class GeneralLogDAO : IGeneralLogDAO
    {
        #region Members

        private IMapper _mapper;

        #endregion

        #region Instantiation

        public GeneralLogDAO()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<GeneralLog, GeneralLogDTO>();
                cfg.CreateMap<GeneralLogDTO, GeneralLog>();
            });

            _mapper = config.CreateMapper();
        }

        #endregion

        #region Methods

        public GeneralLogDTO Insert(GeneralLogDTO generallog)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                GeneralLog entity = _mapper.Map<GeneralLog>(generallog);
                context.GeneralLog.Add(entity);
                context.SaveChanges();
                return _mapper.Map<GeneralLogDTO>(generallog);
            }
        }

        public IEnumerable<GeneralLogDTO> LoadByAccount(long accountId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (GeneralLog GeneralLog in context.GeneralLog.Where(s => s.AccountId.Equals(accountId)))
                {
                    yield return _mapper.Map<GeneralLogDTO>(GeneralLog);
                }
            }
        }

        public IEnumerable<GeneralLogDTO> LoadByLogType(string logType, Nullable<long> characterId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (GeneralLog log in context.GeneralLog.Where(c => c.LogType.Equals(logType) && c.CharacterId == characterId))
                {
                    yield return _mapper.Map<GeneralLogDTO>(log);
                }
            }
        }

        public void SetCharIdNull(long? characterId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (GeneralLog log in context.GeneralLog.Where(c => c.CharacterId == characterId))
                {
                    log.CharacterId = null;
                }
                context.SaveChanges();
            }
        }

        public void WriteGeneralLog(long accountId, string ipAddress, Nullable<long> characterId, string logType, string logData)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                GeneralLog log = new GeneralLog()
                {
                    AccountId = accountId,
                    IpAddress = ipAddress,
                    Timestamp = DateTime.Now,
                    LogType = logType,
                    LogData = logData,
                    CharacterId = characterId
                };

                context.GeneralLog.Add(log);
                context.SaveChanges();
            }
        }

        #endregion
    }
}