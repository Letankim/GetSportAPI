using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Wallet
{
    public int WalletId { get; set; }

    public int UserId { get; set; }

    public decimal Balance { get; set; }

    public DateTime Createdat { get; set; }

    public DateTime? Updatedat { get; set; }

    public virtual Account User { get; set; } = null!;

    public virtual ICollection<Wallettransaction> Wallettransactions { get; set; } = new List<Wallettransaction>();
}
