using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RFIDAPI.Configurations;
using RFIDAPI.Constants;
using RFIDAPI.Helpers;
using RFIDAPI.Managers;
using RFIDAPI.Models;
using RFIDAPI.Services;

namespace RFIDAPI.Controllers;

/**
 * UhfController
 * RBAC (Role Based Access Control) Integration
 * @required {Authorization, Client Custom Role}
 * - Authorization: Bearer - OAuth2.0
 * - Client Custom Role: rfid-client-service
 */
[ApiController]
[Route("api/uhf")]
[Authorize(Roles = "default-roles-service")]
public class UhfController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly RfidConnectionManager _connectionManager;

    /**
     * Constructor
     * @param {IDbContextFactory<AppDbContext> factory, RfidConnectionManager connectionManager}
     */
    public UhfController(IDbContextFactory<AppDbContext> factory, RfidConnectionManager connectionManager)
    {
        _factory = factory;
        _connectionManager = connectionManager;
    }

    private IActionResult OkResponse<T>(T data)
    {
        return StatusCode(StatusCodes.Status200OK, ApiResponse.Ok(data));
    }

    private IActionResult CreatedResponse<T>(T data)
    {
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Success(StatusCodes.Status201Created, data));
    }

    private IActionResult ErrorResponse(int statusCode, string errorMessage)
    {
        return StatusCode(statusCode, ApiResponse.Error(statusCode, errorMessage));
    }

    /**
     * RFID main method
     * @function {connect, disconnect, read, write, start-read, stop-read}
     */
    [HttpGet("connect")]
    public IActionResult Connect([FromQuery] string ip, [FromQuery] uint port)
    {
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(clientIp))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, ConnectionStatus.UNABLE_IDENTIFY_CLIENT);
        }

        if (_connectionManager.IsClientConnected(clientIp))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, ConnectionStatus.CLIENT_ALREADY_CONNECT);
        }

        if (_connectionManager.IsDeviceInUse(ip, port))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, ConnectionStatus.IN_USE);
        }

        var service = new UhfService(_factory);

        var result = service.Connect(ip, port);

        if (!result)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, $"{ConnectionStatus.CAN_NOT_CONNECT} {ip}:{port}");
        }

        string connectionId = Guid.NewGuid().ToString();
        var connection = new RfidConnection
        {
            ConnectionId = connectionId,
            ClientIp = clientIp,
            DeviceIp = ip,
            DevicePort = port,
            ConnectedAt = DateTime.Now,
            LastActivity = DateTime.Now,
            Service = service
        };

        _connectionManager.AddConnection(connection);

        return OkResponse(new DeviceConnectionData
        {
            DeviceAddress = $"{ip}:{port}",
            ConnectionId = connectionId,
            Message = ConnectionStatus.CONNECTED
        });
    }

    [HttpGet("disconnect")]
    public IActionResult Disconnect([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        string deviceAddress = $"{connection.DeviceIp}:{connection.DevicePort}";

        var result = connection.Service.Disconnect(
            connection.DeviceIp,
            connection.DevicePort);

        _connectionManager.RemoveConnection(connectionId);

        if (!result)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, ConnectionStatus.CAN_NOT_DISCONNECT);
        }

        return OkResponse(new DeviceConnectionData
        {
            DeviceAddress = deviceAddress,
            ConnectionId = null,
            Message = ConnectionStatus.DISCONNECTED
        });
    }

    [HttpGet("read")]
    public async Task<IActionResult> Read([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        var epcCode = await UhfService.ReadSingleEPC();

        if (string.IsNullOrWhiteSpace(epcCode))
        {
            return ErrorResponse(StatusCodes.Status404NotFound, Common.TAG_READ_FAILED);
        }

        var formattedEpc = StringUtils.FormatEpcWithSpaces(epcCode);

        return OkResponse(new TagOperationData
        {
            EpcCode = formattedEpc,
            Message = "Tag read successful"
        });
    }

    [HttpPost("write")]
    public async Task<IActionResult> Write([FromQuery] string connectionId, [FromQuery] string epcCode)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        var normalizedEpc = epcCode.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalizedEpc.Length != 24 || !StringUtils.IsHexNumber(normalizedEpc))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, Common.EPC_NOT_VALID);
        }

        var result = await UhfService.WriteSingleEPC(normalizedEpc);

        if (!result)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_WRITE_FAILED);
        }

        var formattedEpc = StringUtils.FormatEpcWithSpaces(normalizedEpc);

        return OkResponse(new TagOperationData
        {
            EpcCode = formattedEpc,
            Message = "Tag write successful"
        });
    }

    [HttpGet("start-read")]
    public IActionResult StartRead([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        var result = connection.Service.StartReadInventory();

        if (!result)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.START_READ_FAILED);
        }

        return OkResponse(new MessageData
        {
            Message = Common.START_READ_TAG
        });
    }

    [HttpGet("stop-read")]
    public IActionResult StopRead([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        var result = connection.Service.StopReadInventory();

        if (!result)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.STOP_READ_FAILED);
        }

        return OkResponse(new MessageData
        {
            Message = Common.STOP_READ_TAG
        });
    }

    /**
     * RFID custom method
     * @function {production-scan, warehouse-stock-in, warehouse-stock-out}
     */
    [HttpPost("production-scan")]
    public async Task<IActionResult> ProductionScan([FromQuery] string connectionId, [FromQuery] string barcode)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(epc))
        {
            return ErrorResponse(StatusCodes.Status404NotFound, Common.TAG_NOT_FOUND);
        }

        if (!string.Equals(epc, "000000000000000000000000", StringComparison.OrdinalIgnoreCase))
        {
            var expectedBlankEpc = StringUtils.FormatEpcWithSpaces("000000000000000000000000");
            return ErrorResponse(StatusCodes.Status404NotFound, $"EPC code not valid. Required EPC code {expectedBlankEpc}");
        }

        var sampleBarcode = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(sampleBarcode.barcode))
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"{barcode} {Common.BARCODE_NOT_EXIST}");
        }

        if (sampleBarcode.prod_mk == "Y")
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"{sampleBarcode.barcode} {Common.BARCODE_ALREADY_PRODUCTION}");
        }

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

        if (!writeEpc)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_WRITE_FAILED);
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeProdMark(sampleBarcode.barcode);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_ROLLBACK_FAILED);
            }

            return ErrorResponse(StatusCodes.Status500InternalServerError, Common.UPDATE_PROD_FAILED);
        }

        var result = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(result.barcode))
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"[{barcode}] {Common.BARCODE_NOT_EXIST}");
        }

        return OkResponse(result);
    }

    [HttpPost("warehouse-stock-in")]
    public async Task<IActionResult> WarehouseStockIn([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(epc) || epc.Length < 20)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, Common.TAG_NOT_FOUND);
        }

        string rfidCode = epc.Substring(0, 20);

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);

        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(epc);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_EXIST}");
        }

        if (sampleBarcode.prod_mk == "N")
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(sampleBarcode.rfid_code + sampleBarcode.dem_mk);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_PRODUCTION}");
        }

        if (sampleBarcode.inout_mk == "A")
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(sampleBarcode.rfid_code + sampleBarcode.dem_mk);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_IN_WAREHOUSE}");
        }

        string demMk = "1111";
        string inoutMk = "A";

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_WRITE_FAILED);
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_ROLLBACK_FAILED);
            }

            return ErrorResponse(StatusCodes.Status500InternalServerError, Common.UPDATE_INOUT_FAILED);
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);

        if (string.IsNullOrEmpty(result.rfid_code))
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(rfidCode);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_EXIST}");
        }

        return OkResponse(result);
    }

    [HttpPost("warehouse-stock-out")]
    public async Task<IActionResult> WarehouseStockOut([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"The connection_id {connectionId} not found.");
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(epc) || epc.Length < 20)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, Common.TAG_NOT_FOUND);
        }

        string rfidCode = epc.Substring(0, 20);

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);

        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(epc);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_EXIST}");
        }

        if (sampleBarcode.prod_mk == "N")
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(sampleBarcode.rfid_code + sampleBarcode.dem_mk);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_PRODUCTION}");
        }

        if (string.IsNullOrEmpty(sampleBarcode.inout_mk))
        {
            connection.Service.StopReadInventory();

            var formattedEpc = StringUtils.FormatEpcWithSpaces(sampleBarcode.rfid_code + sampleBarcode.dem_mk);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_IN_WAREHOUSE}");
        }

        if (sampleBarcode.inout_mk == "B")
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(sampleBarcode.rfid_code + sampleBarcode.dem_mk);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_OUT_WAREHOUSE}");
        }

        string demMk = "0000";
        string inoutMk = "B";

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_WRITE_FAILED);
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return ErrorResponse(StatusCodes.Status502BadGateway, Common.TAG_ROLLBACK_FAILED);
            }

            return ErrorResponse(StatusCodes.Status500InternalServerError, Common.UPDATE_INOUT_FAILED);
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);

        if (string.IsNullOrEmpty(result.rfid_code))
        {
            var formattedEpc = StringUtils.FormatEpcWithSpaces(rfidCode);
            return ErrorResponse(StatusCodes.Status404NotFound, $"{formattedEpc} {Common.TAG_NOT_EXIST}");
        }

        return OkResponse(result);
    }

    /**
     * database management method
     * @function {CRUD}
     */
    [HttpGet("sample-barcode")]
    public async Task<IActionResult> GetSampleBarcodes()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.SampleBarcodes
            .AsNoTracking()
            .OrderBy(x => x.id)
            .ToListAsync();

        return OkResponse(rows);
    }

    [HttpPost("sample-barcode")]
    public async Task<IActionResult> CreateSampleBarcode([FromBody] SampleBarcode sampleBarcode)
    {
        if (string.IsNullOrWhiteSpace(sampleBarcode.barcode) ||
            string.IsNullOrWhiteSpace(sampleBarcode.rfid_code) ||
            string.IsNullOrWhiteSpace(sampleBarcode.dem_mk) ||
            string.IsNullOrWhiteSpace(sampleBarcode.prod_mk))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Barcode, RFID code, DEM mark, and production mark are required.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.SampleBarcodes.CountAsync() >= 2)
        {
            return ErrorResponse(StatusCodes.Status409Conflict, "The sample barcode table is limited to a maximum of two records.");
        }

        var created = new SampleBarcode
        {
            barcode = sampleBarcode.barcode.Trim(),
            rfid_code = sampleBarcode.rfid_code.Trim(),
            dem_mk = sampleBarcode.dem_mk.Trim(),
            prod_mk = sampleBarcode.prod_mk.Trim(),
            inout_mk = sampleBarcode.inout_mk?.Trim()
        };
        db.SampleBarcodes.Add(created);
        await db.SaveChangesAsync();

        return CreatedResponse(created);
    }

    [HttpPut("sample-barcode/{id:int}")]
    public async Task<IActionResult> UpdateSampleBarcode(int id, [FromBody] SampleBarcode sampleBarcode)
    {
        if (string.IsNullOrWhiteSpace(sampleBarcode.barcode) ||
            string.IsNullOrWhiteSpace(sampleBarcode.rfid_code) ||
            string.IsNullOrWhiteSpace(sampleBarcode.dem_mk) ||
            string.IsNullOrWhiteSpace(sampleBarcode.prod_mk))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Barcode, RFID code, DEM mark, and production mark are required.");
        }

        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.SampleBarcodes.FirstOrDefaultAsync(x => x.id == id);
        if (existing == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"Sample barcode record with ID {id} was not found.");
        }

        existing.barcode = sampleBarcode.barcode.Trim();
        existing.rfid_code = sampleBarcode.rfid_code.Trim();
        existing.dem_mk = sampleBarcode.dem_mk.Trim();
        existing.prod_mk = sampleBarcode.prod_mk.Trim();
        existing.inout_mk = sampleBarcode.inout_mk?.Trim();
        await db.SaveChangesAsync();

        return OkResponse(existing);
    }

    [HttpDelete("sample-barcode/{id:int}")]
    public async Task<IActionResult> DeleteSampleBarcode(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.SampleBarcodes.FirstOrDefaultAsync(x => x.id == id);
        if (existing == null)
        {
            return ErrorResponse(StatusCodes.Status404NotFound, $"Sample barcode record with ID {id} was not found.");
        }

        db.SampleBarcodes.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
