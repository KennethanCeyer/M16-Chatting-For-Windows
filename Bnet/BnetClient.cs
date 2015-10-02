﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Bnet.BnetConnect;

namespace Bnet
{
    // This project can output the Class library as a NuGet Package.
    // To enable this option, right-click on the project and select the Properties menu item. In the Build tab select "Produce outputs on build".
    public class BnetClient : BnetHandler
    {
        public ManualResetEvent connectDone = new ManualResetEvent(false);
        public ManualResetEvent receiveDone = new ManualResetEvent(false);

        public delegate void OnChatLoginedDelegate(String user);
        public delegate void OnChatUserDelegate(String user, String message);

        public static event OnChatLoginedDelegate OnChatLogined;
        public static event OnChatUserDelegate OnChatUser;

        private static Dictionary<String, String> bnetConInfo;
        private Socket bnetSock;
        private BnetProtocol bnetProtocol = new BnetProtocol();
        private byte[] sockBuffer = new byte[1024];
        private String bnetUsrId, bnetUserPw;
        public String bnetUserUid;
        public BnetHelper bnetHelper = BnetHelper.getInstance();
        public BnetPacketStream bnetPacketStream = new BnetPacketStream();
        public BnetClient(String bnetConIP, String bnetConPort)
        {
            this.debugMode = true;
            bnetProtocol.clientToken = (uint) Math.Abs(new Random().Next());
            bnetConInfo = new Dictionary<String, String>();
            bnetConInfo.Add("ip", bnetConIP);
            bnetConInfo.Add("port", bnetConPort);
        }

        public void Connect(String userId, String userPw)
        {
            bnetUsrId = userId;
            bnetUserPw = userPw;
            IPAddress bnetServerIP = Dns.GetHostAddresses(bnetConInfo["ip"])[0];
            IPEndPoint bnetServerEP = new IPEndPoint(bnetServerIP, Int32.Parse(bnetConInfo["port"]));
            this.bnetSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.bnetSock.ReceiveTimeout = 3000;
            this.bnetSock.SendTimeout = 3000;
            this.getHandleMsg(BnetCode.ConnectionWithServer);
            try {
                connectDone.Reset();
                this.bnetSock.BeginConnect(bnetServerEP, new AsyncCallback(OnConnectCallback), this.bnetSock);
                connectDone.WaitOne();
            } catch(Exception e)
            {
                this.getHandleMsg(BnetCode.ConnectionFailed);
                this.getHandleMsg(e.StackTrace);

                throw e;
            }
        }

        public void MusicBot()
        {
            string[] Song =
            {
                "안녕하세요 M16 IB채널 봇입니다.",
                "이곳은 Impossible Bossies 클랜 채널입니다.",
                "불가능한 보스잡기 게임을 주관하는 클랜입니다~",
                "있다 저녁에 한 겜할분, 대기하시길"
            };
            uint i = 0;
            while (true)
            {
                bnetProtocol.setBnetByte(Song[i], true);
                bnetProtocol.send(this.bnetSock, BnetPacketModel.SID_CHATCOMMAND);
                Thread.Sleep(15000);
                i = (uint) ((i + 1) % Song.Length);
            }
        }

        public void setChatMessage(String message)
        {
            receiveDone.WaitOne();
            bnetProtocol.setBnetByte(message, true);
            bnetProtocol.send(this.bnetSock, BnetPacketModel.SID_CHATCOMMAND);
        }

        public void OnConnectCallback(IAsyncResult IAR)
        {
            connectDone.Set();
            receiveDone.Reset();

            Socket tmpSock = (Socket) IAR.AsyncState;
            tmpSock.EndConnect(IAR);
            this.BindREceiveHandler(tmpSock);
            this.bnetSock = tmpSock;

            try
            {
                byte[] triedConnection = { 0x01 };
                tmpSock.Send(triedConnection);
            }
            catch (Exception e)
            {
                this.getHandleMsg(BnetCode.ConnectionFailed);
                this.getHandleMsg(e.StackTrace);
            }
            this.getHandleMsg(BnetCode.ConnectionSuccess);

            Int32 bnetClientTimeOffset = Convert.ToInt32(TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).TotalMilliseconds / -60000);
            bnetProtocol.setBnetByte(0x00000000);
            bnetProtocol.setBnetByte(0x49583836); // Platform IX86
            bnetProtocol.setBnetByte(0x44534852);
            //bnetProtocol.setBnetByte(0x4452544c); // DRTL
            bnetProtocol.setBnetByte(0x00000000); // Version  0.0
            bnetProtocol.setBnetByte("koKR");     // Language
            bnetProtocol.setBnetByte(0x00000000);
            bnetProtocol.setBnetByte(bnetClientTimeOffset);
            bnetProtocol.setBnetByte(0x412);
            bnetProtocol.setBnetByte(0x412);
            bnetProtocol.setBnetByte("KOR", true);
            bnetProtocol.setBnetByte("Korea", true);
            bnetProtocol.send(tmpSock, BnetPacketModel.SID_AUTH_INFO);
        }

