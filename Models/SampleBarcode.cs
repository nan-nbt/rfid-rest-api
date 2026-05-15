using System.ComponentModel.DataAnnotations.Schema;

[Table("sample_barcode")]
public class SampleBarcode
{
    public int id { get; set; }

    public string barcode { get; set; }

    public string rfid_code { get; set; }

    public string dem_mk { get; set; }

    public string prod_mk { get; set; }

    public string? inout_mk { get; set; }
}