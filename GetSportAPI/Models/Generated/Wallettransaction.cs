using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Wallettransaction
{
    public int TransactionId { get; set; }

    public int WalletId { get; set; }

    public decimal Amount { get; set; }

    public int Direction { get; set; }

    public string Type { get; set; } = null!;

    public int? Relatedid { get; set; }

    public DateTime Createdat { get; set; }

    public string? Bankinfo { get; set; }

    public string? Comment { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
}
