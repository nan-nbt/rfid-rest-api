namespace RFIDAPI.Constants;

public static class Common
{
    public const string TAG_NOT_FOUND = "No tag detected. Please ensure an RFID tag is placed within antenna range";
    public const string TAG_NOT_EXIST = "Tag does not exist in database";
    public const string TAG_NOT_PRODUCTION = "Tag has not been scanned for production";
    public const string TAG_IN_WAREHOUSE = "Tag already stocked in warehouse";
    public const string TAG_NOT_IN_WAREHOUSE = "Tag not stocked in warehouse";
    public const string TAG_OUT_WAREHOUSE = "Tag already stocked out warehouse";
    public const string TAG_WRITE_FAILED = "Failed to write EPC code to tag. Please ensure the tag is in antenna range";
    public const string TAG_ROLLBACK_FAILED = "Failed to rollback tag EPC code after database update failure";
    public const string TAG_READ_FAILED = "Failed to read tag EPC code. Please ensure a tag is in antenna range";
    public const string EPC_NOT_FOUND = "EPC code was not found";
    public const string EPC_NOT_VALID = "The epc_code field length must be 24-digit hexadecimal code";
    public const string SUCCESS = "Success";
    public const string FAILED = "Failed";
    public const string BARCODE_NULL_EMPTY = "Barcode parameter is required";
    public const string REQ_INIT_EPC = "EPC code not valid. Required EPC code 00 00 00 00 00 00 00 00 00 00 00 00";
    public const string BARCODE_NOT_EXIST = "Barcode does not exist in database";
    public const string BARCODE_ALREADY_PRODUCTION = "Barcode is already production scanned";
    public const string UPDATE_PROD_FAILED = "Failed to update sample barcode production mark";
    public const string UPDATE_INOUT_FAILED = "Failed to update sample barcode inventory mark";
    public const string START_READ_TAG = "Start read inventory";
    public const string START_READ_FAILED = "The device status on start read inventory. Please stop it first";
    public const string STOP_READ_TAG = "Stop read inventory";
    public const string STOP_READ_FAILED = "Failed to stop read inventory";
}
