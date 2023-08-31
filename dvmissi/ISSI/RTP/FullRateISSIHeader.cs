/**
* Digital Voice Modem - ISSI
* AGPLv3 Open Source. Use is subject to license terms.
* DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
*
* @package DVM / ISSI
*
*/
/*
*   Copyright (C) 2023 by Bryan Biedenkapp N2PLL
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

using fnecore;
using fnecore.P25;

namespace dvmissi.ISSI.RTP
{
    /// <summary>
    /// Implements a P25 full rate ISSI header packet.
    /// </summary>
    /// 
    /// Byte 0                   1                   2                   3
    /// Bit  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    ///     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    ///     |                   Message Indicator(72 bits)                  |
    ///     |                                                               |
    ///     +               +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    ///     |               |     AlgID     |           Key ID              |
    ///     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    ///     |     MFID      |            Group ID           |   NID(15-8)   |
    ///     +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
    ///     |   NID(7-0)    |SF |VBB| Rsvd  |
    ///     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    public class FullRateISSIHeader
    {
        public const int LENGTH = 18;
        private const int MI_BUF_LEN = 9;

        /// <summary>
        /// Manufacturer ID.
        /// </summary>
        public byte MFId
        {
            get;
            set;
        }

        /// <summary>
        /// Talkgroup ID.
        /// </summary>
        public ushort GroupId
        {
            get;
            set;
        }

        /// <summary>
        /// P25 Network ID.
        /// </summary>
        public ushort NID
        {
            get;
            set;
        }

        /// <summary>
        /// Algorithm ID.
        /// </summary>
        public byte AlgorithmId
        {
            get;
            set;
        }

        /// <summary>
        /// Key ID.
        /// </summary>
        public ushort KeyId
        {
            get;
            set;
        }

        /// <summary>
        /// Encryption MI.
        /// </summary>
        public byte[] MessageIndicator
        {
            get;
            set;
        }

        /// <summary>
        /// Count of voice blocks contained in each voice-bearing packet.
        /// </summary>
        public byte VoiceBlockBundling
        {
            get;
            set;
        }

        /*
        ** Methods
        */

        /// <summary>
        /// Initializes a new instance of the <see cref="FullRateISSIHeader"/> class.
        /// </summary>
        public FullRateISSIHeader()
        {
            MFId = P25Defines.P25_MFG_STANDARD;
            GroupId = 0;
            NID = 0;
            AlgorithmId = P25Defines.P25_ALGO_UNENCRYPT;
            KeyId = 0;
            MessageIndicator = new byte[MI_BUF_LEN];
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="FullRateISSIHeader"/> class.
        /// </summary>
        /// <param name="data"></param>
        public FullRateISSIHeader(byte[] data) : this()
        {
            Decode(data);
        }

        /// <summary>
        /// Decode a block.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool Decode(byte[] data)
        {
            if (data == null)
                return false;

            Buffer.BlockCopy(data, 0, MessageIndicator, 0, MI_BUF_LEN);

            AlgorithmId = data[10U];
            KeyId = FneUtils.ToUInt16(data, 11);
            MFId = data[13U];
            GroupId = FneUtils.ToUInt16(data, 14);
            NID = FneUtils.ToUInt16(data, 16);

            byte sfReserved = data[18];
            VoiceBlockBundling = (byte)((sfReserved & 0x3F) >> 4);

            return true;
        }

        /// <summary>
        /// Encode a block.
        /// </summary>
        /// <param name="data"></param>
        public void Encode(ref byte[] data)
        {
            if (data == null)
                return;

            Buffer.BlockCopy(MessageIndicator, 0, data, 0, MI_BUF_LEN);
            data[10U] = AlgorithmId;
            FneUtils.WriteBytes(KeyId, ref data, 11);
            data[13U] = MFId;
            FneUtils.WriteBytes(GroupId, ref data, 14);
            FneUtils.WriteBytes(NID, ref data, 16);
            data[18U] |= (byte)(VoiceBlockBundling << 4);
        }
    } // public class FullRateISSIHeader
} // namespace dvmissi.ISSI.RTP
