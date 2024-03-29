﻿/**
* Digital Voice Modem - ISSI
* AGPLv3 Open Source. Use is subject to license terms.
* DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
*
* @package DVM / ISSI
*
*/
/*
*   Copyright (C) 2022 by Bryan Biedenkapp N2PLL
*
*   This program is free software: you can redistribute it and/or modify
*   it under the terms of the GNU Affero General Public License as published by
*   the Free Software Foundation, either version 3 of the License, or
*   (at your option) any later version.
*
*   This program is distributed in the hope that it will be useful,
*   but WITHOUT ANY WARRANTY; without even the implied warranty of
*   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*   GNU Affero General Public License for more details.
*/

using System;
using System.Collections.Generic;

using fnecore;

using SIPSorcery.SIP;

namespace dvmissi
{
    /// <summary>
    /// 
    /// </summary>
    public class ConfigLogObject
    {
        /// <summary>
        /// 
        /// </summary>
        public int DisplayLevel = 1;
        /// <summary>
        /// 
        /// </summary>
        public int FileLevel = 1;
        /// <summary>
        /// 
        /// </summary>
        public string FilePath = ".";
        /// <summary>
        /// 
        /// </summary>
        public string FileRoot = "dvmissi";
    } // public class ConfigLogObject

    /// <summary>
    /// 
    /// </summary>
    public class ConfigurationObject
    {
        /// <summary>
        /// 
        /// </summary>
        public ConfigLogObject Log = new ConfigLogObject();

        /// <summary>
        /// 
        /// </summary>
        public int PingTime = 5;

        /// <summary>
        /// 
        /// </summary>
        public bool RawPacketTrace = false;

        /// <summary>
        /// 
        /// </summary>
        public string Name = "ISSI";
        /// <summary>
        /// 
        /// </summary>
        public uint PeerId;
        /// <summary>
        /// 
        /// </summary>
        public string Address;
        /// <summary>
        /// 
        /// </summary>
        public int Port;
        /// <summary>
        /// 
        /// </summary>
        public string Passphrase;

        /// <summary>
        /// 
        /// </summary>
        public int SipPort = SIPConstants.DEFAULT_SIP_PORT;

        /// <summary>
        /// 
        /// </summary>
        public string RemoteIssiAddress;
        /// <summary>
        /// 
        /// </summary>
        public int RemoteSipPort;

        /*
        ** Methods
        */

        /// <summary>
        /// Helper to convert the <see cref="ConfigPeerObject"/> to a <see cref="PeerDetails"/> object.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public static PeerDetails ConvertToDetails(ConfigurationObject peer)
        {
            PeerDetails details = new PeerDetails();

            // identity
            details.Identity = peer.Name;
            details.RxFrequency = 0;
            details.TxFrequency = 0;

            // system info
            details.Latitude = 0.0d;
            details.Longitude = 0.0d;
            details.Height = 1;
            details.Location = "Digital Network";

            // channel data
            details.TxPower = 0;
            details.TxOffsetMhz = 0.0f;
            details.ChBandwidthKhz = 0.0f;
            details.ChannelID = 0;
            details.ChannelNo = 0;

            // RCON
            details.Password = "ABCD123";
            details.Port = 9990;

            details.Software = AssemblyVersion._VERSION;

            return details;
        }
    } // public class ConfigurationObject
} // namespace dvmissi
