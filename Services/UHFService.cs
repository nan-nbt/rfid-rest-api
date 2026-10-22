using Microsoft.EntityFrameworkCore;
using RFIDAPI.Configurations;
using RFIDAPI.Helpers;
using RFIDAPI.Models;

namespace RFIDAPI.Services;

public class UhfService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private bool _isConnected;
    private bool _isInventory;
    private readonly List<UhfTagInfo> _tagList = [];

    /*
     * Constructor
     */
    public UhfService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /*
     * RFID main method
     * @function {Connect, Disconnect, ReadSingleEpc, WriteSingleEpc, FindBarcode, UpdateSampleBarcodeProdMark}
     */
    public bool Connect(string ip, uint port)
    {
        var connect = UhfApi.TcpConnect(ip, port);
        if (connect)
        {
            _isConnected = true;
            return true;
        }

        return false;
    }

    public bool Disconnect(string ip, uint port)
    {
        if (_isConnected)
        {
            UhfApi.TcpDisconnect();

            _isConnected = false;

            return true;
        }

        return false;
    }

    public bool StartReadInventory()
    {
        var inventory = UhfApi.StartInventory();
        if (inventory)
        {
            _isInventory = true;
            return true;
        }

        return false;
    }

    public bool StopReadInventory()
    {
        if (_isInventory)
        {
            UhfApi.StopInventory();

            _isInventory = false;

            return true;
        }
        
        return false;
    }

    public async Task<List<UhfTagInfo>> GetListEPC()
    {
        var start = Environment.TickCount;

        while (Environment.TickCount - start < 500)
        {
            var info = UhfApi.ReadTagFromBuffer();

            if (info is not null && !_tagList.Any(tag => tag.Epc == info.Epc))
            {
                _tagList.Add(info);
            }

            await Task.Delay(10);
        }

        return _tagList;
    }

    public static Task<string> ReadSingleEPC()
    {
        string filterData = "";
        const string tagAccessCode = "00000000";

        int filterBank = 1;
        byte[]? filterBuff = string.IsNullOrEmpty(filterData)
            ? null
            : DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] accessCodeBytes = DataConvert.HexStringToByteArray(tagAccessCode);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;

        string result = UhfApi.ReadData(accessCodeBytes, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, leng);
        if (result != string.Empty)
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(string.Empty);
    }

    public static Task<bool> WriteSingleEPC(string epc)
    {
        string wdata = epc;
        string filterData = "";
        const string tagAccessCode = "00000000";

        int filterBank = 1;
        byte[]? filterBuff = string.IsNullOrEmpty(filterData)
            ? null
            : DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] accessCodeBytes = DataConvert.HexStringToByteArray(tagAccessCode);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;
        byte[] uDatabuf = DataConvert.HexStringToByteArray(wdata);

        var result = UhfApi.WriteData(accessCodeBytes, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, (byte)leng, uDatabuf);
        return Task.FromResult(result);
    }

    public static Task<bool> WriteMaskEPC(string epc, string mask)
    {
        string wdata = epc;
        string filterData = mask;
        const string tagAccessCode = "00000000";

        int filterBank = 1;
        byte[]? filterBuff = string.IsNullOrEmpty(filterData)
            ? null
            : DataConvert.HexStringToByteArray(filterData);
        int filterPtr = 32;
        int filterLen = 0;

        byte[] accessCodeBytes = DataConvert.HexStringToByteArray(tagAccessCode);
        int bank = 1;
        int Ptr = 2;
        int leng = 6;
        byte[] uDatabuf = DataConvert.HexStringToByteArray(wdata);

        var result = UhfApi.WriteData(accessCodeBytes, (byte)filterBank, filterPtr, filterLen, filterBuff, (byte)bank, Ptr, (byte)leng, uDatabuf);
        return Task.FromResult(result);
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
