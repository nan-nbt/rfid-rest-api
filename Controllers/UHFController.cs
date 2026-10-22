using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RFIDAPI.Configurations;
using RFIDAPI.Managers;
using RFIDAPI.Models;
using RFIDAPI.Services;
using RFIDAPI.Constants;

namespace RFIDAPI.Controllers;

/**
 * UhfController
 * RBAC (Role Based Access Control) Integration
 * @required {Authorization, Client Custom Role}
 * - Authorization: Bearer - OAuth2.0
 * - Client Custom Role: rfid-client-service
 */
[ApiController]
[Route("api/UHF")]
[Authorize(Roles = "rfid-client-service")]
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

    /**
     * RFID main method
     * @return {Connect, Disconnect, Read, Write}
     */
    [HttpGet("connect")]
    public IActionResult Connect([FromQuery] string ip, [FromQuery] uint port)
    {
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(clientIp))
        {
            return BadRequest(new
            {
                result = false,
                status = ConnectionStatus.UNABLE_IDENTIFY_CLIENT
            });
        }

        if (_connectionManager.IsClientConnected(clientIp))
        {
            return new JsonResult(new
            {
                result = false,
                status = ConnectionStatus.CLIENT_ALREADY_CONNECT
            });
        }

        string rfidKey = ip + ":" + port;

        if (string.IsNullOrEmpty(rfidKey))
        {
            return BadRequest(new
            {
                result = false,
                status = ConnectionStatus.KEY_EMPTY
            });
        }

        if (_connectionManager.IsDeviceInUse(ip, port))
        {
            return new JsonResult(new
            {
                result = false,
                status = ConnectionStatus.IN_USE
            });
        }

        var service = new UhfService(_factory);

        var result = service.Connect(ip, port);

        if (!result)
        {
            return new JsonResult(new
            {
                result = false,
                status = ConnectionStatus.CAN_NOT_CONNECT + " " + ip + ":" + port
            });
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
        
        return new JsonResult(new
        {
            result = true,
            connectionId,
            status = ConnectionStatus.CONNECTED
        });
    }

    [HttpGet("disconnect")]
    public IActionResult Disconnect([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                result = false,
                status = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        var result = connection.Service.Disconnect(
            connection.DeviceIp,
            connection.DevicePort);

        _connectionManager.RemoveConnection(connectionId);

        return new JsonResult(new
        {
            result,
            status = result
                ? ConnectionStatus.DISCONNECTED
                : ConnectionStatus.CAN_NOT_DISCONNECT
        });
    }

    [HttpGet("read")]
    public async Task<IActionResult> Read([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        var epcCode = await UhfService.ReadSingleEPC();

        if (epcCode == string.Empty)
        {
            return NotFound(new
            {
                message = Common.TAG_READ_FAILED
            });
        }

        return new JsonResult(new
        {
            epcCode
        });
    }

    [HttpPost("write")]
    public async Task<IActionResult> Write([FromQuery] string connectionId, [FromQuery] string epcCode)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        if (string.IsNullOrEmpty(epcCode))
        {
            return NotFound(new
            {
                message = Common.EPC_NOT_FOUND
            });
        }

        if (epcCode.Length != 24)
        {
            return NotFound(new
            {
                message = Common.EPC_NOT_VALID
            });
        }

        var result = await UhfService.WriteSingleEPC(epcCode);

        return new JsonResult(new
        {
            result,
            epcCode,
            status = result ? Common.SUCCESS : Common.FAILED
        });
    }

    /**
     * RFID custom method
     * @function {ProductionScan, WarehouseInOut}
     */
    [HttpPost("production-scan")]
    public async Task<IActionResult> ProductionScan([FromQuery] string connectionId, [FromQuery] string barcode)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        if (string.IsNullOrEmpty(barcode))
        {
            return NotFound(new
            {
                message = Common.BARCODE_NULL_EMPTY
            });
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = Common.TAG_NOT_FOUND
            });
        }

        if (!string.Equals(epc, "000000000000000000000000"))
        {
            return NotFound(new
            {
                message = Common.REQ_INIT_EPC
            });
        }

        var sampleBarcode = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(sampleBarcode.barcode))
        {
            return NotFound(new
            {
                message = "[" + barcode + "] " + Common.BARCODE_NOT_EXIST
            });
        }

        if (sampleBarcode.prod_mk == "Y")
        {
            return NotFound(new
            {
                message = "[" + sampleBarcode.barcode + "] " + Common.BARCODE_ALREADY_PRODUCTION
            });
        }

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = Common.TAG_WRITE_FAILED
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeProdMark(sampleBarcode.barcode);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = Common.TAG_ROLLBACK_FAILED
                });
            }

            return NotFound(new
            {
                message = Common.UPDATE_PROD_FAILED
            });
        }

        var result = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(result.barcode))
        {
            return NotFound(new
            {
                message = "[" + barcode + "] " + Common.BARCODE_NOT_EXIST
            });
        }

        return new JsonResult(new
        {
            result,
            status = Common.SUCCESS
        });
    }

    [HttpPost("warehouse-stock-in")]
    public async Task<IActionResult> WarehouseStockIn([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");
        string rfidCode = epc.Substring(0, 20);

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = Common.TAG_NOT_FOUND
            });
        }        

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            return NotFound(new
            {
                message = "[" + rfidCode + "] " + Common.TAG_NOT_EXIST
            });
        }

        if (sampleBarcode.prod_mk == "N")
        {
            return NotFound(new
            {
                message = "[" + sampleBarcode.rfid_code + sampleBarcode.dem_mk + "] " + Common.TAG_NOT_PRODUCTION
            });
        }

        if (sampleBarcode.inout_mk == "A")
        {
            return NotFound(new
            {
                message = "[" + sampleBarcode.rfid_code + sampleBarcode.dem_mk + "] " + Common.TAG_IN_WAREHOUSE
            });
        }

        string demMk = "1111";
        string inoutMk = "A";

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = Common.TAG_WRITE_FAILED
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = Common.TAG_ROLLBACK_FAILED
                });
            }

            return NotFound(new
            {
                message = Common.UPDATE_INOUT_FAILED
            });
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(result.rfid_code))
        {
            return NotFound(new
            {
                message = "[" + rfidCode + "] " + Common.TAG_NOT_EXIST
            });
        }

        return new JsonResult(new
        {
            result,
            status = Common.SUCCESS
        });
    }

    [HttpPost("warehouse-stock-out")]
    public async Task<IActionResult> WarehouseStockOut([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        string hexEpc = await UhfService.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");
        string rfidCode = epc.Substring(0, 20);

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = Common.TAG_NOT_FOUND
            });
        }        

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            return NotFound(new
            {
                message = "[" + rfidCode + "] " + Common.TAG_NOT_EXIST
            });
        }

        if (sampleBarcode.prod_mk == "N")
        {
            return NotFound(new
            {
                message = "[" + sampleBarcode.rfid_code + sampleBarcode.dem_mk + "] " + Common.TAG_NOT_PRODUCTION
            });
        }

        if (string.IsNullOrEmpty(sampleBarcode.inout_mk))
        {
            connection.Service.StopReadInventory();

            return NotFound(new
            {
                message = "[" + sampleBarcode.rfid_code + sampleBarcode.dem_mk + "] " + Common.TAG_NOT_IN_WAREHOSUE
            });                
        }

        if (sampleBarcode.inout_mk == "B")
        {
            return NotFound(new
            {
                message = "[" + sampleBarcode.rfid_code + sampleBarcode.dem_mk + "] " + Common.TAG_OUT_WAREHOUSE
            });
        }

        string demMk = "0000";
        string inoutMk = "B";

        var writeEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = Common.TAG_WRITE_FAILED
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await UhfService.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = Common.TAG_ROLLBACK_FAILED
                });
            }

            return NotFound(new
            {
                message = Common.UPDATE_INOUT_FAILED
            });
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(result.rfid_code))
        {
            return NotFound(new
            {
                message = "[" + rfidCode + "] " + Common.TAG_NOT_EXIST
            });
        }

        return new JsonResult(new
        {
            result,
            status = Common.SUCCESS
        });
    }

    [HttpGet("start-read")]
    public IActionResult StartRead([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);
        
        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        var result = connection.Service.StartReadInventory();

        return new JsonResult(new
        {
            result,
            status = result ? Common.START_READ_TAG : Common.START_READ_FAILED
        });
    }

    [HttpGet("stop-read")]
    public IActionResult StopRead([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = ConnectionStatus.CONNECTION_NOT_FOUND
            });
        }

        var result = connection.Service.StopReadInventory();

        return new JsonResult(new
        {
            result,
            status = result ? Common.STOP_READ_TAG : Common.STOP_READ_FAILED
        });
    }
}
