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
using System.Collections.Generic;

using Serilog;

using dvmissi.ISSI.RTP;

namespace dvmissi.ISSI
{
    /// <summary>
    /// This class implements a P25 payload which encapsulates one or more P25 block
    /// objects. The various forms of ISSI RTP Packets are shown below.
    /// 
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  |  RTP Hdr  | |  RTP Hdr  | |  RTP Hdr  | |  RTP Hdr  | |  RTP Hdr  |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  |  Control  | |  Control  | |  Control  | |  Control  | |  Control  |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  |Block Hdrs | |Block Hdrs | |Block Hdrs | |Block Hdrs | |Block Hdrs |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  |Packet Type| |Packet Type| |Packet Type| |Packet Type| |Packet Type|
    ///  | PTT Start | | PTT Grant | | PTT Rqst  | | PTT Mute  | | Heartbeat |
    ///  |           | |    Wait   | | Progress  | |   Unmute  | | HB Query  |
    ///  |           | |    Deny   | |           | |           | |           |
    ///  |           | |    End    | |           | |           | |           |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  | PTT Cntrl | |Opt Manufac| | PTT Cntrl | |Opt Manufac| |Opt Manufac|
    ///  |   Word    | |  Specific | |   Word    | |  Specific | |  Specific |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///        :             :       |Opt ISSI   |       :             :
    ///        :             :       |Hdr Word Bl|       :             :
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///  |Opt Manufac| |Opt Manufac| | Opt IMBE  | |Opt Manufac| |Opt Manufac|
    ///  |  Specific | |  Specific | +-+-+-+-+-+-+ |  Specific | |  Specific |
    ///  +-+-+-+-+-+-+ +-+-+-+-+-+-+ | Opt IMBE  | +-+-+-+-+-+-+ +-+-+-+-+-+-+
    ///                              +-+-+-+-+-+-+
    ///                              | Opt IMBE  |
    ///                              +-+-+-+-+-+-+
    ///                              |Opt Manufac|
    ///                              |  Specific |
    ///                              +-+-+-+-+-+-+
    ///                                    :
    ///                                    :
    ///                              +-+-+-+-+-+-+
    ///                              |Opt Manufac|
    ///                              |  Specific |
    ///                              +-+-+-+-+-+-+ 
    /// </summary>
    public class P25RTPPayload
    {
        public const int MIN_IMBE_BLOCKS_PER_PROG_PKT = 1;
        public const int MAX_IMBE_BLOCKS_PER_PROG_PKT = 3;

        public const int IMBE_TIME_OFFSET = 160;

        /// <summary>
        /// Control Block.
        /// </summary>
        public ControlOctet Control
        {
            get;
            set;
        }

        /// <summary>
        /// Block Headers.
        /// </summary>
        public List<BlockHeader> BlockHeaders
        {
            get;
            set;
        }

        /// <summary>
        /// ISSI Packet Type.
        /// </summary>
        /// <remarks>First P25 block in an RTP packet over ISSI should be the ISSI packet type.</remarks>
        public ISSIPacketType PacketType
        {
            get;
            set;
        }

        /// <summary>
        /// PTT Control Block.
        /// </summary>
        public PTTControl PTT
        {
            get;
            set;
        }

        /// <summary>
        /// Full-rate ISSI Header Block.
        /// </summary>
        public FullRateISSIHeader FullRateISSIHeader
        {
            get;
            set;
        }

        /// <summary>
        /// Full-rate ISSI Voice Blocks.
        /// </summary>
        public List<FullRateVoice> FullRateVoiceBlocks
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<int, int> BlockHeaderToVoiceBlock
        {
            get;
        }

        /*
        ** Methods
        */

