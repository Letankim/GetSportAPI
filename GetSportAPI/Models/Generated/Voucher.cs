using System;
using System.Collections.Generic;

namespace GetSportAPI.Models.Generated;

public partial class Voucher
{
    public int VoucherId { get; set; }

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Discountpercent { get; set; }

    public DateTime Startdate { get; set; }

    public DateTime Enddate { get; set; }

    public int? Usagelimit { get; set; }

    public int Usage { get; set; }

    public bool Isactive { get; set; }

    public virtual ICollection<Uservoucher> Uservouchers { get; set; } = new List<Uservoucher>();
}
