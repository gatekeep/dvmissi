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

using dvmissi.FNE.P25;

namespace dvmissi.ISSI.RTP
{
    /// <summary>
    /// Implements a P25 full rate voice packet.
    /// </summary>
    public class FullRateVoice
    {
        private const int IMBE_BUF_LEN = 11;

        /// <summary>
        /// Frame type.
        /// </summary>
        public byte FrameType
        {
            get;
            set;
        }

        /// <summary>
        /// Message Vectors.
        /// </summary>
        public ushort[] MessageVectors
        {
            get;
            private set;
        }

        /// <summary>
        /// Total errors detected in the frame.
        /// </summary>
        public byte TotalErrors
        {
            get;
            set;
        }

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
        /// Superframe Counter.
        /// </summary>
        public byte SuperFrameCnt
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public byte[] AdditionalFrameData
        {
            get;
            set;
        }

        /*
        ** Methods
        */
        /// <summary>
        /// Initializes a new instance of the <see cref="FullRateVoice"/> class.
        /// </summary>
        public FullRateVoice()
        {
            FrameType = P25ISSI.P25_DFSI_LDU1_VOICE1;
            TotalErrors = 0;
            MuteFrame = false;
            LostFrame = false;
            SuperFrameCnt = 0;
            AdditionalFrameData = null;

            ResetVectors();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetVectors()
        {
            MessageVectors = new ushort[8];
            for (int i = 0; i < 8; i++)
                MessageVectors[i] = 0;
        }

        /// <summary>
        /// Reassembles the 11-byte IMBE frame from the 8 ISSI message vectors.
        /// </summary>
        /// <returns></returns>
        public byte[] GetIMBE()
        {
            byte[] imbe = new byte[IMBE_BUF_LEN];

            imbe[0] = (byte)((MessageVectors[0] >> 4) & 0xFFU);
            imbe[1] = (byte)((MessageVectors[0] & 0x0FU) + ((MessageVectors[1] >> 8) & 0x0FU));
            imbe[2] = (byte)(MessageVectors[1] & 0xFFU);
            imbe[3] = (byte)((MessageVectors[2] >> 4) & 0xFFU);
            imbe[4] = (byte)((MessageVectors[2] & 0x0FU) + ((MessageVectors[3] >> 8) & 0x0FU));
            imbe[5] = (byte)(MessageVectors[3] & 0xFFU);

            imbe[6] = (byte)((MessageVectors[4] >> 3) & 0xFFU);
            imbe[7] = (byte)(((MessageVectors[4] << 5) & 0xE0U) + ((MessageVectors[5] >> 6) & 0x1FU));
            imbe[8] = (byte)(((MessageVectors[5] << 2) & 0xFCU) + ((MessageVectors[6] >> 9) & 0x03U));
            imbe[9] = (byte)((MessageVectors[6] >> 1) & 0xFFU);
            imbe[10] = (byte)(((MessageVectors[6] << 7) & 0x80U) + (MessageVectors[7] & 0x7F));

            return imbe;
        }

        /// <summary>
        /// Assembles an 11-byte IMBE frame into 8 ISSI message vectors.
        /// </summary>
        /// <param name="imbe"></param>
        public void SetIMBE(byte[] imbe)
        {
            if (imbe == null)
                return;

            ResetVectors();

            MessageVectors[0] = (ushort)((imbe[0] << 4) + ((imbe[1] & 0xF0U) >> 4));
            MessageVectors[1] = (ushort)(((imbe[1] & 0x0FU) << 8) + (imbe[2]));
            MessageVectors[2] = (ushort)((imbe[3] << 4) + ((imbe[4] & 0xF0U) >> 4));
            MessageVectors[3] = (ushort)(((imbe[4] & 0x0FU) << 8) + (imbe[5]));

            MessageVectors[4] = (ushort)((imbe[6] << 3) + ((imbe[7] & 0xE0U) >> 5));
            MessageVectors[5] = (ushort)(((imbe[7] & 0x1FU) << 6) + ((imbe[8] & 0xFCU) >> 2));
            MessageVectors[6] = (ushort)(((imbe[8] & 0x03U) << 9) + (imbe[9] << 1) + ((imbe[10] & 0x80U) >> 7));
            MessageVectors[7] = (ushort)(imbe[10] & 0x7F);
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

            ResetVectors();

            ulong value = 0U;

            // extract frame type, U0 and U1
            // combine bytes into ulong (8 byte) value
            value = data[0U];
            value = (value << 8) + data[1U];
            value = (value << 8) + data[2U];
            value = (value << 8) + data[3U];

            FrameType = (byte)((value >> 24) & 0xFFU);                          // Frame Type
            MessageVectors[0] = (ushort)((value >> 12) & 0xFFFU);               // U0
            MessageVectors[1] = (ushort)(value & 0xFFFU);                       // U1

            // extract U2..U7
            value = 0U;

            // combine bytes into ulong (8 byte) value
            value = data[4U];
            value = (value << 8) + data[5U];
            value = (value << 8) + data[6U];
            value = (value << 8) + data[7U];
            value = (value << 8) + data[8U];
            value = (value << 8) + data[9U];
            value = (value << 8) + data[10U];
            value = (value << 8) + data[11U];

            MessageVectors[2] = (ushort)((value >> 52) & 0xFFFU);               // U2
            MessageVectors[3] = (ushort)((value >> 40) & 0xFFFU);               // U3
            MessageVectors[4] = (ushort)((value >> 29) & 0x7FFU);               // U4
            MessageVectors[5] = (ushort)((value >> 18) & 0x7FFU);               // U5
            MessageVectors[6] = (ushort)((value >> 7) & 0x7FFU);                // U6
            MessageVectors[7] = (ushort)(value & 0x7FU);                        // U7

            // extract remaining elements
            value = 0U;

            // combine bytes into ulong (8 byte) value
            value = data[12U];
            value = (value << 8) + data[13U];

            TotalErrors = (byte)((value >> 13) & 0x07U);                        // Total Errors
            MuteFrame = (value & 0x200U) == 0x200U;                             // Mute Frame Flag
            LostFrame = (value & 0x100U) == 0x100U;                             // Lost Frame Flag
            SuperFrameCnt = (byte)((value >> 2) & 0x03U);                       // Superframe Counter

            // extract additional frame data
            if (data.Length > 14U)
            {
                AdditionalFrameData = new byte[data.Length - 14U];
                Buffer.BlockCopy(data, 14, AdditionalFrameData, 0, AdditionalFrameData.Length);
            }
            else
                AdditionalFrameData = null;

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

            ulong value = 0U;

            // pack frame type, U0 and U1
            value = (ulong)(FrameType);                                         // Frame Type
            value = (value << 12) + (ulong)(MessageVectors[0] & 0xFFFU);        // U0
            value = (value << 12) + (ulong)(MessageVectors[1] & 0xFFFU);        // U1

            // split ulong (8 byte) value into bytes
            data[0U] = (byte)((value >> 24) & 0xFFU);
            data[1U] = (byte)((value >> 16) & 0xFFU);
            data[2U] = (byte)((value >> 8) & 0xFFU);
            data[3U] = (byte)((value >> 0) & 0xFFU);

            // pack U2..U7
            value = 0U;
            value = (ulong)(MessageVectors[2] & 0xFFFU);                        // U2
            value = (value << 12) + (ulong)(MessageVectors[3] & 0xFFFU);        // U3
            value = (value << 11) + (ulong)(MessageVectors[4] & 0x7FFU);        // U4
            value = (value << 11) + (ulong)(MessageVectors[5] & 0x7FFU);        // U5
            value = (value << 11) + (ulong)(MessageVectors[6] & 0x7FFU);        // U6
            value = (value << 7) + (ulong)(MessageVectors[7] & 0x7FU);          // U7

            // split ulong (8 byte) value into bytes
            data[4U] = (byte)((value >> 56) & 0xFFU);
            data[5U] = (byte)((value >> 48) & 0xFFU);
            data[6U] = (byte)((value >> 40) & 0xFFU);
            data[7U] = (byte)((value >> 32) & 0xFFU);
            data[8U] = (byte)((value >> 24) & 0xFFU);
            data[9U] = (byte)((value >> 16) & 0xFFU);
            data[10U] = (byte)((value >> 8) & 0xFFU);
            data[11U] = (byte)((value >> 0) & 0xFFU);

            // pack remaining elements
            value = 0U;
            value = (ulong)(TotalErrors);                                       // Total Errors
            value = (value << 13) +
                (ulong)(SuperFrameCnt & 0x03U);                                 // Superframe Count
            value |= (MuteFrame ? 0x200U : 0x00U);                              // Mute Frame Flag
            value |= (LostFrame ? 0x100U : 0x00U);                              // Log Frame Flag

            // split ulong (8 byte) value into bytes
            data[12U] = (byte)((value >> 8) & 0xFFU);
            data[13U] = (byte)((value >> 0) & 0xFFU);

            if (AdditionalFrameData != null)
            {
                if (data.Length >= 14U + AdditionalFrameData.Length)
                    Buffer.BlockCopy(AdditionalFrameData, 0, data, 14, AdditionalFrameData.Length);
            }
        }
    } // public class FullRateVoice
} // namespace dvmissi.ISSI.RTP
