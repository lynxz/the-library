using Azure;
using Azure.Data.Tables;

namespace TheLibrary.Api.Models;

public class UserAccount : ITableEntity
{
    public string PartitionKey { get; set; } = "USER";
    public string RowKey { get; set; } = string.Empty; // username
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
