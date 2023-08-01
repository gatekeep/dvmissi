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
    /// Implements a P25 control octet packet.
    /// </summary>
    public class ControlOctet
    {
        /// <summary>
        /// 
        /// </summary>
        public bool Signal
        {
            get;
            set;
        }

        /// <summary>
        /// Indicates a compact (1) or verbose (0) block header.
        /// </summary>
        public bool Compact
        {
            get;
            set;
        }

        /// <summary>
        /// Number of block headers following this control octet.
        /// </summary>
        public byte BlockHeaderCount
        {
            get;
            set;
        }

        /*
        ** Methods
        */
        /// <summary>
        /// Initializes a new instance of the <see cref="ControlOctet"/> class.
        /// </summary>
        public ControlOctet()
        {
            Signal = false;
            Compact = false;
            BlockHeaderCount = 0;
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

            Signal = (data[0] & 0x07) == 0x07;                                  // Signal Flag
            Compact = (data[0] & 0x06) == 0x06;                                 // Compact Flag
            BlockHeaderCount = (byte)(data[0] & 0x3F);                          // Block Header Count

            return true;
        }

        /// <summary>
        /// Encode a RTP header.
        /// </summary>
        /// <param name="data"></param>
        public void Encode(ref byte[] data)
        {
            if (data == null)
                return;

            data[0] = (byte)((Signal ? 0x07U : 0x00U) +                         // Signal Flag
                (Compact ? 0x06U : 0x00U) +                                     // Control Flag
                (BlockHeaderCount & 0x3F));
        }
    } // public class ControlOctet
} // namespace dvmissi.ISSI.RTP
