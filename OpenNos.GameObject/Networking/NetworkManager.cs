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
using OpenNos.Core.Communication.Scs.Communication.EndPoints.Tcp;
using OpenNos.Core.Communication.Scs.Communication.Messages;
using OpenNos.Core.Communication.Scs.Server;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using OpenNos.Core;

namespace OpenNos.GameObject
{
    public class NetworkManager<EncryptorT>
        where EncryptorT : EncryptionBase
    {

        #region Members

        private IList<Type> _packetHandlers;
        private EncryptorT _encryptor;
        private IDictionary<String, DateTime> _connectionLog;
        private ConcurrentDictionary<long, ClientSession> _sessions = new ConcurrentDictionary<long, ClientSession>();

        #endregion

        #region Instantiation

        public NetworkManager(string ipAddress, int port, IList<Type> packetHandlers, bool useFraming)
        {
            _packetHandlers = packetHandlers;
            _encryptor = (EncryptorT)Activator.CreateInstance(typeof(EncryptorT));

            var server = ScsServerFactory.CreateServer(new ScsTcpEndPoint(ipAddress, port));
            //Register events of the server to be informed about clients
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            server.WireProtocolFactory = new WireProtocolFactory<EncryptorT>(useFraming);

            server.Start(); //Start the server

            Logger.Log.Info(Language.Instance.GetMessageFromKey("STARTED"));
        }

        #endregion

        #region Event Handlers

        void Server_ClientConnected(object sender, ServerClientEventArgs e)
        {
            Logger.Log.Info(Language.Instance.GetMessageFromKey("NEW_CONNECT") + e.Client.ClientId);
            NetworkClient customClient = e.Client as NetworkClient;

            if (!CheckConnectionLog(customClient))
            {
                Logger.Log.WarnFormat(Language.Instance.GetMessageFromKey("FORCED_DISCONNECT"), customClient.ClientId);
                customClient.Disconnect();
                return;
            }

            ClientSession session = new ClientSession(customClient);
            session.Initialize(_encryptor, _packetHandlers);
            if (!_sessions.TryAdd(customClient.ClientId, session))
            {
                Logger.Log.WarnFormat(Language.Instance.GetMessageFromKey("FORCED_DISCONNECT"), customClient.ClientId);
                customClient.Disconnect();
                _sessions.TryRemove(customClient.ClientId, out session);
                return;
            };
        }

        void Server_ClientDisconnected(object sender, ServerClientEventArgs e)
        {
            ClientSession session;
            _sessions.TryRemove(e.Client.ClientId, out session);
            session.Destroy();
            e.Client.Disconnect();
            Logger.Log.Info(Language.Instance.GetMessageFromKey("DISCONNECT") + e.Client.ClientId);
            session = null;
        }

        #endregion

        #region Methods

        private bool CheckConnectionLog(NetworkClient client)
        {
            if (ConnectionLog.Any())
            {
                IEnumerable<KeyValuePair<string, DateTime>> logsToDelete = ConnectionLog
                    .Where(cl => cl.Key.Equals(client.RemoteEndPoint.ToString()) && (DateTime.Now - cl.Value).Seconds > 5);

                foreach (KeyValuePair<string, DateTime> connectionLogEntry in logsToDelete)
                {
                    ConnectionLog.Remove(connectionLogEntry);
                }
            }

            if (ConnectionLog.ContainsKey(client.RemoteEndPoint.ToString()))
            {
                return false;
            }
            else
            {
                ConnectionLog.Add(client.RemoteEndPoint.ToString(), DateTime.Now);
                return true;
            }
        }

        #endregion

        #region Properties

        public IDictionary<String, DateTime> ConnectionLog
        {
            get
            {
                if (_connectionLog == null)
                {
                    _connectionLog = new Dictionary<String, DateTime>();
                }

                return _connectionLog;
            }
            set
            {
                if (_connectionLog != value)
                {
                    _connectionLog = value;
                }
            }
        }

        #endregion
    }
}