/**
* Digital Voice Modem - ISSI
* AGPLv3 Open Source. Use is subject to license terms.
* DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
*
* @package DVM / ISSI
*
*/
/*
*   Copyright (C) 2022-2023 by Bryan Biedenkapp N2PLL
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using Serilog;

using fnecore;
using fnecore.DMR;
using fnecore.P25;

using dvmissi.ISSI;
using dvmissi.ISSI.RTP;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

namespace dvmissi
{
    /// <summary>
    /// Metadata class containing remote call data.
    /// </summary>
    public class RemoteCallData
    {
        /// <summary>
        /// Source ID.
        /// </summary>
        public uint SrcId = 0;
        /// <summary>
        /// Destination ID.
        /// </summary>
        public uint DstId = 0;

        /// <summary>
        /// Link-Control Opcode.
        /// </summary>
        public byte LCO = 0;
        /// <summary>
        /// Manufacturer ID.
        /// </summary>
        public byte MFId = 0;
        /// <summary>
        /// Service Options.
        /// </summary>
        public byte ServiceOptions = 0;

        /// <summary>
        /// Low-speed Data Byte 1
        /// </summary>
        public byte LSD1 = 0;
        /// <summary>
        /// Low-speed Data Byte 2
        /// </summary>
        public byte LSD2 = 0;

        /// <summary>
        /// Encryption Message Indicator
        /// </summary>
        public byte[] MessageIndicator = new byte[P25Defines.P25_MI_LENGTH];

        /// <summary>
        /// Algorithm ID.
        /// </summary>
        public byte AlgorithmId = P25Defines.P25_ALGO_UNENCRYPT;
        /// <summary>
        /// Key ID.
        /// </summary>
        public ushort KeyId = 0;

        /*
        ** Methods
        */

        /// <summary>
        /// Reset values.
        /// </summary>
        public void Reset()
        {
            SrcId = 0;
            DstId = 0;

            LCO = 0;
            MFId = 0;
            ServiceOptions = 0;

            LSD1 = 0;
            LSD2 = 0;

            MessageIndicator = new byte[P25Defines.P25_MI_LENGTH];

            AlgorithmId = P25Defines.P25_ALGO_UNENCRYPT;
            KeyId = 0;
        }
    } // public class RemoteCallData

    /// <summary>
    /// Implements a FNE system.
    /// </summary>
    public abstract partial class FneSystemBase
    {
        private const int P25_FIXED_SLOT = 2;

        private const string SIP_ISSI_P25DR = "p25dr";

        private const string SIP_ISSI_P25_GROUP_CALL = "TIA-P25-Groupcall";
        private const string SIP_ISSI_P25_U2U_CALLING = "TIA-P25-U2Uorig";
        private const string SIP_ISSI_P25_U2U_CALLED = "TIA-P25-U2Udest";
        private const string SIP_ISSI_P25_RSSI_CAP = "TIA-P25-RFSSCapability";

        private const string SIP_ISSI_CONTENT_TYPE = "x-tia-p25-issi";

        private const string SIP_ISSI_P25_SU = "TIA-P25-SU";
        private const string SIP_ISSI_P25_SG = "TIA-P25-SG";

        protected FneBase fne;

        private Random rand;
        private uint txStreamId;

        private bool callInProgress = false;

        private bool remoteCallInProgress = false;
        public RemoteCallData remoteCallData = new RemoteCallData();

        private SIPTransport sipTransport;
        private SIPChannel sipListener;
        
        private ConcurrentDictionary<string, SIPUserAgent> calls = new ConcurrentDictionary<string, SIPUserAgent>();

        private ConcurrentDictionary<string, uint> callIdToStreamId = new ConcurrentDictionary<string, uint>();
        private ConcurrentDictionary<uint, SIPUserAgent> outgoingCalls = new ConcurrentDictionary<uint, SIPUserAgent>();
        private ConcurrentDictionary<uint, RTPSession> outgoingRtp = new ConcurrentDictionary<uint, RTPSession>();

        /*
        ** Properties
        */

        /// <summary>
        /// Gets the system name for this <see cref="FneSystemBase"/>.
        /// </summary>
        public string SystemName
        {
            get
            {
                if (fne != null)
                    return fne.SystemName;
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the peer ID for this <see cref="FneSystemBase"/>.
        /// </summary>
        public uint PeerId
        {
            get
            {
                if (fne != null)
                    return fne.PeerId;
                return uint.MaxValue;
            }
        }

        /// <summary>
        /// Flag indicating whether this <see cref="FneSystemBase"/> is running.
        /// </summary>
        public bool IsStarted
        { 
            get
            {
                if (fne != null)
                    return fne.IsStarted;
                return false;
            }
        }

        /// <summary>
        /// Gets the <see cref="FneType"/> this <see cref="FneBase"/> is.
        /// </summary>
        public FneType FneType
        {
            get
            {
                if (fne != null)
                    return fne.FneType;
                return FneType.UNKNOWN;
            }
        }

        /*
        ** Methods
        */

        /// <summary>
        /// Initializes a new instance of the <see cref="FneSystemBase"/> class.
        /// </summary>
        /// <param name="fne">Instance of <see cref="FneMaster"/> or <see cref="FnePeer"/></param>
        public FneSystemBase(FneBase fne)
        {
            this.fne = fne;

            this.rand = new Random(Guid.NewGuid().GetHashCode());

            // hook various FNE network callbacks
            this.fne.DMRDataValidate = DMRDataValidate;
            this.fne.DMRDataReceived += DMRDataReceived;

            this.fne.P25DataValidate = P25DataValidate;
            this.fne.P25DataPreprocess += P25DataPreprocess;
            this.fne.P25DataReceived += P25DataReceived;

            this.fne.NXDNDataValidate = NXDNDataValidate;
            this.fne.NXDNDataReceived += NXDNDataReceived;

            this.fne.PeerIgnored = PeerIgnored;
            this.fne.PeerConnected += PeerConnected;

            // hook logger callback
            this.fne.LogLevel = Program.FneLogLevel;
            this.fne.Logger = (LogLevel level, string message) =>
            {
                switch (level)
                {
                    case LogLevel.WARNING:
                        Log.Logger.Warning(message);
                        break;
                    case LogLevel.ERROR:
                        Log.Logger.Error(message);
                        break;
                    case LogLevel.DEBUG:
                        Log.Logger.Debug(message);
                        break;
                    case LogLevel.FATAL:
                        Log.Logger.Fatal(message);
                        break;
                    case LogLevel.INFO:
                    default:
                        Log.Logger.Information(message);
                        break;
                }
            };

            netLDU1 = new byte[9 * 25];
            netLDU2 = new byte[9 * 25];

            sipTransport = new SIPTransport();
        }

        /// <summary>
        /// Starts the main execution loop for this <see cref="FneSystemBase"/>.
        /// </summary>
        public void Start()
        {
            if (!fne.IsStarted)
                fne.Start();

            // initialize SIP transport
            sipListener = new SIPUDPChannel(new IPEndPoint(IPAddress.Any, Program.Configuration.SipPort));
            sipTransport.AddSIPChannel(sipListener);

            sipTransport.SIPTransportRequestReceived += SipTransport_SIPTransportRequestReceived;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ua"></param>
        /// <param name="dst"></param>
        /// <returns></returns>
        private RTPSession CreateRTPSession(SIPUserAgent ua, string dst)
        {
            RTPSession session = new RTPSession(false, false, false);
            session.AcceptRtpFromAny = true; // hmmm....

            session.OnRtpPacketReceived += (ep, type, rtp) => OnRtpPacketReceived(ua, type, rtp);
            session.OnTimeout += (mediaType) =>
            {
                if (ua?.Dialogue != null)
                    Log.Logger.Warning($"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up.");
                else
                    Log.Logger.Warning($"RTP timeout on incomplete call, closing RTP session.");
                ua.Hangup();
            };

            return session;
        }

        /// <summary>
        /// Handler for RTP packet requests.
        /// </summary>
        /// <param name="ua">The SIP user agent associated with the RTP session.</param>
        /// <param name="type">The media type of the RTP packet (audio or video).</param>
        /// <param name="rtpPacket">The RTP packet received from the remote party.</param>
        private void OnRtpPacketReceived(SIPUserAgent ua, SDPMediaTypesEnum type, RTPPacket rtpPacket)
        {
            if (callInProgress)
                return;

            P25RTPPayload payload = new P25RTPPayload(rtpPacket.GetBytes());
            for (int i = 0; i < payload.BlockHeaders.Count; i++)
            {
                BlockHeader header = payload.BlockHeaders[i];
                switch (header.Type)
                {
                    case BlockType.START_OF_STREAM:
                        {
                            if (txStreamId == 0)
                            {
                                txStreamId = (uint)rand.Next(int.MinValue, int.MaxValue);
                                remoteCallInProgress = true;
                                remoteCallData.Reset();
                                Log.Logger.Information($"({SystemName}) ISSI Traffic *CALL START     * [STREAM ID {txStreamId}]");
                            }
                        }
                        break;
                    case BlockType.END_OF_STREAM:
                        {
                            Log.Logger.Information($"({SystemName}) ISSI Traffic *CALL END       * [STREAM ID {txStreamId}]");
                            txStreamId = 0;
                            remoteCallInProgress = false;
                            remoteCallData.Reset();
                        }
                        break;

                    case BlockType.FULL_RATE_VOICE:
                        {
                            int blkIdx = payload.BlockHeaderToVoiceBlock[i];
                            FullRateVoice voice = payload.FullRateVoiceBlocks[blkIdx];

                            switch (voice.FrameType)
                            {
                                // LDU1
                                case P25ISSI.P25_DFSI_LDU1_VOICE1:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 10, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE2:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 26, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE3:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 55, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                            {
                                                remoteCallData.LCO = voice.AdditionalFrameData[0];
                                                remoteCallData.MFId = voice.AdditionalFrameData[1];
                                                remoteCallData.ServiceOptions = voice.AdditionalFrameData[3];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC3 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE4:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 80, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                                remoteCallData.DstId = FneUtils.Bytes3ToUInt32(voice.AdditionalFrameData, 0);
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC4 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE5:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 105, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                                remoteCallData.SrcId = FneUtils.Bytes3ToUInt32(voice.AdditionalFrameData, 0);
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC5 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE6:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 130, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE7:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 155, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE8:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 180, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU1_VOICE9:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU1, 204, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 2)
                                            {
                                                remoteCallData.LSD1 = voice.AdditionalFrameData[0];
                                                remoteCallData.LSD2 = voice.AdditionalFrameData[1];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC9 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;

                                // LDU2
                                case P25ISSI.P25_DFSI_LDU2_VOICE10:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 10, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE11:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 26, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE12:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 55, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                            {
                                                remoteCallData.MessageIndicator[0] = voice.AdditionalFrameData[0];
                                                remoteCallData.MessageIndicator[1] = voice.AdditionalFrameData[1];
                                                remoteCallData.MessageIndicator[2] = voice.AdditionalFrameData[2];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC12 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE13:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 80, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                            {
                                                remoteCallData.MessageIndicator[3] = voice.AdditionalFrameData[0];
                                                remoteCallData.MessageIndicator[4] = voice.AdditionalFrameData[1];
                                                remoteCallData.MessageIndicator[5] = voice.AdditionalFrameData[2];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC13 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE14:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 105, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                            {
                                                remoteCallData.MessageIndicator[6] = voice.AdditionalFrameData[0];
                                                remoteCallData.MessageIndicator[7] = voice.AdditionalFrameData[1];
                                                remoteCallData.MessageIndicator[8] = voice.AdditionalFrameData[2];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC14 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE15:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 130, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 3)
                                            {
                                                remoteCallData.AlgorithmId = voice.AdditionalFrameData[0];
                                                remoteCallData.KeyId = FneUtils.ToUInt16(voice.AdditionalFrameData, 1);
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC15 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE16:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 155, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE17:
                                    Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 180, IMBE_BUF_LEN);
                                    break;
                                case P25ISSI.P25_DFSI_LDU2_VOICE18:
                                    {
                                        Buffer.BlockCopy(voice.IMBE, 0, netLDU2, 204, IMBE_BUF_LEN);
                                        if (voice.AdditionalFrameData != null)
                                        {
                                            if (voice.AdditionalFrameData.Length >= 2)
                                            {
                                                remoteCallData.LSD1 = voice.AdditionalFrameData[0];
                                                remoteCallData.LSD2 = voice.AdditionalFrameData[1];
                                            }
                                            else
                                                Log.Logger.Warning($"({SystemName}) ISSI Traffic *TRAFFIC        * VC18 Missing Metadata [STREAM ID {txStreamId}]");
                                        }
                                    }
                                    break;
                            }

                            FnePeer peer = (FnePeer)fne;

                            // send P25 LDU1
                            if (p25N == 8U)
                            {
                                ushort pktSeq = 0;
                                if (p25SeqNo == 0U)
                                    pktSeq = peer.pktSeq(true);
                                else
                                    pktSeq = peer.pktSeq();

                                Log.Logger.Information($"({SystemName}) P25D: Traffic *VOICE FRAME    * PEER {fne.PeerId} SRC_ID {remoteCallData.SrcId} TGID {remoteCallData.DstId} [STREAM ID {txStreamId}]");

                                byte[] buffer = new byte[200];
                                CreateP25MessageHdr((byte)P25DUID.LDU1, remoteCallData, ref buffer);
                                CreateP25LDU1Message(ref buffer, remoteCallData);

                                peer.SendMaster(new Tuple<byte, byte>(Constants.NET_FUNC_PROTOCOL, Constants.NET_PROTOCOL_SUBFUNC_P25), buffer, pktSeq, txStreamId);
                            }

                            // send P25 LDU2
                            if (p25N == 17U)
                            {
                                ushort pktSeq = 0;
                                if (p25SeqNo == 0U)
                                    pktSeq = peer.pktSeq(true);
                                else
                                    pktSeq = peer.pktSeq();

                                Log.Logger.Information($"({SystemName}) P25D: Traffic *VOICE FRAME    * PEER {fne.PeerId} SRC_ID {remoteCallData.SrcId} TGID {remoteCallData.DstId} [STREAM ID {txStreamId}]");

                                byte[] buffer = new byte[200];
                                CreateP25MessageHdr((byte)P25DUID.LDU2, remoteCallData, ref buffer);
                                CreateP25LDU2Message(ref buffer, remoteCallData);

                                peer.SendMaster(new Tuple<byte, byte>(Constants.NET_FUNC_PROTOCOL, Constants.NET_PROTOCOL_SUBFUNC_P25), buffer, pktSeq, txStreamId);
                            }

                            p25SeqNo++;
                            p25N++;
                        }
                        break;

                    default:
                        Log.Logger.Error($"Unknown/Unhandled DFSI opcode {header.Type}");
                        break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sysId"></param>
        /// <param name="netId"></param>
        /// <returns></returns>
        private string GenerateISSIDomain(uint sysId, uint netId)
        {
            // bryanb: the TIA-102 engineers were taking the good stuff when they came up with this shit...
            string systemName = $"{sysId.ToString("X3")}.{netId.ToString("X5")}.";
            return $"{systemName}{SIP_ISSI_P25DR}";
        }

        /// <summary>
        /// Helper to generate a ISSI SIP INVITE route header.
        /// </summary>
        /// <param name="sysId"></param>
        /// <param name="netId"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        private string GenerateISSIInviteRoute(uint sysId, uint netId, bool group = true)
        {
            // bryanb: the TIA-102 engineers were taking the good stuff when they came up with this shit...
            string sipRoute = $"sip:{SIP_ISSI_P25_GROUP_CALL}@{GenerateISSIDomain(sysId, netId)}";
            if (!group)
                sipRoute = $"sip:{SIP_ISSI_P25_U2U_CALLING}@{GenerateISSIDomain(sysId, netId)}";

            return sipRoute;
        }

        /// <summary>
        /// Helper to generate the ISSI SID From/To header.
        /// </summary>
        /// <param name="sysId"></param>
        /// <param name="netId"></param>
        /// <param name="dstId"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        private string GenerateISSISID(uint sysId, uint netId, uint dstId, bool group = true)
        {
            // bryanb: the TIA-102 engineers were taking the good stuff when they came up with this shit...
            string sid = $"sip:{netId.ToString("X5")}{sysId.ToString("X3")}{dstId.ToString("X4")}@{SIP_ISSI_P25DR};user={SIP_ISSI_P25_SG}";
            if (!group)
                sid = $"sip:{netId.ToString("X5")}{sysId.ToString("X3")}{dstId.ToString("X6")}@{SIP_ISSI_P25DR};user={SIP_ISSI_P25_SU}";

            return sid;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sysId"></param>
        /// <param name="netId"></param>
        /// <param name="dstId"></param>
        /// <param name="group"></param>
        private async void CallRemote(uint sysId, uint netId, uint dstId, bool group, uint streamId)
        {
            SIPUserAgent ua = new SIPUserAgent(sipTransport, null);
            ua.OnCallHungup += OnHangup;

            string sipRoute = GenerateISSIInviteRoute(sysId, netId, group);
            string sipFromTo = GenerateISSISID(sysId, netId, dstId, group);

            RTPSession rtpSession = CreateRTPSession(ua, null);
            SIPCallDescriptor descriptor = new SIPCallDescriptor($"sip:*@{Program.Configuration.RemoteIssiAddress}:{Program.Configuration.RemoteSipPort}", null);
            descriptor.From = sipFromTo;
            descriptor.To = sipFromTo;
            descriptor.RouteSet = sipRoute;
            descriptor.ContentType = $"application/{SIP_ISSI_CONTENT_TYPE}";

            bool callResult = await ua.Call(descriptor, rtpSession);
            if (callResult)
            {
                await rtpSession.Start();
                calls.TryAdd(ua.Dialogue.CallId, ua);

                callIdToStreamId.TryAdd(ua.Dialogue.CallId, streamId);
                outgoingCalls.TryAdd(streamId, ua);
                outgoingRtp.TryAdd(streamId, rtpSession);
            }
        }

        /// <summary>
        /// Handler for incoming SIP requests.
        /// </summary>
        /// <param name="localSIPEndPoint"></param>
        /// <param name="remoteEndPoint"></param>
        /// <param name="sipRequest"></param>
        /// <returns></returns>
        private async Task SipTransport_SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                switch (sipRequest.Method)
                {
                    case SIPMethodsEnum.BYE:
                        {
                            SIPResponse byeResp = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                            await sipTransport.SendResponseAsync(byeResp);
                        }
                        break;
                    case SIPMethodsEnum.INVITE:
                        {
                            SIPUserAgent ua = new SIPUserAgent(sipTransport, null);
                            ua.OnCallHungup += OnHangup;

                            // answer call
                            SIPServerUserAgent sua = ua.AcceptCall(sipRequest);
                            RTPSession session = CreateRTPSession(ua, sipRequest.URI.User);
                            session.OnRtpPacketReceived += (ep, type, rtp) => OnRtpPacketReceived(ua, type, rtp);
                            await ua.Answer(sua, session);
                            if (ua.IsCallActive)
                            {
                                await session.Start();
                                calls.TryAdd(ua.Dialogue.CallId, ua);
                            }
                        }
                        break;
                    case SIPMethodsEnum.OPTIONS:
                        {
                            SIPResponse optResp = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                            await sipTransport.SendResponseAsync(optResp);
                        }
                        break;

                    default:
                        Log.Logger.Error($"SIP {sipRequest.Method} request received but no processing has been set up for it, rejecting.");
                        break;
                }
            }
            catch (NotImplementedException)
            {
                Log.Logger.Error($"{sipRequest.Method} request processing not implemented for {sipRequest.URI.ToParameterlessString()} from {remoteEndPoint}.");

                SIPResponse notImplResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.NotImplemented, null);
                await sipTransport.SendResponseAsync(notImplResponse);
            }
        }

        /// <summary>
        /// Remove call from the active calls list.
        /// </summary>
        /// <param name="dialogue">The dialogue that was hungup.</param>
        private void OnHangup(SIPDialogue dialogue)
        {
            if (dialogue != null)
            {
                string callId = dialogue.CallId;
                uint streamId = 0U;
                if (callIdToStreamId.ContainsKey(callId))
                {
                    streamId = callIdToStreamId[callId];
                    if (outgoingRtp.ContainsKey(streamId))
                        outgoingRtp.TryRemove(streamId, out _);
                    if (outgoingCalls.ContainsKey(streamId))
                        outgoingCalls.TryRemove(streamId, out _);
                }

                if (calls.ContainsKey(callId))
                {
                    if (calls.TryRemove(callId, out var ua))
                        ua.Close();
                }
            }
        }

        /// <summary>
        /// Stops the main execution loop for this <see cref="FneSystemBase"/>.
        /// </summary>
        public void Stop()
        {
            if (fne.IsStarted)
                fne.Stop();

            sipTransport.RemoveSIPChannel(sipListener);
            if (sipListener != null)
            {
                sipListener.Close();
                sipListener.Dispose();
            }
        }

        /// <summary>
        /// Callback used to process whether or not a peer is being ignored for traffic.
        /// </summary>
        /// <param name="peerId">Peer ID</param>
        /// <param name="srcId">Source Address</param>
        /// <param name="dstId">Destination Address</param>
        /// <param name="slot">Slot Number</param>
        /// <param name="callType">Call Type (Group or Private)</param>
        /// <param name="frameType">Frame Type</param>
        /// <param name="dataType">DMR Data Type</param>
        /// <param name="streamId">Stream ID</param>
        /// <returns>True, if peer is ignored, otherwise false.</returns>
        protected virtual bool PeerIgnored(uint peerId, uint srcId, uint dstId, byte slot, CallType callType, FrameType frameType, DMRDataType dataType, uint streamId)
        {
            return false;
        }

        /// <summary>
        /// Event handler used to handle a peer connected event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void PeerConnected(object sender, PeerConnectedEvent e)
        {
            return;
        }
    } // public abstract partial class FneSystemBase
} // namespace dvmissi