        /// <summary>
        /// Initializes a new instance of the <see cref="P25RTPPayload"/> class.
        /// </summary>
        public P25RTPPayload()
        {
            Control = new ControlOctet();
            Control.Signal = false;
            BlockHeaders = new List<BlockHeader>();
            PacketType = null;
            PTT = null;
            FullRateISSIHeader = null;
            FullRateVoiceBlocks = new List<FullRateVoice>();
            BlockHeaderToVoiceBlock = new Dictionary<int, int>();
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="P25RTPPayload"/> class.
        /// </summary>
        public P25RTPPayload(byte[] data) : this()
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

            int offs = 1;

            // decode control block
            Control = new ControlOctet(data[0]);

            // decode block headers
            int blockHeaderCount = Control.BlockHeaderCount;
            for (int i = 0; i < blockHeaderCount; i++)
            {
                byte[] buffer = new byte[BlockHeader.LENGTH];
                Buffer.BlockCopy(data, offs, buffer, 0, BlockHeader.LENGTH);
                BlockHeader header = new BlockHeader(buffer);
                BlockHeaders.Add(header);

                offs += BlockHeader.LENGTH;
            }

            // decode voice blocks
            for (int i = 0; i < blockHeaderCount; i++)
            {
                BlockHeader header = BlockHeaders[i];
                switch (header.Type)
                {
                    case BlockType.PACKET_TYPE:
                        {
                            byte[] buffer = new byte[ISSIPacketType.LENGTH];
                            Buffer.BlockCopy(data, offs, buffer, 0, ISSIPacketType.LENGTH);
                            PacketType = new ISSIPacketType(buffer);
                            offs += ISSIPacketType.LENGTH;
                        }
                        break;
                    case BlockType.RF_PTT_CONTROL_WORD:
                        {
                            byte[] buffer = new byte[PTTControl.LENGTH];
                            Buffer.BlockCopy(data, offs, buffer, 0, PTTControl.LENGTH);
                            PTT = new PTTControl(buffer);
                            offs += PTTControl.LENGTH;
                        }
                        break;
                    case BlockType.FULL_RATE_ISSI_HEADER:
                        {
                            byte[] buffer = new byte[FullRateISSIHeader.LENGTH];
                            Buffer.BlockCopy(data, offs, buffer, 0, FullRateISSIHeader.LENGTH);
                            FullRateISSIHeader = new FullRateISSIHeader(buffer);
                            offs += FullRateISSIHeader.LENGTH;
                        }
                        break;

                    case BlockType.FULL_RATE_VOICE:
                        {
                            byte[] buffer = new byte[header.BlockLength];
                            Buffer.BlockCopy(data, offs, buffer, 0, (int)header.BlockLength);
                            FullRateVoice voice = new FullRateVoice(data);
                            FullRateVoiceBlocks.Add(voice);
                            BlockHeaderToVoiceBlock.Add(i, FullRateVoiceBlocks.Count - 1);
                            offs += (int)header.BlockLength;
                        }
                        break;

                    default:
                        Log.Logger.Error($"Unknown/Unhandled ISSI opcode {header.Type}");
                        break;
                }
            }

            if (FullRateISSIHeader != null)
                FullRateISSIHeader.VoiceBlockBundling = (byte)(FullRateVoiceBlocks.Count - 1);

            return true;
        }

        /// <summary>
        /// Calculate block size.
        /// </summary>
        public int CalculateSize()
        {
            int totalLength = 0;

            int blockHeaderCount = Control.BlockHeaderCount;
            if (BlockHeaders.Count - 1 != blockHeaderCount)
            {
                Log.Logger.Error($"Number of block headers in control octect do not match number of block headers in P25 payload. {BlockHeaders.Count - 1} != {blockHeaderCount}");
                return -1;
            }

            // ensure we have block headers
            if (BlockHeaders.Count == 0)
            {
                Log.Logger.Error($"P25 packet incomplete. No block headers.");
                return -1;
            }

            // ensure we have a ISSI packet type
            if (PacketType == null)
            {
                Log.Logger.Error($"P25 packet incomplete. No packet type.");
                return -1;
            }

            // ensure we have a PTT control block in certain situations
            if (PTT == null &&
                (PacketType.Type == RTP.PacketType.PTT_TRANSMIT_REQ ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_START ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_PROGRESS))
            {
                Log.Logger.Error($"P25 packet incomplete. No PTT control block.");
                return -1;
            }

            // ensure we have the ISSI header in certain situations
            if (FullRateISSIHeader == null &&
                (PacketType.Type == RTP.PacketType.PTT_TRANSMIT_REQ ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_PROGRESS))
            {
                Log.Logger.Error($"P25 packet incomplete. No ISSI header block for PTT request or progress frames.");
                return -1;
            }

            // encode control octet
            totalLength += ControlOctet.LENGTH;

            // encode block headers
            foreach (BlockHeader header in BlockHeaders)
                totalLength += BlockHeader.LENGTH;

            // encode ISSI packet type block
            totalLength += ISSIPacketType.LENGTH;

            // encode PTT control block
            if (PTT != null)
                totalLength += PTTControl.LENGTH;

            // encode ISSI header
            if (FullRateISSIHeader != null)
                totalLength += FullRateISSIHeader.LENGTH;

            // encode voice frames
            if (FullRateVoiceBlocks.Count > 0)
            {
                foreach (FullRateVoice voice in FullRateVoiceBlocks)
                    totalLength += voice.Size();
            }

            return totalLength;
        }

