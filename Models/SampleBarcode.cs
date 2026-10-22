using System.ComponentModel.DataAnnotations.Schema;

namespace RFIDAPI.Models;

[Table("sample_barcode")]
public class SampleBarcode
{
    public int id { get; set; }

    public string barcode { get; set; } = string.Empty;

    public string rfid_code { get; set; } = string.Empty;

    public string dem_mk { get; set; } = string.Empty;

    public string prod_mk { get; set; } = string.Empty;

    public string? inout_mk { get; set; }
}
