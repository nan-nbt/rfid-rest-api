using System.Runtime.InteropServices;
using System.Text;
using RFIDAPI.Models;

namespace RFIDAPI.Helpers;

public static class UhfApi
{
    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private extern static int TCPConnect(StringBuilder ip, uint hostport);

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UHF_GetReceived_EX(ref int uLenUii, byte[] uUii);

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UHFInventory();

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UHFStopGet();

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private extern static void TCPDisconnect();

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UHFWriteData(byte[] uAccessPwd, byte FilterBank, int FilterStartaddr, int FilterLen, byte[]? FilterData, byte uBank, int uPtr, byte uCnt, byte[] uDatabuf);

    [DllImport("UHFAPI.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int UHFReadData(byte[] uAccessPwd,
     byte FilterBank,
     int FilterStartaddr,
     int FilterLen,
     byte[]? FilterData,
     byte uBank,
     int uPtr,
     int uCnt,
     byte[] uReadDatabuf,
     ref int uReadDataLen);

    public static bool TcpConnect(string ip, uint port)
    {
        StringBuilder bIp = new StringBuilder();
        bIp.Append(ip);
        int result = TCPConnect(bIp, port);
        if (result == 0)
        {
            return true;
        }
        return false;
    }

    public static bool StartInventory()
    {
        int result = UHFInventory();
        if (result == 0)
            return true;
        else
            return false;
    }

    public static bool StopInventory()
    {
        if (UHFStopGet() == 0)
            return true;
        else
            return false;
    }

    public static bool ReadTagFromBuffer(ref int len, ref byte[] data)
    {
        if (UHF_GetReceived_EX(ref len, data) == 0)
        {
            return true;
        }
        return false;
    }

    public static UhfTagInfo? ReadTagFromBuffer()
    {
        int uLen = 0;
        byte[] bufData = new byte[256];
        if (ReadTagFromBuffer(ref uLen, ref bufData))
        {
            string epc_data = string.Empty;
            string tid_data = string.Empty; //tid數據
            string rssi_data = string.Empty;
            string ant_data = string.Empty;
            string user_data = string.Empty;

            int uii_len = bufData[0];//uii長度
            int tid_leng = bufData[uii_len + 1];//tid數據長度
            int tid_idex = uii_len + 2;//tid起始位
            int rssi_index = 1 + uii_len + 1 + tid_leng;
            int ant_index = rssi_index + 2;

            string strData = BitConverter.ToString(bufData, 0, uLen).Replace("-", "");
            epc_data = strData.Substring(6, uii_len * 2 - 4);  //Epc

            if (tid_leng > 12)
            {
                tid_data = strData.Substring(tid_idex * 2, 24); //Tid
                user_data = strData.Substring(tid_idex * 2 + 24, (tid_leng - 12) * 2); //Tid
            }
            else
            {
                tid_data = strData.Substring(tid_idex * 2, tid_leng * 2); //Tid
                if (tid_data.Length < 8)
                {
                    tid_data = "";
                }
            }

            string temp = strData.Substring(rssi_index * 2, 4);
            int rssiTemp = Convert.ToInt32(temp, 16) - 65535;
            rssi_data = ((float)rssiTemp / 10.0).ToString();// RSSI  =  (0xFED6   -65535)/10
            if (!rssi_data.Contains("."))
            {
                rssi_data = rssi_data + ".0";
            }
            ant_data = Convert.ToInt32((strData.Substring(ant_index * 2, 2)), 16).ToString();

            UhfTagInfo info = new UhfTagInfo();
            info.Epc = epc_data;
            info.Tid = tid_data;
            info.Rssi = rssi_data;
            info.Ant = ant_data;
            info.User = user_data;

            return info;
        }
        else
        {
            return null;
        }
    }

    public static void TcpDisconnect()
    {
        TCPDisconnect();
    }

    public static bool WriteData(byte[] uAccessPwd, byte FilterBank, int FilterStartaddr, int FilterLen, byte[]? FilterData, byte uBank, int uPtr, byte uCnt, byte[] uDatabuf)
    {
        if (UHFWriteData(uAccessPwd, FilterBank, FilterStartaddr, FilterLen, FilterData, uBank, uPtr, uCnt, uDatabuf) == 0)
        {
            return true;
        }
        return false;
    }

    public static string ReadData(byte[] uAccessPwd, byte FilterBank, int FilterStartaddr, int FilterLen, byte[]? FilterData, byte uBank, int uPtr, int uCnt)
    {
        try
        {
            byte[] uReadDatabuf = new byte[2048];
            int uReadDataLen = 0;

            int result = UHFReadData(uAccessPwd, FilterBank, FilterStartaddr, FilterLen, FilterData, uBank, uPtr, uCnt, uReadDatabuf, ref uReadDataLen);
            if (result == 0)
            {
                return DataConvert.ByteArrayToHexString(uReadDatabuf, uReadDataLen);
            }
        }
        catch (DllNotFoundException)
        {
            // The native reader library is optional on development machines.
            return string.Empty;
        }

        return string.Empty;
    }
}
