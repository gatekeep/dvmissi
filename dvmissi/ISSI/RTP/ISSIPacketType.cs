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

namespace dvmissi.ISSI.RTP
{
    /// <summary>
    /// 
    /// </summary>
    public enum PacketType : byte
    {
        PTT_TRANSMIT_REQ = 0,
        PTT_TRANSMIT_GRANT,
        PTT_TRANSMIT_PROGRESS,
        PTT_TRANSMIT_END,
        PTT_TRANSMIT_START,
        PTT_TRANSMIT_MUTE,
        PTT_TRANSMIT_UNMUTE,
        PTT_TRANSMIT_WAIT,
        PTT_TRANSMIT_DENY,
        PTT_HEARTBEAT,
        PTT_HEARTBEAT_QUERY,

        UNDEFINED = 127
    } // public enum PacketType : byte

    /// <summary>
    /// Implements a P25 ISSI packet type packet.
    /// </summary>
    /// 
    /// Byte 0                   1                   2                   3
    /// Bit  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    ///     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    ///     |M|     PT      |       SO      |    TSN      |L|   Interval    |
    ///     +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    public class ISSIPacketType
    {
        public const int LENGTH = 4;

        /// <summary>
        /// Flag indicating the frame should be muted.
        /// </summary>
        public bool MuteFrame
        {
            get;
            set;
        }

        /// <summary>
        /// Flag indicating the frame was lost.
        /// </summary>
        public bool LostFrame
        {
            get;
            set;
        }

        /// <summary>
        /// Identifies the RTP packet type.
        /// </summary>
        public PacketType Type
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public byte ServiceOptions
        {
            get;
            set;
        }

        /// <summary>
        /// Transmission Sequence Number.
        /// </summary>
        public byte TSN
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public byte Interval
        {
            get;
            set;
        }

        /*
        ** Methods
        */

        /// <summary>
        /// Initializes a new instance of the <see cref="ISSIPacketType"/> class.
        /// </summary>
        public ISSIPacketType()
        {
            MuteFrame = false;
            LostFrame = false;
            Type = PacketType.UNDEFINED;
            ServiceOptions = 0;
            TSN = 0;
            Interval = 0;
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="ISSIPacketType"/> class.
        /// </summary>
        /// <param name="data"></param>
        public ISSIPacketType(byte[] data)
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

            ulong value = 0U;

            // combine bytes into ulong (8 byte) value
            value = data[0U];
            value = (value << 8) + data[1U];
            value = (value << 8) + data[2U];
            value = (value << 8) + data[3U];

            MuteFrame = ((value >> 24) & 0x80U) == 0x80U;                       // Mute Frame Flag
            LostFrame = ((value >> 8) & 0x01U) == 0x01U;                        // Lost Frame Flag
            Type = (PacketType)((value >> 24) & 0x7FU);                         // Packet Type
            ServiceOptions = (byte)((value >> 16) & 0xFFU);                     // Service Options
            TSN = (byte)((value >> 9) & 0x7FU);                                 // Transmission Sequence Number
            Interval = (byte)(value & 0xFFU);                                   // Interval

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

            ulong value = 0;

            value = (ulong)((MuteFrame ? 0x80U : 0x00U) +                       // Mute Frame Flag
                ((byte)Type & 0x7FU));                                          // Packet Type
            value = (value << 24) + (ServiceOptions & 0xFFU);                   // Service Options
            value = (value << 8) + (TSN & 0xFFU);                               // Transmission Sequence Number
            value = (value << 1) + (LostFrame ? 0x01U : 0x00U);                 // Lost Frame Flag
            value = (value << 8) + (Interval & 0xFFU);                          // Interval

            // split ulong (8 byte) value into bytes
            data[0U] = (byte)((value >> 24) & 0xFFU);
            data[1U] = (byte)((value >> 16) & 0xFFU);
            data[2U] = (byte)((value >> 8) & 0xFFU);
            data[3U] = (byte)((value >> 0) & 0xFFU);
        }
    } // public class ISSIPacketType
} // namespace dvmissi.ISSI.RTP
