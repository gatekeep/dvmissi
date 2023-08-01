/**
* Digital Voice Modem - Fixed Network Equipment
* AGPLv3 Open Source. Use is subject to license terms.
* DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
*
* @package DVM / Fixed Network Equipment
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
    /// P25 ISSI Frame Types
    /// </summary>
    public class P25ISSI
    {
        public const byte P25_DFSI_LDU1_VOICE1 = 0xC2;       // IMBE LDU1 - Voice 1
        public const byte P25_DFSI_LDU1_VOICE2 = 0xC3;       // IMBE LDU1 - Voice 2
        public const byte P25_DFSI_LDU1_VOICE3 = 0xC4;       // IMBE LDU1 - Voice 3 + Link Control
        public const byte P25_DFSI_LDU1_VOICE4 = 0xC5;       // IMBE LDU1 - Voice 4 + Link Control
        public const byte P25_DFSI_LDU1_VOICE5 = 0xC6;       // IMBE LDU1 - Voice 5 + Link Control
        public const byte P25_DFSI_LDU1_VOICE6 = 0xC7;       // IMBE LDU1 - Voice 6 + Link Control
        public const byte P25_DFSI_LDU1_VOICE7 = 0xC8;       // IMBE LDU1 - Voice 7 + Link Control
        public const byte P25_DFSI_LDU1_VOICE8 = 0xC9;       // IMBE LDU1 - Voice 8 + Link Control
        public const byte P25_DFSI_LDU1_VOICE9 = 0xCA;       // IMBE LDU1 - Voice 9 + Low Speed Data

        public const byte P25_DFSI_LDU2_VOICE10 = 0xCB;      // IMBE LDU2 - Voice 10
        public const byte P25_DFSI_LDU2_VOICE11 = 0xCC;      // IMBE LDU2 - Voice 11
        public const byte P25_DFSI_LDU2_VOICE12 = 0x6D;      // IMBE LDU2 - Voice 12 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE13 = 0x6E;      // IMBE LDU2 - Voice 13 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE14 = 0x6F;      // IMBE LDU2 - Voice 14 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE15 = 0x70;      // IMBE LDU2 - Voice 15 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE16 = 0x71;      // IMBE LDU2 - Voice 16 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE17 = 0x72;      // IMBE LDU2 - Voice 17 + Encryption Sync
        public const byte P25_DFSI_LDU2_VOICE18 = 0x73;      // IMBE LDU2 - Voice 18 + Low Speed Data
    } // public class P25ISSI
} // namespace dvmissi.ISSI.RTP
