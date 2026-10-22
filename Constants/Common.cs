using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RFIDAPI.Constants;

public static class Common
{
    public const string TAG_NOT_FOUND = "Tag not found or unknown error";
    public const string TAG_NOT_EXIST = "Tag does not exist in database";
    public const string TAG_NOT_PRODUCTION = "Tag not production scanned";
    public const string TAG_IN_WAREHOUSE = "Tag already stocked in warehouse";
    public const string TAG_NOT_IN_WAREHOSUE = "Tag not stocked in warehouse";
    public const string TAG_OUT_WAREHOUSE = "Tag already stocked out warehouse";
    public const string TAG_WRITE_FAILED = "Tag write EPC code failed";
    public const string TAG_ROLLBACK_FAILED = "Tag rollback EPC code failed";
    public const string TAG_READ_FAILED = "Tag read EPC code failed";
    public const string EPC_NOT_FOUND = "EPC code not found";
    public const string EPC_NOT_VALID = "EPC code not valid. length must be 24 digit aplhanumeric";
    public const string SUCCESS = "Success";
    public const string FAILED = "Failed";
    public const string BARCODE_NULL_EMPTY = "Barcode parameter is null or empty";
    public const string REQ_INIT_EPC = "EPC code not valid. Required EPC code 000000000000000000000000";
    public const string BARCODE_NOT_EXIST = "Barcode does not exist in database";
    public const string BARCODE_ALREADY_PRODUCTION = "Barcode is already production scanned";
    public const string UPDATE_PROD_FAILED = "Failed to update sample barcode prod_mk";
    public const string UPDATE_INOUT_FAILED = "Failed to update sample barcode inout_mk";
    public const string START_READ_TAG = "Start read inventory";
    public const string START_READ_FAILED = "Failed to start read inventory";
    public const string STOP_READ_TAG = "Stop read inventory";
    public const string STOP_READ_FAILED = "Failed to stop read inventory";
}