        public void BindREceiveHandler(Socket cSock)
        {
            cSock.BeginReceive(sockBuffer, 0, sockBuffer.Length, SocketFlags.None, new AsyncCallback(OnReceiveCallback), cSock);
        }

        public void OnReceiveCallback(IAsyncResult IAR)
        {
            try
            {
                receiveDone.Reset();
                Socket recvSock = (Socket)IAR.AsyncState;
                int receiveLen = recvSock.EndReceive(IAR);
                receiveDone.Set();
                if (receiveLen > 0)
                {
                    byte[] receiveBuffer = this.sockBuffer;
                    if (receiveBuffer[0] == 0xFF)
                    {
                        int bnetResult;
                        BnetPacketStruct bnetPackSt = bnetProtocol.decapsulize(receiveBuffer);
                        switch (bnetPackSt.packet_id)
                        {
                            case BnetPacketModel.SID_OPTIONALWORK:
                            case BnetPacketModel.SID_EXTRAWORK:
                            case BnetPacketModel.SID_REQUIREDWORK:
                                break;
                            case BnetPacketModel.SID_NULL:
                                bnetProtocol.send(recvSock, BnetPacketModel.SID_NULL);
                                break;
                            case BnetPacketModel.SID_PING:
                                bnetProtocol.setBnetByte(bnetPackSt.pack_data.ToArray());
                                bnetProtocol.send(recvSock, BnetPacketModel.SID_PING);
                                this.getHandleMsg(BnetCode.ConnectionPING);
                                break;
                            case BnetPacketModel.SID_AUTH_INFO:
                                bnetProtocol.nlsRevision = bnetPacketStream.readDword(bnetPackSt.pack_data.ToArray(), 0);
                                bnetProtocol.serverToken = bnetPacketStream.readDword(bnetPackSt.pack_data.ToArray(), 4);

                                bnetProtocol.setBnetByte(bnetProtocol.clientToken);
                                bnetProtocol.setBnetByte(0x00000000); // EXE Version
                                bnetProtocol.setBnetByte(0x00000000); // EXE Hash
                                bnetProtocol.setBnetByte(0x00000001); // Number of CD-Key
                                bnetProtocol.setBnetByte(0x00000000); // Spawn CD-Key
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte(0x00000000);
                                bnetProtocol.setBnetByte("war3.exe 03/18/11 20:03:55 471040", true);
                                bnetProtocol.setBnetByte("Chat", true);
                                bnetProtocol.send(recvSock, BnetPacketModel.SID_AUTH_CHECK);
                                this.getHandleMsg(BnetCode.ConnectionAUTH_INFO);
                                break;
                            case BnetPacketModel.SID_AUTH_CHECK:
                                bnetResult = BitConverter.ToInt32(bnetPackSt.pack_data.ToArray(), 0);
                                if (bnetResult != 0)
                                {
                                    switch (bnetResult)
                                    {
                                        case 0x201:
                                            this.getHandleMsg(BnetCode.ServerBen);
                                            break;
                                        default:
                                            this.getHandleMsg(BnetCode.UnkownError);
                                            break;
                                    }
                                }
                                else
                                {
                                    byte[] bnetPwHash = bnetProtocol.encriptDobuleHash(bnetUserPw);

                                    bnetProtocol.setBnetByte(bnetProtocol.clientToken);
                                    bnetProtocol.setBnetByte(bnetProtocol.serverToken);
                                    bnetProtocol.setBnetByte(BitConverter.ToUInt32(bnetPwHash, 0));
                                    bnetProtocol.setBnetByte(BitConverter.ToUInt32(bnetPwHash, 4));
                                    bnetProtocol.setBnetByte(BitConverter.ToUInt32(bnetPwHash, 8));
                                    bnetProtocol.setBnetByte(BitConverter.ToUInt32(bnetPwHash, 12));
                                    bnetProtocol.setBnetByte(BitConverter.ToUInt32(bnetPwHash, 16));
                                    bnetProtocol.setBnetByte(bnetUsrId, true);
                                    bnetProtocol.send(recvSock, BnetPacketModel.SID_LOGONRESPONSE2);
                                    this.getHandleMsg(BnetCode.ConnectionAUTH_CHECK);
                                }
                                break;
                            case BnetPacketModel.SID_LOGONRESPONSE2:
                                bnetResult = BitConverter.ToInt32(bnetPackSt.pack_data.ToArray(), 0);
                                switch (bnetResult)
                                {
                                    case 0x00:
                                        this.getHandleMsg(BnetCode.LOGONRESP_Success);
                                        bnetProtocol.setBnetByte(0x00);
                                        bnetProtocol.setBnetByte(0x00);
                                        bnetProtocol.send(recvSock, BnetPacketModel.SID_ENTERCHAT);
                                        break;
                                    case 0x01:
                                        this.getHandleMsg(BnetCode.LOGONRESP_FaildID);
                                        break;
                                    case 0x02:
                                        this.getHandleMsg(BnetCode.LOGONRESP_FaildPW);
                                        break;
                                    case 0x06:
                                        this.getHandleMsg(BnetCode.LOGONRESP_LockedID);
                                        break;
                                    default:
                                        this.getHandleMsg(BnetCode.UnkownError);
                                        break;
                                }
                                break;
                            case BnetPacketModel.SID_ENTERCHAT:
                                this.getHandleMsg(BnetCode.ENTERCHAT);
                                bnetProtocol.setBnetByte(0x01);
                                bnetProtocol.setBnetByte("ib", true);
                                bnetProtocol.send(recvSock, BnetPacketModel.SID_JOINCHANNEL);
                                this.getHandleMsg(Encoding.UTF8.GetString(bnetPackSt.pack_data.ToArray()));
                                this.bnetUserUid = bnetPackSt.getData(bnetPackSt.pack_data.ToArray());
                                OnChatLogined(this.bnetUserUid);
                                Thread musicBotThread = new Thread(new ThreadStart(MusicBot));
                                musicBotThread.Start();
                                break;
                            case BnetPacketModel.SID_CHATEVENT:
                                BnetPacketEvent bnetPacketEvent = (BnetPacketEvent)BitConverter.ToUInt32(bnetPackSt.pack_data.ToArray(), 0);
                                uint flags = BitConverter.ToUInt32(bnetPackSt.pack_data.ToArray(), 4);
                                uint ping = BitConverter.ToUInt32(bnetPackSt.pack_data.ToArray(), 8);
                                string user = bnetPackSt.getData(bnetPackSt.pack_data.ToArray(), 24);
                                this.getHandleMsg(bnetPacketEvent.ToString());
                                this.getHandleMsg(flags.ToString());
                                this.getHandleMsg(user);

                                switch (bnetPacketEvent)
                                {
                                    case BnetPacketEvent.EID_CHANNEL:
                                        break;
                                    case BnetPacketEvent.EID_SHOWUSER:
                                        break;
                                    case BnetPacketEvent.EID_ERROR:
                                    case BnetPacketEvent.EID_INFO:
                                        break;
                                    case BnetPacketEvent.EID_BROADCAST:
                                        break;
                                    case BnetPacketEvent.EID_WHISPER:
                                    case BnetPacketEvent.EID_WHISPERSENT:
                                    case BnetPacketEvent.EID_TALK:
                                        String message = bnetPackSt.getData(bnetPackSt.pack_data.ToArray());
                                        this.getHandleMsg(message);
                                        OnChatUser(user, message);
                                        break;
                                    case BnetPacketEvent.EID_JOIN:
                                        break;
                                    case BnetPacketEvent.EID_LEAVE:
                                        break;
                                    default:
                                        bnetProtocol.setBnetByte(0x00000000);
                                        bnetProtocol.setBnetByte(0x00000000);
                                        bnetProtocol.setBnetByte(0x00000000);
                                        bnetProtocol.setBnetByte(0x00000000);
                                        bnetProtocol.send(recvSock, BnetPacketModel.SID_CHECKAD);
                                        break;
                                }
                                break;
                        }
                    }
                }
                this.BindREceiveHandler(recvSock);
            }
            catch (SocketException e)
            {
                this.getHandleMsg(e.StackTrace);
            }
        }
    }
}