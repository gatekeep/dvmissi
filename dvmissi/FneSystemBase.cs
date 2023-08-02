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

using dvmissi.FNE;
using dvmissi.FNE.DMR;

using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

namespace dvmissi
{
    /// <summary>
    /// Represents the individual timeslot data status.
    /// </summary>
    public class SlotStatus
    {
        /// <summary>
        /// Rx Start Time
        /// </summary>
        public DateTime RxStart = DateTime.Now;
        
        /// <summary>
        /// 
        /// </summary>
        public uint RxSeq = 0;
        
        /// <summary>
        /// Rx RF Source
        /// </summary>
        public uint RxRFS = 0;
        /// <summary>
        /// Tx RF Source
        /// </summary>
        public uint TxRFS = 0;
        
        /// <summary>
        /// Rx Stream ID
        /// </summary>
        public uint RxStreamId = 0;
        /// <summary>
        /// Tx Stream ID
        /// </summary>
        public uint TxStreamId = 0;
        
        /// <summary>
        /// Rx TG ID
        /// </summary>
        public uint RxTGId = 0;
        /// <summary>
        /// Tx TG ID
        /// </summary>
        public uint TxTGId = 0;
        /// <summary>
        /// Tx Privacy TG ID
        /// </summary>
        public uint TxPITGId = 0;
        
        /// <summary>
        /// Rx Time
        /// </summary>
        public DateTime RxTime = DateTime.Now;
        /// <summary>
        /// Tx Time
        /// </summary>
        public DateTime TxTime = DateTime.Now;
        
        /// <summary>
        /// Rx Type
        /// </summary>
        public FrameType RxType = FrameType.TERMINATOR;
        
        /** DMR Data */
        /// <summary>
        /// Rx Link Control Header
        /// </summary>
        public LC DMR_RxLC = null;
        /// <summary>
        /// Rx Privacy Indicator Link Control Header
        /// </summary>
        public PrivacyLC DMR_RxPILC = null;
        /// <summary>
        /// Tx Link Control Header
        /// </summary>
        public LC DMR_TxHLC = null;
        /// <summary>
        /// Tx Privacy Link Control Header
        /// </summary>
        public PrivacyLC DMR_TxPILC = null;
        /// <summary>
        /// Tx Terminator Link Control
        /// </summary>
        public LC DMR_TxTLC = null;
    } // public class SlotStatus

    /// <summary>
    /// Implements a FNE system.
    /// </summary>
    public abstract partial class FneSystemBase
    {
        private const int P25_FIXED_SLOT = 2;

        public const int SAMPLE_RATE = 8000;
        public const int BITS_PER_SECOND = 16;

        private const int MBE_SAMPLES_LENGTH = 160;

        private const int AUDIO_BUFFER_MS = 20;
        private const int AUDIO_NO_BUFFERS = 2;
        private const int AFSK_AUDIO_BUFFER_MS = 60;
        private const int AFSK_AUDIO_NO_BUFFERS = 4;

        private const int TX_MODE_DMR = 1;
        private const int TX_MODE_P25 = 2;

        protected FneBase fne;

        private bool callInProgress = false;

        private SlotStatus[] status;

        private Random rand;
        private uint txStreamId;

        private SIPTransport sipTransport;
        private SIPChannel sipListener;
        
        private ConcurrentDictionary<string, SIPUserAgent> calls = new ConcurrentDictionary<string, SIPUserAgent>();

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

            // initialize slot statuses
            this.status = new SlotStatus[3];
            this.status[0] = new SlotStatus();  // DMR Slot 1
            this.status[1] = new SlotStatus();  // DMR Slot 2
            this.status[2] = new SlotStatus();  // P25

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
            // TODO TODO TODO
        }

        /// <summary>
        /// 
        /// </summary>
        private async void CallRemote()
        {
            SIPUserAgent ua = new SIPUserAgent(sipTransport, null);
            ua.OnCallHungup += OnHangup;

            RTPSession rtpSession = CreateRTPSession(ua, null);
            var callResult = await ua.Call($"sip:CHANGEME@{Program.Configuration.RemoteIssiAddress}:{Program.Configuration.RemoteSipPort}", null, null, rtpSession);

            if (callResult)
            {
                await rtpSession.Start();
                calls.TryAdd(ua.Dialogue.CallId, ua);
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
                            RTPSession rtpSession = CreateRTPSession(ua, sipRequest.URI.User);
                            await ua.Answer(sua, rtpSession);
                            if (ua.IsCallActive)
                            {
                                await rtpSession.Start();
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
