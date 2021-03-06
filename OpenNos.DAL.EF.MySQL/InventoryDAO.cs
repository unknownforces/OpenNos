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
using OpenNos.Core;
using OpenNos.DAL.EF.MySQL.DB;
using OpenNos.DAL.EF.MySQL.Helpers;
using OpenNos.DAL.Interface;
using OpenNos.Data;
using OpenNos.Data.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenNos.DAL.EF.MySQL
{
    public class InventoryDAO : IInventoryDAO
    {
        #region Members

        private Type _baseType;
        private IMapper _mapper;
        private IDictionary<Type, Type> itemInstanceMappings = new Dictionary<Type, Type>();

        #endregion

        #region Instantiation

        public InventoryDAO()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<Inventory, InventoryDTO>();
                cfg.CreateMap<InventoryDTO, Inventory>();
            });

            _mapper = config.CreateMapper();
        }

        #endregion

        #region Methods

        public DeleteResult DeleteFromSlotAndType(long characterId, short slot, byte type)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                Inventory inv = context.Inventory.FirstOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type) && i.CharacterId.Equals(characterId));
                ItemInstance invItem = context.ItemInstance.FirstOrDefault(i => i.Inventory.InventoryId == inv.InventoryId);
                if (inv != null)
                {
                    context.Inventory.Remove(inv);
                    context.ItemInstance.Remove(invItem);
                    context.SaveChanges();
                }

                return DeleteResult.Deleted;
            }
        }

        public ItemInstanceDTO Id(long inventoryId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                var itemInstance = context.ItemInstance.Include(nameof(Inventory)).FirstOrDefault(i => i.Inventory.InventoryId.Equals(inventoryId));
                return _mapper.Map<ItemInstanceDTO>(itemInstance);
            }
        }

        public void InitializeMapper(Type baseType)
        {
            _baseType = baseType;
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap(baseType, typeof(ItemInstance))
                    .ForMember("Item", opts => opts.Ignore());

                cfg.CreateMap(typeof(ItemInstance), typeof(ItemInstanceDTO)).As(baseType);

                Type itemInstanceType = typeof(ItemInstance);
                foreach (KeyValuePair<Type, Type> entry in itemInstanceMappings)
                {
                    //GameObject -> Entity
                    cfg.CreateMap(entry.Key, entry.Value).ForMember("Item", opts => opts.Ignore())
                                .IncludeBase(baseType, typeof(ItemInstance));

                    //Entity -> GameObject
                    cfg.CreateMap(entry.Value, entry.Key)
                                .IncludeBase(typeof(ItemInstance), baseType);

                    Type retrieveDTOType = Type.GetType($"OpenNos.Data.{entry.Key.Name}DTO, OpenNos.Data");
                    //Entity -> DTO
                    cfg.CreateMap(entry.Value, typeof(ItemInstanceDTO)).As(entry.Key);
                }

                //Inventory Mappings
                cfg.CreateMap<InventoryDTO, Inventory>();
                cfg.CreateMap<Inventory, InventoryDTO>();
            });

            _mapper = config.CreateMapper();
        }

        public IEnumerable<InventoryDTO> InsertOrUpdate(IEnumerable<InventoryDTO> inventories)
        {
            try
            {
                IList<InventoryDTO> results = new List<InventoryDTO>();
                using (var context = DataAccessHelper.CreateContext())
                {
                    foreach (InventoryDTO inventory in inventories)
                    {
                        results.Add(InsertOrUpdate(context, inventory));
                    }
                }

                return results;
            }
            catch (Exception e)
            {
                Logger.Log.Error(String.Format(Language.Instance.GetMessageFromKey("UPDATE_ERROR"), e.Message), e);
                return Enumerable.Empty<InventoryDTO>();
            }
        }

        public InventoryDTO InsertOrUpdate(InventoryDTO inventory)
        {
            try
            {
                using (var context = DataAccessHelper.CreateContext())
                {
                    return InsertOrUpdate(context, inventory);
                }
            }
            catch (Exception e)
            {
                Logger.Log.Error(String.Format(Language.Instance.GetMessageFromKey("UPDATE_ERROR"), e.Message), e);
                return null;
            }
        }

        public IEnumerable<InventoryDTO> LoadByCharacterId(long characterId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Inventory inventory in context.Inventory.Include(nameof(ItemInstance)).Where(i => i.CharacterId.Equals(characterId)))
                {
                    yield return _mapper.Map<InventoryDTO>(inventory);
                }
            }
        }

        public InventoryDTO LoadById(long inventoryId)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                return _mapper.Map<InventoryDTO>(context.Inventory.Include(nameof(ItemInstance)).FirstOrDefault(i => i.InventoryId.Equals(inventoryId)));
            }
        }

        public InventoryDTO LoadBySlotAndType(long characterId, short slot, byte type)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                return _mapper.Map<InventoryDTO>(context.Inventory.Include(nameof(ItemInstance)).FirstOrDefault(i => i.Slot.Equals(slot) && i.Type.Equals(type) && i.CharacterId.Equals(characterId)));
            }
        }

        public IEnumerable<InventoryDTO> LoadByType(long characterId, byte type)
        {
            using (var context = DataAccessHelper.CreateContext())
            {
                foreach (Inventory inventoryEntry in context.Inventory.Include(nameof(ItemInstance)).Where(i => i.Type.Equals(type) && i.CharacterId.Equals(characterId)))
                {
                    yield return _mapper.Map<InventoryDTO>(inventoryEntry);
                }
            }
        }

        public void RegisterMapping(Type gameObjectType)
        {
            Type targetType = Assembly.GetExecutingAssembly().GetTypes().SingleOrDefault(t => t.Name.Equals(gameObjectType.Name));
            Type itemInstanceType = typeof(ItemInstance);
            itemInstanceMappings.Add(gameObjectType, targetType);
        }

        private InventoryDTO Insert(InventoryDTO inventory, OpenNosContext context)
        {
            Inventory entity = Mapper.Map<Inventory>(inventory);
            KeyValuePair<Type, Type> targetMapping = itemInstanceMappings.FirstOrDefault(k => k.Key.Equals(inventory.ItemInstance.GetType()));
            if (targetMapping.Key != null)
            {
                entity.ItemInstance = _mapper.Map(inventory.ItemInstance, targetMapping.Key, targetMapping.Value) as ItemInstance;
            }

            entity.ItemInstance.Item = null; //stupid references

            context.Inventory.Add(entity);
            context.SaveChanges();
            return _mapper.Map<InventoryDTO>(entity);
        }

        private InventoryDTO InsertOrUpdate(OpenNosContext context, InventoryDTO inventory)
        {
            long InventoryId = inventory.InventoryId;
            byte Type = inventory.Type;
            short Slot = inventory.Slot;
            long CharacterId = inventory.CharacterId;
            Inventory entity = context.Inventory.FirstOrDefault(c => c.InventoryId == InventoryId);
            if (entity == null) //new entity
            {
                Inventory delete = context.Inventory.FirstOrDefault(s => s.CharacterId == CharacterId && s.Slot == Slot && s.Type == Type);
                if (delete != null)
                {
                    ItemInstance deleteItem = context.ItemInstance.FirstOrDefault(s => s.Inventory.InventoryId == delete.InventoryId);
                    context.ItemInstance.Remove(deleteItem);
                    context.Inventory.Remove(delete);
                    context.SaveChanges();
                }
                inventory = Insert(inventory, context);
            }
            else //existing entity
            {
                entity.ItemInstance = context.ItemInstance.FirstOrDefault(c => c.Inventory.InventoryId == entity.InventoryId);
                inventory = Update(entity, inventory, context);
            }

            return inventory;
        }

        private InventoryDTO Update(Inventory entity, InventoryDTO inventory, OpenNosContext context)
        {
            if (entity != null)
            {
                _mapper.Map(inventory, entity);
                context.SaveChanges();
            }

            return _mapper.Map<InventoryDTO>(entity);
        }

        #endregion
    }
}