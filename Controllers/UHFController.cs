using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class UHFController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly RFIDConnectionManager _connectionManager;
    // private static UHFService[] services = new UHFService[10];
    // private static readonly object _lock = new object();
    // private static readonly Dictionary<string, int> connectedClients = new Dictionary<string, int>();
    // private static readonly Dictionary<string, string> connectedDevices = new Dictionary<string, string>();

    /*
     * Constructor
     */
    public UHFController(IDbContextFactory<AppDbContext> factory, RFIDConnectionManager connectionManager)
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
                status = "Unable to identify client IP"
            });
        }

        if (_connectionManager.IsClientConnected(clientIp))
        {
            return new JsonResult(new
            {
                result = false,
                status = "This client already connected"
            });
        }

        string rfidKey = ip + ":" + port;

        if (string.IsNullOrEmpty(rfidKey))
        {
            return BadRequest(new
            {
                result = false,
                status = "RFID key is empty"
            });
        }

        if (_connectionManager.IsDeviceInUse(ip, port))
        {
            return new JsonResult(new
            {
                result = false,
                status = "RFID device already in use"
            });
        }

        var service = new UHFService(_factory);

        var result = service.Connect(ip, port);

        if (!result)
        {
            return new JsonResult(new
            {
                result = false,
                status = "Can not connect to device host " + ip + ":" + port
            });
        }

        string connectionId = Guid.NewGuid().ToString();
        var connection = new RFIDConnection
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
            status = "Connected"
        });
        
        // var service = new UHFService(_factory);
        // int indexService = 0;

        // for (int i = 0; i < services.Length; i++)
        // {
        //     if (services[i] == null)
        //     {
        //         services[i] = service;
        //         indexService = i;
        //         break;
        //     }
        // }

        // var result = services[indexService].Connect(ip, port);

        // return new JsonResult(new
        // {
        //     result,
        //     indexService,
        //     status = result ? "Connected" : "Can not connect"
        // });
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
                status = "Connection not found"
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
                ? "Disconnected"
                : "Can not disconnect"
        });

        // if (services[indexService] == null)
        // {
        //     return NotFound(new
        //     {
        //         message = "Service not found"
        //     });
        // }

        // var result = services[indexService].Disconnect(ip, port);

        // services[indexService] = null;

        // return new JsonResult(new
        // {
        //     result,
        //     indexService,
        //     status = result ? "Disonnected" : "Can not disconnect"
        // });
    }

    [HttpGet("read")]
    public async Task<IActionResult> Read([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = "Connection not found"
            });
        }

        var epcCode = await connection.Service.ReadSingleEPC();

        if (epcCode == string.Empty)
        {
            return NotFound(new
            {
                message = "Failed to Read EPC Code"
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
                message = "Connection not found"
            });
        }

        if (string.IsNullOrEmpty(epcCode))
        {
            return NotFound(new
            {
                message = "EPC Code not found"
            });
        }

        if (epcCode.Length != 24)
        {
            return NotFound(new
            {
                message = "EPC Code not valid. Length must be 24"
            });
        }

        var result = await connection.Service.WriteSingleEPC(epcCode);

        return new JsonResult(new
        {
            result,
            epcCode,
            status = result ? "Success" : "Failed"
        });
    }

    /*
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
                message = "Connection not found"
            });
        }

        if (string.IsNullOrEmpty(barcode))
        {
            return NotFound(new
            {
                message = "Barcode parameter is null or empty"
            });
        }

        string hexEpc = await connection.Service.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = "Tag not found or unknown tag error"
            });
        }

        if (!string.Equals(epc, "000000000000000000000000"))
        {
            return NotFound(new
            {
                message = "EPC code not valid. Required EPC code 000000000000000000000000"
            });
        }

        var sampleBarcode = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(sampleBarcode.barcode))
        {
            return NotFound(new
            {
                message = "Barcode " + barcode + " does not exist in database"
            });
        }

        if (sampleBarcode.prod_mk == "Y")
        {
            return NotFound(new
            {
                message = "Barcode " + sampleBarcode.barcode + " already production scanned"
            });
        }

        var writeEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = "Failed to write EPC code"
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeProdMark(sampleBarcode.barcode);

        if (!updateResult)
        {
            var rollbackEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = "Failed to rollback EPC code"
                });
            }

            return NotFound(new
            {
                message = "Failed to update sample barcode prod_mk"
            });
        }

        var result = await connection.Service.FindBarcode(barcode);

        if (string.IsNullOrEmpty(result.barcode))
        {
            return NotFound(new
            {
                message = "Barcode " + barcode + " does not exist in database"
            });
        }

        return new JsonResult(new
        {
            result,
            status = "success"
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
                message = "Connection not found"
            });
        }

        string hexEpc = await connection.Service.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");
        string rfidCode = epc.Substring(0, 20);

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = "Tag not found or unknown tag error"
            });
        }        

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            return NotFound(new
            {
                message = "RFID code " + rfidCode + " does not exist in database"
            });
        }

        if (sampleBarcode.prod_mk == "N")
        {
            return NotFound(new
            {
                message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not production scanned"
            });
        }

        if (sampleBarcode.inout_mk == "A")
        {
            return NotFound(new
            {
                message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " already stocoked in warehouse"
            });
        }

        string demMk = "1111";
        string inoutMk = "A";

        var writeEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = "Failed to write EPC code"
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = "Failed to rollback EPC code"
                });
            }

            return NotFound(new
            {
                message = "Failed to update sample barcode inout_mk"
            });
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(result.rfid_code))
        {
            return NotFound(new
            {
                message = "RFID code " + rfidCode + " does not exist in database"
            });
        }

        return new JsonResult(new
        {
            result,
            status = "success"
        });

        // open comment block for read inventory or multiple tag
        // if (services[indexService] == null)
        // {
        //     return NotFound(new
        //     {
        //         message = "Service not found"
        //     });
        // }

        // var inventory = services[indexService].StartReadInventory();

        // if (!inventory)
        // {
        //     return NotFound(new
        //     {
        //         message = "Failed to start read inventory"
        //     });
        // }

        // List<UHFTAGInfo> tagList = await services[indexService].GetListEPC();

        // if (tagList == null)
        // {
        //     services[indexService].StopReadInventory();

        //     return NotFound(new
        //     {
        //         message = "Failed to get list EPC"
        //     });
        // }

        // foreach (var item in tagList)
        // {
        //     string rfidCode = item.Epc.Replace(" ", "").Substring(0, 20);

        //     var sampleBarcode = await services[indexService].FindRFIDCode(rfidCode);
            
        //     if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + rfidCode + " does not exist in database"
        //         });
        //     }

        //     if (sampleBarcode.prod_mk == "N")
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not production scanned"
        //         });
        //     }

        //     if (sampleBarcode.inout_mk == "A")
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " already stocoked in warehouse"
        //         });
        //     }

        //     string demMk = "1111";
        //     string inoutMk = "A";  

        //     var writeEpc = await services[indexService].WriteMaskEPC(sampleBarcode.rfid_code + demMk, item.Epc.Replace(" ", ""));

        //     if (!writeEpc)
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "Failed to write EPC code"
        //         });
        //     }

        //     var updateResult = await services[indexService].UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        //     if (!updateResult)
        //     {
        //         var rollbackEpc = await services[indexService].WriteMaskEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk, item.Epc.Replace(" ", ""));

        //         if (!rollbackEpc)
        //         {
        //             services[indexService].StopReadInventory();

        //             return NotFound(new
        //             {
        //                 message = "Failed to rollback EPC code"
        //             });
        //         }

        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "Failed to update sample barcode inout_mk"
        //         });
        //     }
        // }

        // services[indexService].StopReadInventory();

        // return new JsonResult(new
        // {
        //     // tagList,
        //     status = "success"
        // });
    }

    [HttpPost("warehouse-stock-out")]
    public async Task<IActionResult> WarehouseStockOut([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);

        if (connection == null)
        {
            return NotFound(new
            {
                message = "Connection not found"
            });
        }

        string hexEpc = await connection.Service.ReadSingleEPC();
        string epc = hexEpc.Replace(" ", "");
        string rfidCode = epc.Substring(0, 20);

        if (string.IsNullOrEmpty(epc))
        {
            return NotFound(new
            {
                message = "Tag not found or unknown tag error"
            });
        }        

        var sampleBarcode = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        {
            return NotFound(new
            {
                message = "RFID code " + rfidCode + " does not exist in database"
            });
        }

        if (sampleBarcode.prod_mk == "N")
        {
            return NotFound(new
            {
                message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not production scanned"
            });
        }

        if (string.IsNullOrEmpty(sampleBarcode.inout_mk))
        {
            connection.Service.StopReadInventory();

            return NotFound(new
            {
                message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not stocoked in warehouse yet"
            });                
        }

        if (sampleBarcode.inout_mk == "B")
        {
            return NotFound(new
            {
                message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " already stocoked out warehouse"
            });
        }

        string demMk = "0000";
        string inoutMk = "B";

        var writeEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + demMk);

        if (!writeEpc)
        {
            return NotFound(new
            {
                message = "Failed to write EPC code"
            });
        }

        var updateResult = await connection.Service.UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        if (!updateResult)
        {
            var rollbackEpc = await connection.Service.WriteSingleEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk);

            if (!rollbackEpc)
            {
                return NotFound(new
                {
                    message = "Failed to rollback EPC code"
                });
            }

            return NotFound(new
            {
                message = "Failed to update sample barcode inout_mk"
            });
        }

        var result = await connection.Service.FindRFIDCode(rfidCode);
        
        if (string.IsNullOrEmpty(result.rfid_code))
        {
            return NotFound(new
            {
                message = "RFID code " + rfidCode + " does not exist in database"
            });
        }

        return new JsonResult(new
        {
            result,
            status = "success"
        });

        // open comment block for read inventory or multiple tag
        // if (services[indexService] == null)
        // {
        //     return NotFound(new
        //     {
        //         message = "Service not found"
        //     });
        // }

        // var inventory = services[indexService].StartReadInventory();

        // if (!inventory)
        // {
        //     return NotFound(new
        //     {
        //         message = "Failed to start read inventory"
        //     });
        // }

        // List<UHFTAGInfo> tagList = await services[indexService].GetListEPC();

        // if (tagList == null)
        // {
        //     services[indexService].StopReadInventory();
            
        //     return NotFound(new
        //     {
        //         message = "Failed to get list EPC"
        //     });
        // }

        // foreach (var item in tagList)
        // {
        //     string rfidCode = item.Epc.Replace(" ", "").Substring(0, 20);

        //     var sampleBarcode = await services[indexService].FindRFIDCode(rfidCode);
            
        //     if (string.IsNullOrEmpty(sampleBarcode.rfid_code))
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + rfidCode + " does not exist in database"
        //         });
        //     }

        //     if (sampleBarcode.prod_mk == "N")
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not production scanned"
        //         });
        //     }

        //     if (string.IsNullOrEmpty(sampleBarcode.inout_mk))
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " not stocoked in warehouse yet"
        //         });                
        //     }

        //     if (sampleBarcode.inout_mk == "B")
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "RFID code " + sampleBarcode.rfid_code + sampleBarcode.dem_mk + " already stocoked out warehouse"
        //         });
        //     }

        //     string demMk = "0000";
        //     string inoutMk = "B";

        //     var writeEpc = await services[indexService].WriteMaskEPC(sampleBarcode.rfid_code + demMk, item.Epc.Replace(" ", ""));

        //     if (!writeEpc)
        //     {
        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "Failed to write EPC code"
        //         });
        //     }

        //     var updateResult = await services[indexService].UpdateSampleBarcodeInoutMark(sampleBarcode.rfid_code, demMk, inoutMk);

        //     if (!updateResult)
        //     {
        //         var rollbackEpc = await services[indexService].WriteMaskEPC(sampleBarcode.rfid_code + sampleBarcode.dem_mk, item.Epc.Replace(" ", ""));

        //         if (!rollbackEpc)
        //         {
        //             services[indexService].StopReadInventory();

        //             return NotFound(new
        //             {
        //                 message = "Failed to rollback EPC code"
        //             });
        //         }

        //         services[indexService].StopReadInventory();

        //         return NotFound(new
        //         {
        //             message = "Failed to update sample barcode inout_mk"
        //         });
        //     }
        // }

        // services[indexService].StopReadInventory();

        // return new JsonResult(new
        // {
        //     tagList,
        //     status = "success"
        // });
    }

    [HttpGet("start-read")]
    public IActionResult StartRead([FromQuery] string connectionId)
    {
        var connection = _connectionManager.GetConnection(connectionId);
        
        if (connection == null)
        {
            return NotFound(new
            {
                message = "Connection not found"
            });
        }

        var result = connection.Service.StartReadInventory();

        return new JsonResult(new
        {
            result,
            status = result ? "Start read inventory" : "Failed to start read inventory"
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
                message = "Connection not found"
            });
        }

        var result = connection.Service.StopReadInventory();

        return new JsonResult(new
        {
            result,
            status = result ? "Stop read inventory" : "Failed to stop read inventory"
        });
    }
}