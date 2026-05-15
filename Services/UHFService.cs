using Microsoft.EntityFrameworkCore;

public class UHFService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private bool isConnected = false;
    private bool isInventory = false;
    private List<UHFTAGInfo> tagList = new List<UHFTAGInfo>();
    private UHFAPI uhf;

    /*
     * Constructor
     */
    public UHFService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        uhf = new UHFAPI();
    }

    /*
     * RFID main method
     * @function {Connect, Disconnect, ReadSingleEpc, WriteSingleEpc, FindBarcode, UpdateSampleBarcodeProdMark}
     */
    public bool Connect(string ip, uint port)
    {
        var connect = uhf.TcpConnect(ip, port);
        if (connect)
        {
            isConnected = true;
            return true;
        }

        return false;
    }

    public bool Disconnect(string ip, uint port)
    {
        if (isConnected)
        {
            uhf.TcpDisconnect();

            isConnected = false;

            return true;
        }

        return false;
    }

    public bool StartReadInventory()
    {
        var inventory = uhf.StartInventory();
        if (inventory)
        {
            isInventory = true;
            return true;
        }

        return false;
    }

    public bool StopReadInventory()
    {
        if (isInventory)
        {
            uhf.StopInventory();

            isInventory = false;

            return true;
        }
        
        return false;
    }

    public async Task<List<UHFTAGInfo>> GetListEPC()
    {
        var start = Environment.TickCount;

        while (Environment.TickCount - start < 500)
        {
            var info = uhf.ReadTagFromBuffer();

            if (info != null && !tagList.Any(t => t.Epc == info.Epc))
            {
                tagList.Add(info);
            }

            Thread.Sleep(10);
        }

        return tagList;
    }

    public async Task<string> ReadSingleEPC()
    {
        string filterData = "";
        string accessPwd = "00000000";

        int filterBank = 1;
        byte[] filterBuff = DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] pwd = DataConvert.HexStringToByteArray(accessPwd);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;

        string result = uhf.ReadData(pwd, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, leng);
        if (result != string.Empty)
        {
            return result;
        }

        return string.Empty;
    }

    public async Task<bool> WriteSingleEPC(string epc)
    {
        string wdata = epc;
        string filterData = "";
        string accessPwd = "00000000";

        int filterBank = 1;
        byte[] filterBuff = DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] pwd = DataConvert.HexStringToByteArray(accessPwd);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;
        byte[] uDatabuf = DataConvert.HexStringToByteArray(wdata);

        var result = uhf.WriteData(pwd, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, (byte)leng, uDatabuf);
        if (result)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> WriteMaskEPC(string epc, string mask)
    {
        string wdata = epc;
        string filterData = mask;
        string accessPwd = "00000000";

        int filterBank = 1;
        byte[] filterBuff = DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] pwd = DataConvert.HexStringToByteArray(accessPwd);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;
        byte[] uDatabuf = DataConvert.HexStringToByteArray(wdata);

        var result = uhf.WriteData(pwd, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, (byte)leng, uDatabuf);
        if (result)
        {
            return true;
        }

        return false;
    }

    public async Task<SampleBarcode> FindBarcode(string barcode)
    {
        using var _db = _factory.CreateDbContext();
        var sampleBarcode = await _db.SampleBarcodes.FirstOrDefaultAsync(x => x.barcode == barcode);

        if (sampleBarcode == null)
        {
            return new SampleBarcode();
        }

        return sampleBarcode;
    }

    public async Task<SampleBarcode> FindRFIDCode(string rfidCode)
    {
        using var _db = _factory.CreateDbContext();
        var sampleBarcode = await _db.SampleBarcodes.FirstOrDefaultAsync(x => x.rfid_code == rfidCode);

        if (sampleBarcode == null)
        {
            return new SampleBarcode();
        }

        return sampleBarcode;
    }

    public async Task<bool> UpdateSampleBarcodeProdMark(string barcode)
    {
        using var _db = _factory.CreateDbContext();
        var sampleBarcode = await _db.SampleBarcodes.FirstOrDefaultAsync(x => x.barcode == barcode);

        if (sampleBarcode == null)
        {
            return false;
        }

        sampleBarcode.prod_mk = "Y";

        int result = await _db.SaveChangesAsync();
        if (result <= 0)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateSampleBarcodeInoutMark(string rfidCode, string dem_mk, string inoutMark)
    {
        using var _db = _factory.CreateDbContext();
        var sampleBarcode = await _db.SampleBarcodes.FirstOrDefaultAsync(x => x.rfid_code == rfidCode);

        if (sampleBarcode == null)
        {
            return false;
        }

        sampleBarcode.dem_mk = dem_mk;
        sampleBarcode.inout_mk = inoutMark;

        int result = await _db.SaveChangesAsync();
        if (result <= 0)
        {
            return false;
        }

        return true;
    }
}