        /// <summary>
        /// Encode a block.
        /// </summary>
        /// <param name="data"></param>
        public void Encode(ref byte[] data)
        {
            if (data == null)
                return;

            int offs = 0;

            int blockHeaderCount = Control.BlockHeaderCount;
            if (BlockHeaders.Count - 1 != blockHeaderCount)
            {
                Log.Logger.Error($"Number of block headers in control octect do not match number of block headers in P25 payload. {BlockHeaders.Count - 1} != {blockHeaderCount}");
                return;
            }

            // ensure we have block headers
            if (BlockHeaders.Count == 0)
            {
                Log.Logger.Error($"P25 packet incomplete. No block headers.");
                return;
            }

            // ensure we have a ISSI packet type
            if (PacketType == null)
            {
                Log.Logger.Error($"P25 packet incomplete. No packet type.");
                return;
            }

            // ensure we have a PTT control block in certain situations
            if (PTT == null && 
                (PacketType.Type == RTP.PacketType.PTT_TRANSMIT_REQ ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_START ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_PROGRESS))
            {
                Log.Logger.Error($"P25 packet incomplete. No PTT control block.");
                return;
            }

            // ensure we have the ISSI header in certain situations
            if (FullRateISSIHeader == null &&
                (PacketType.Type == RTP.PacketType.PTT_TRANSMIT_REQ ||
                 PacketType.Type == RTP.PacketType.PTT_TRANSMIT_PROGRESS))
            {
                Log.Logger.Error($"P25 packet incomplete. No ISSI header block for PTT request or progress frames.");
                return;
            }

            byte[] buffer = null;

            // encode control octet
            byte controlByte = 0;
            Control.Encode(ref controlByte);
            data[0] = controlByte;
            offs += ControlOctet.LENGTH;

            // encode block headers
            uint blockBufLen = (uint)(blockHeaderCount * BlockHeader.LENGTH);
            buffer = new byte[blockBufLen];
            int blockOffs = 0;
            foreach (BlockHeader header in BlockHeaders)
            {
                byte[] blockBuf = new byte[BlockHeader.LENGTH];
                header.Encode(ref blockBuf);
                Buffer.BlockCopy(blockBuf, 0, buffer, blockOffs, BlockHeader.LENGTH);
                blockOffs += BlockHeader.LENGTH;
            }

            Buffer.BlockCopy(buffer, 0, data, offs, buffer.Length);
            offs += buffer.Length;

            // encode ISSI packet type block
            buffer = new byte[ISSIPacketType.LENGTH];
            PacketType.Encode(ref buffer);
            Buffer.BlockCopy(buffer, 0, data, offs, ISSIPacketType.LENGTH);
            offs += ISSIPacketType.LENGTH;

            // encode PTT control block
            if (PTT != null)
            {
                buffer = new byte[PTTControl.LENGTH];
                PTT.Encode(ref buffer);
                Buffer.BlockCopy(buffer, 0, data, offs, PTTControl.LENGTH);
                offs += PTTControl.LENGTH;
            }

            // encode ISSI header
            if (FullRateISSIHeader != null)
            {
                buffer = new byte[FullRateISSIHeader.LENGTH];
                FullRateISSIHeader.Encode(ref buffer);
                Buffer.BlockCopy(buffer, 0, data, offs, FullRateISSIHeader.LENGTH);
                offs += FullRateISSIHeader.LENGTH;
            }

            // encode voice frames
            if (FullRateVoiceBlocks.Count > 0)
            {
                foreach (FullRateVoice voice in FullRateVoiceBlocks)
                {
                    byte[] voiceBuf = new byte[voice.Size()];
                    voice.Encode(ref voiceBuf);
                    Buffer.BlockCopy(voiceBuf, 0, data, offs, voice.Size());
                    offs += voice.Size();
                }
            }
        }
    } // public class P25RTPPayload
} // namespace dvmissi.ISSI
