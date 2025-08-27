using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Uservoucher
{
    public int UservoucherId { get; set; }

    public int UserId { get; set; }

    public int VoucherId { get; set; }

    public DateTime? Usedat { get; set; }

    public DateTime Assignedat { get; set; }

    public virtual Account User { get; set; } = null!;

    public virtual Voucher Voucher { get; set; } = null!;
}
